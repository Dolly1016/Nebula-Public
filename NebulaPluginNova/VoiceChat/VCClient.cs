using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Nebula.VoiceChat;

public class VCClient : IDisposable
{
    public enum VCState
    {
        Discussion,
        Dead,
        Distanced,
        GhostVC,
        Comm,
    }

    private OpusDotNet.OpusDecoder myDecoder;
    private BufferedWaveProvider bufferedProvider;
    private VolumeSampleProvider volumeFilter;
    private VolumeMeter volumeMeter;
    private PanningSampleProvider panningFilter;
    private PlayerControl relatedControl;
    private GamePlayer? relatedInfo = null;
    public MixingSampleProvider? myRoute = null;
    private float wallRatio = 1f;
    private int radioMask;
    private DataEntry<float>? volumeEntry = null;
    public uint sId = 0;

    //PUIDがわからない相手のボリュームを一時保存する
    private static Dictionary<string, float> lowLevelVolumeSaver = new();


    public float level = 0f;
    public float Level => level;
    public bool IsSpeaking => Level > 0.045f;

    public float Volume { get => volumeEntry?.Value ?? (lowLevelVolumeSaver.TryGetValue(relatedControl.name, out var val) ? val : 4f); }
    public void SetVolume(float val) {
        if (volumeEntry == null)
        {
            lowLevelVolumeSaver[relatedControl.name] = val;
        }
        else
            volumeEntry.Value = val;
    }

    public PlayerControl MyPlayer => relatedControl;

    //発話者の条件から決定されるVoiceType (直接会話中、ラジオで会話中、語り掛け中 など)
    public VoiceType InputtedVoiceType { get; private set; } = VoiceType.Normal;
    //音声を聞くにあたって提案されたVoiceType (直接の会話よりマイクからの音声を採択 など)
    public VoiceType ListenAsVoiceType { get; private set; } = VoiceType.Normal;

    void UpdateVoiceRoute()
    {
        var type = InputtedVoiceType;

        //通常の音声に対してラジオとして試聴しようとする場合、採択する
        if (type == VoiceType.Normal && ListenAsVoiceType == VoiceType.Radio) type = VoiceType.Radio;

        var route = NebulaGameManager.Instance!.VoiceChatManager?.GetRoute(type);
        SetRoute(route);
    }

    public void SetVoiceType(VoiceType voiceType)
    {
        if (voiceType == InputtedVoiceType) return;

        InputtedVoiceType = voiceType;
        UpdateVoiceRoute();
    }

    public void SetListenAsVoiceType(VoiceType voiceType)
    {
        if (voiceType == ListenAsVoiceType) return;

        ListenAsVoiceType = voiceType;
        UpdateVoiceRoute();
    }

    public bool IsValid => relatedControl;
    public bool CanHear => (PlayerControl.LocalPlayer.Data.IsDead || !relatedControl.Data.IsDead);
    public VCClient(PlayerControl player) {
        relatedControl = player;

        myDecoder = new(24000, 1);
        bufferedProvider = new(new(22050, 1));
        var floatConverter = new WaveToSampleProvider(new Wave16ToFloatProvider(bufferedProvider));
        volumeFilter = new(floatConverter);
        volumeMeter = new(volumeFilter, player.AmOwner ? (() => !(NebulaGameManager.Instance?.VoiceChatManager?.CanListenSelf ?? false)) : (() => false));
        panningFilter = new(volumeMeter);
        panningFilter.Pan = 0f;

        
        IEnumerator CoSetVolumeEntry()
        {
            string puid = "";
            while (puid.Length == 0)
            {
                yield return new WaitForSeconds(1f);
                yield return PropertyRPC.CoGetProperty<string>(player.PlayerId, "myPuid", result => puid = result, null);
            }
            Debug.Log($"Gain PUID of {player.name} ({player.PlayerId} : {puid})");
            if (puid.Length == 0) puid = player.name;
            volumeEntry = new FloatDataEntry(puid, VoiceChatManager.VCSaver, 2f);
        }

        NebulaManager.Instance.StartCoroutine(CoSetVolumeEntry().WrapToIl2Cpp());
    }

    public void OnGameStart()
    {
        relatedInfo = NebulaGameManager.Instance!.GetPlayer(relatedControl.PlayerId);
    }

    private void CheckCanHear(out float volume, out float pan, Vector2 speeker)
    {
        try
        {
            var lightRadius = PlayerControl.LocalPlayer.lightSource.viewDistance;
            //幽霊の語りかけは固定範囲
            if (InputtedVoiceType == VoiceType.Ghost) lightRadius = 1.8f;

            Vector2 ownerPos = PlayerControl.LocalPlayer.transform.position;
            Vector2 myPos = speeker;

            float distance = myPos.Distance(ownerPos);

            //幽霊の語りかけは壁を無視する
            if (InputtedVoiceType != VoiceType.Ghost && GeneralConfigurations.WallsBlockAudioOption && NebulaPhysicsHelpers.AnyShadowBetween(ownerPos, (myPos - ownerPos).normalized, distance, out _))
                wallRatio *= 0.9f;
            else
                wallRatio += (1f - wallRatio) * 0.9f;

            float distanceRatio = 1f;

            if (distance > lightRadius * 1.7f)
                distanceRatio = 0f;
            else if (distance > lightRadius * 0.7f)
                distanceRatio = 1f - (distance - lightRadius * 0.7f) / (lightRadius * 1f);

            volume = Mathf.Clamp01(distanceRatio) * wallRatio;

            float xDis = myPos.x - ownerPos.x;
            pan = Mathf.Clamp(xDis / 1.4f, -1f, 1f);
        }
        catch
        {
            volume = 0f;
            pan = 0f;
        }
    }

    private void UpdateAudio()
    {
        bool atLobby = AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined;
        bool atEnd = AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Ended;

        //常に出来る限り普通に聞こうとするものとする
        SetListenAsVoiceType(VoiceType.Normal);

        if (atEnd)
        {
            volumeFilter.Volume = 1f;
            panningFilter.Pan = 0f;
            return;
        }

        //幽霊として語りかけていないとき
        if (InputtedVoiceType != VoiceType.Ghost)
        {
            if (!atLobby && !CanHear)
            {
                volumeFilter.Volume = 0f;
                panningFilter.Pan = 0f;
                return;
            }

            //互いに死んでいる場合
            if (!atLobby && VoiceChatManager.ListenerIsPerfectlyDead && relatedControl.Data.IsDead)
            {
                volumeFilter.Volume = NebulaGameManager.Instance?.VoiceChatManager?.FilteringMode == VCFilteringMode.AliveOnly ? 0f : 1f;
                panningFilter.Pan = 0f;
                return;
            }

            //ラジオ
            if (!(relatedControl.Data?.IsDead ?? false) && InputtedVoiceType == VoiceType.Radio)
            {
                volumeFilter.Volume = ((1 << PlayerControl.LocalPlayer.PlayerId) & radioMask) == 0 ? 0f : 1f;
                panningFilter.Pan = 0f;
                return;
            }

            //会議中
            if (!atLobby && VoiceChatManager.IsInDiscussion)
            {
                if (VoiceChatManager.ListenerIsDead && NebulaGameManager.Instance?.VoiceChatManager?.FilteringMode == VCFilteringMode.DeadOnly)
                    volumeFilter.Volume = 0f;
                else
                    volumeFilter.Volume = (VoiceChatManager.ListenerIsDead || !relatedControl.Data!.IsDead) ? 1f : 0f;
                
                panningFilter.Pan = 0f;
                return;
            }

            //コミュサボ
            if (!atLobby && GeneralConfigurations.AffectedByCommsSabOption && !VoiceChatManager.ListenerIsPerfectlyDead && (!PlayerControl.LocalPlayer.Data.Role?.IsImpostor ?? true) && AmongUsUtil.InCommSab)
            {
                volumeFilter.Volume = 0f;
                return;
            }
        }

        float volume, pan;
        CheckCanHear(out volume, out pan, relatedControl.transform.position);
        
        //普通に話している生存者
        if(InputtedVoiceType == VoiceType.Normal && !(relatedControl.Data?.IsDead ?? false))
        {
            if (GeneralConfigurations.CanTalkInWanderingPhaseOption)
            {
                foreach (var mic in NebulaGameManager.Instance!.VoiceChatManager!.AllMics())
                {
                    float micVolume = mic.CanCatch(relatedControl.transform.position);
                    if (!(micVolume > 0f)) continue;

                    foreach (var speaker in NebulaGameManager.Instance!.VoiceChatManager!.AllSpeakers())
                    {
                        if (!speaker.CanPlaySoundFrom(mic)) continue;

                        CheckCanHear(out var speakerVol, out var speakerPan, speaker.Position);
                        speakerVol *= micVolume;

                        if (speakerVol > volume)
                        {
                            volume = speakerVol;
                            pan = speakerPan;

                            //ラジオの声を採用
                            SetListenAsVoiceType(VoiceType.Radio);
                        }
                    }
                }
            }
            else
            {
                //タスクフェイズ中の通話機能が無効になっている場合
                volume = 0f;
                pan = 0f;
            }
        }

        volumeFilter.Volume = volume;
        panningFilter.Pan = pan;
    }

    public void Update()
    {
        UpdateAudio();
        volumeFilter.Volume *= Volume;
        
        level -= Time.deltaTime * 1.4f;
        level = Mathf.Max(level, volumeMeter.Level);

        stock.RemoveAll(s => s.sId <= this.sId);
        while (stock.Count > 0)
        {
            var lastCount = stock.Count;
            for(int i = 0; i < stock.Count; i++)
            {
                if(stock[i].sId == this.sId + 1)
                {
                    PushData(stock[i].sId, stock[i].isRadio, stock[i].radioMask, stock[i].data);
                    stock.RemoveAt(i);
                    break;
                }
            }
            if (lastCount == stock.Count) break;
        }
    }

    public void Dispose()
    {
        myDecoder?.Dispose();
        myDecoder = null!;

        SetRoute(null);
    }

    public ISampleProvider MyProvider { get => panningFilter; }

    private byte[] rawAudioData = new byte[5760]; 

    private void PushData(uint sId, bool isRadio, int radioMask, byte[] data)
    {
        this.sId = sId;

        SetVoiceType((isRadio && !(relatedControl.Data?.IsDead ?? false)) ? VoiceType.Radio : VoiceType.Normal);


        if (InputtedVoiceType != VoiceType.Radio)
        {
            if ((relatedControl.Data?.IsDead ?? false) && VoiceChatManager.CanListenGhostVoice(relatedInfo))
                SetVoiceType(VoiceType.Ghost);
            else
                SetVoiceType(VoiceType.Normal);
        }

        //聴こえない音に対しては何もしない
        if (InputtedVoiceType != VoiceType.Ghost && !CanHear) return;

        this.radioMask = radioMask;

        int rawSize = myDecoder!.Decode(data, data.Length, rawAudioData, rawAudioData.Length);

        try
        {
            if (bufferedProvider!.BufferedBytes == 0)
                bufferedProvider!.AddSamples(new byte[1024], 0, 1024);
            

            bufferedProvider!.AddSamples(rawAudioData, 0, rawSize);
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private List<(uint sId, bool isRadio, int radioMask, byte[] data)> stock = new();

    private void KeepData(uint sId, bool isRadio, int radioMask, byte[] data)
    {
        stock.Add((sId, isRadio, radioMask, data));
        stock.Sort((a,b) => (int)((long)b.sId - (long)a.sId));
        //常に末尾に直近のデータがある
    }

    public void OnReceivedData(uint sId, bool isRadio, int radioMask, byte[] data)
    {
        if (sId < this.sId) return;

        if (sId == this.sId + 1 || sId > this.sId + 10) PushData(sId, isRadio, radioMask, data);
        else KeepData(sId, isRadio, radioMask, data);
        
    }

    public void SetRoute(MixingSampleProvider? route)
    {
        if (myRoute != null) myRoute.RemoveMixerInput(MyProvider);
        myRoute = route;
        myRoute?.AddMixerInput(MyProvider);
    }
}
