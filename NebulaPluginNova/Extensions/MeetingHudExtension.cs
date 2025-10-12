using AmongUs.Data;
using AmongUs.InnerNet.GameDataMessages;
using Assets.CoreScripts;
using Epic.OnlineServices.Presence;
using Hazel;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Nebula.Patches;
using Nebula.Roles.Impostor;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using static PlayerMaterial;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Extensions;

[NebulaRPCHolder]
public static class MeetingHudExtension
{
    //タイマーなど、MeetingHudの拡張
    static public float DiscussionTimer = 0f;
    static public float VotingTimer = 0f;
    static public float LeftTime => DiscussionTimer + VotingTimer;
    static public float ResultTimer = 0f;
    static private int VoteForMask = 0;
    static private int CanVoteMask = 0;
    static private int SealedMask = 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static private bool CanVoteForByMask(byte playerId) => (VoteForMask & (1 << playerId)) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static private bool CanUseAbilityForByMask(byte playerId) => (SealedMask & (1 << playerId)) == 0;

    static public bool CanVoteFor(byte playerId) => !(GamePlayer.GetPlayer(playerId)?.IsDead ?? true) && CanVoteForByMask(playerId) && CanUseAbilityForByMask(playerId);
    static public bool CanVoteFor(GamePlayer player) => !player.IsDead && CanVoteForByMask(player.PlayerId) && CanUseAbilityForByMask(player.PlayerId);
    static public bool CanUseAbilityFor(GamePlayer player, bool shouldBeAlive) => CanUseAbility && (!shouldBeAlive || !player.IsDead) && CanUseAbilityForByMask(player.PlayerId);
    static public bool HasVote(byte playerId) => (CanVoteMask & (1 << playerId)) != 0;
    static public bool CanSkip = true;
    static public bool ExileEvenIfTie = false;
    static public bool IsObvious = false;
    static public bool CanShowPhotos = true;
    static public float ActionCoolDown = 0f;
    static public bool CanInvokeSomeAction => !(ActionCoolDown > 0f);
    static public int LastSharedCount = 110;

    //自分自身が投票権を持つ場合、True。
    static public bool CanVote { set; get => field && (CanVoteMask & (1 << GamePlayer.LocalPlayer!.PlayerId)) != 0; }
    //自分自身が会議内で能力を使える場合、True。
    static public bool CanUseAbility = true;

    static public GamePlayer? LastReporter = null;
    //直近の投票の結果吊られるプレイヤー
    static public PlayerControl[]? ExiledAll = null;
    static public GamePlayer[]? ExiledAllModCache = null;
    
    //直近の投票がタイであったかどうか
    static public bool WasTie = false;

    public static void UpdateVotingMask(int mask) => VoteForMask = mask;
    public static void UpdateSealedMask(int mask) => SealedMask = mask;
    public static void AddSealedMask(int mask) => SealedMask |= mask;
    public static void UpdateCanVoteMask(int mask) => CanVoteMask = mask;
    public static void RemoveCanVoteMask(int mask) => CanVoteMask &= ~mask;


    public static void InitMeetingTimer()
    {
        LogicOptionsNormal logicOptionsNormal = GameManager.Instance.LogicOptions.Cast<LogicOptionsNormal>();
        DiscussionTimer = logicOptionsNormal.GetDiscussionTime();
        VotingTimer = logicOptionsNormal.GetVotingTime();
        ResultTimer = 5f;
        VoteForMask = 0xFFFFFFF;
        CanVoteMask = 0xFFFFFFF;
        CanUseAbility = true;
        SealedMask = 0;
        CanSkip = true;
        ExileEvenIfTie = false;
        IsObvious = false;
        ExiledAll = null;
        CanShowPhotos = true;
        ActionCoolDown = 0f;
        LastSharedCount = 110;

        int penalty = (int)(GeneralConfigurations.DeathPenaltyOption * (float)(NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.IsDead) ?? 0f));
        if(GeneralConfigurations.IsInEarlyPhase && GamePlayer.AllPlayers.All(p => !p.IsDead))
        {
            penalty += (int)(float)GeneralConfigurations.EarlyDiscussionReductionOption;
        }

        //DiscussionTimerを引けるだけ引いておいて、引き過ぎた分をVotingTimerに繰り越し
        DiscussionTimer -= penalty;
        if (DiscussionTimer < 0f) VotingTimer += DiscussionTimer;

        //0秒を切らないように調整
        DiscussionTimer = Mathn.Max(0f, DiscussionTimer);
        VotingTimer = Mathn.Max(15f, VotingTimer);
    }

    public static void ReflectVotingMask()
    {
        foreach (var p in MeetingHud.Instance.playerStates)
        {
            if (((1 << p.TargetPlayerId) & VoteForMask) != 0)
                p.SetEnabled();
            else
                p.SetDisabled();
        }

        if (CanSkip && MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.NotVoted && CanVote)
            MeetingHud.Instance.SkipVoteButton.SetEnabled();
        else
            MeetingHud.Instance.SkipVoteButton.SetDisabled();
    }

    public static void UpdatePlayerState(this MeetingHud meetingHud)
    {
        foreach (PlayerVoteArea pva in meetingHud.playerStates)
        {
            var p = NebulaGameManager.Instance?.GetPlayer(pva.TargetPlayerId);
            bool isDead = p == null || p.IsDead || p.WillDie;

            if (pva.AmDead == isDead) continue;

            pva.SetDead(pva.DidReport, isDead);
            pva.Overlay.gameObject.SetActive(isDead);
        }

        ReflectVotingMask();
    }

    public static void ResetPlayerState(this MeetingHud meetingHud)
    {
        if (MeetingHud.Instance.state <= MeetingHud.VoteStates.Discussion) return;

        Reset();

        meetingHud.ClearVote();

        meetingHud.UpdatePlayerState();
        foreach (PlayerVoteArea voter in meetingHud.playerStates)
        {
            voter.ThumbsDown.enabled = false;
            voter.UnsetVote();
            voter.ClearButtons();
        }
        meetingHud.SkipVoteButton.ClearButtons();

        GameOperatorManager.Instance?.Run(new MeetingResetEvent());

        if (AmongUsClient.Instance.AmHost) meetingHud.CheckForEndVoting();
    }

    public static Dictionary<byte, int> WeightMap= new();
    public static Dictionary<byte, int> LastVotedForMap = new();
    public static List<GameObject> LeftContents = new();
    public static List<(byte exiledId, byte sourceId, CommunicableTextTag playerState, CommunicableTextTag eventTag)> ExtraVictims = new();

    public static void Reset()
    {
        LastVotedForMap.Clear();
        WeightMap.Clear();
        ExtraVictims.Clear();
    }

    public static void AddLeftContent(GameObject obj)
    {
        obj.transform.SetParent(MeetingHud.Instance.transform);
        obj.layer = LayerExpansion.GetUILayer();
        obj.transform.localPosition = new Vector3(-4.8f, 1.8f - 1.2f * (float)LeftContents.Count, -40f);
        LeftContents.Add(obj);
        ActionCoolDown = 0.8f;
    }

    public static void ExpandDiscussionTime()
    {
        IEnumerator CoGainDiscussionTime()
        {
            for (int i = 0; i < 10; i++)
            {
                MeetingHudExtension.VotingTimer += 1f;
                MeetingHud.Instance!.lastSecond = 11;
                yield return new WaitForSeconds(0.1f);
            }
        }
        CoGainDiscussionTime().StartOnScene();
    }

    public static void ExileExtraVictims()
    {
        foreach(var victims in ExtraVictims)
        {
            var player = NebulaGameManager.Instance?.GetPlayer(victims.exiledId);
            if (player == null) continue;

            var killer = NebulaGameManager.Instance?.GetPlayer(victims.sourceId);

            player.VanillaPlayer.Exiled();
            player.VanillaPlayer.Data.IsDead = true;
            PlayerExtension.ResetOnDying(player.VanillaPlayer);

            player.Unbox().MyState = victims.playerState;

            //Entityイベント発火
            GameOperatorManager.Instance?.Run(new PlayerExtraExiledEvent(player, killer), true);

            if (player.AmOwner && NebulaAchievementManager.GetRecord("death." + player.PlayerState.TranslationKey, out var recDeath)) new StaticAchievementToken(recDeath);
            if(player.AmOwner) new StaticAchievementToken("stats.death." + player.PlayerState.TranslationKey);

            if ((killer?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + player.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);
            if (player.AmOwner) new StaticAchievementToken("extraVictim");

            NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, victims.sourceId == byte.MaxValue ? null : victims.sourceId, 1 << victims.exiledId) { RelatedTag = victims.eventTag });
        }

        ExtraVictims.Clear();
    }

    public static bool MarkedAsExtraVictims(byte playerId) => ExtraVictims.Any(c => c.exiledId == playerId);

    public static void ModCastVote(this MeetingHud meeting, byte playerId, byte suspectIdx,int votes)
    {
        RpcModCastVote.Invoke((playerId, suspectIdx, votes));
    }

    public static PlayerVoteArea GetPlayer(this MeetingHud instance, int playerId) => instance.playerStates.FirstOrDefault(p => p.TargetPlayerId == playerId)!;
    public static bool TryGetPlayer(this MeetingHud instance, int playerId, [MaybeNullWhen(false)]out PlayerVoteArea pva)
    {
        pva = instance.playerStates.FirstOrDefault(p => p.TargetPlayerId == playerId);
        return pva != null;
    }

    private static RemoteProcess<(byte source, byte target, int weight)> RpcModCastVote = new(
        "CaseVote",
        (message, _) =>
        {
            WeightMap[message.source] = message.weight;
            if (PlayerControl.LocalPlayer.PlayerId == message.source) MeetingHud.Instance.state = MeetingHud.VoteStates.Voted;

            GameOperatorManager.Instance?.Run(new PlayerVoteCastEvent(GamePlayer.GetPlayer(message.source)!, GamePlayer.GetPlayer(message.target), message.weight));

            if (AmongUsClient.Instance.AmHost)
            {
                //MeetingHud.CastVote
                NetworkedPlayerInfo playerById = GameData.Instance.GetPlayerById(message.source);
                var playerVoteArea = MeetingHud.Instance.GetPlayer(message.source);
                
                if (playerVoteArea != null && !playerVoteArea.AmDead && !playerVoteArea.DidVote)
                {
                    if (playerById.AmOwner || AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame) SoundManager.Instance.PlaySound(MeetingHud.Instance.VoteLockinSound, false, 1f, null);
                    
                    playerVoteArea.SetVote(message.target);
                    MeetingHud.Instance.SetDirtyBit(1U);
                    MeetingHud.Instance.CheckForEndVoting();
                    if(GeneralConfigurations.ShowVoteStateOption) PlayerControl.LocalPlayer.RpcSendChatNote(message.source, ChatNoteTypes.DidVote);
                }
            }

            if (!GeneralConfigurations.ShowVoteStateOption)
            {
                SoundManager.Instance.PlaySound(HudManager.Instance.Chat.messageSound, false, 1f, null).pitch = 0.5f + System.Random.Shared.NextSingle() * 0.6f;
            }
        }
        );

    static internal Vector3 VoteAreaPlayerIconPos = new Vector3(-0.95f, 0f, -2.5f);


    internal enum ReportType
    {
        EmergencyMeeting,
        ReportDeadBody,
        ReportDissolvedBody,
    }

    internal static void ModCmdReportDeadBody(GamePlayer player, GamePlayer? deadBody, ReportType reportType)
    {
        LogUtils.WriteToConsole("Called ModCmdReportDeadBody");
        RpcModCmdReportDeadBody.Invoke((player, deadBody, reportType));
    }
    private static readonly RemoteProcess<(GamePlayer player, GamePlayer? deadBody, ReportType reportType)> RpcModCmdReportDeadBody = new("ReportDissolvedDeadBody", (message, _) =>
    {
        if (AmongUsClient.Instance.AmHost) ModReportDeadBody(message.player.VanillaPlayer, GameData.Instance.GetPlayerById(message.deadBody?.PlayerId ?? 255), message.reportType);
    });

    private static readonly RemoteProcess<(GamePlayer reporter, GamePlayer? dead, ReportType reportType)> RpcStartMeeting = new("ReportDeadBody", (message, _) =>
    {
        ModStartMeeting(message.reporter.VanillaPlayer, GameData.Instance.GetPlayerById(message.dead?.PlayerId ?? 255), message.reportType);
    });

    //PlayerControl.ReportDeadBodyの代替メソッド
    internal static void ModReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo? deadBody, ReportType reportType)
    {
        //会議室が開くか否かのチェック
        if (AmongUsClient.Instance.IsGameOver || MeetingHud.Instance) return;

        //フェイクタスクでない緊急タスクがある場合ボタンは押せない
        if (reportType == ReportType.EmergencyMeeting &&
            PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)
            (task => PlayerTask.TaskIsEmergency(task) &&
                (NebulaGameManager.Instance?.LocalFakeSabotage?.MyFakeTasks.All(
                    type => ShipStatus.Instance.GetSabotageTask(type)?.TaskType != task.TaskType) ?? true))) != null) return;

        if (reporter.Data.IsDead) return;
        if (!AmongUsClient.Instance.AmHost) return;

        HudManager.Instance.OpenMeetingRoom(reporter);

        RpcStartMeeting.Invoke((reporter.GetModInfo()!, GamePlayer.GetPlayer(deadBody ? deadBody!.PlayerId : (byte)255), reportType));

        MeetingModRpc.RpcNoticeStartMeeting.Invoke((reporter.PlayerId, deadBody?.PlayerId ?? 255));

        if (reportType == ReportType.EmergencyMeeting)
        {
            NebulaGameManager.Instance!.EmergencyCalls++;
            if (NebulaGameManager.Instance!.EmergencyCalls == GeneralConfigurations.NumOfMeetingsOption) MeetingModRpc.RpcBreakEmergencyButton.Invoke();
        }
    }

    private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, ReportType reportType)
    {
        while (!MeetingHud.Instance) yield return null;
        
        MeetingRoomManager.Instance.RemoveSelf();
        DestroyableSingleton<HudManager>.Instance.InitMap();
        MapBehaviour.Instance.SetPreMeetingPosition(PlayerControl.LocalPlayer.transform.position, false);
        foreach(var player in GamePlayer.AllPlayers)
        {
            if (player.VanillaPlayer) player.VanillaPlayer.ResetForMeeting();
        }

        if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
        if (Minigame.Instance) Minigame.Instance.ForceClose();
        ShipStatus.Instance.OnMeetingCalled();
        KillAnimation.SetMovement(reporter, true);
        GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

        var meetingHud = MeetingHud.Instance;
        meetingHud.StartCoroutine(ModCoMeetingHudIntro(meetingHud, reporter, deadBody, reportType).WrapToIl2Cpp());
        yield break;
    }

    private static IEnumerator ModCoMeetingHudIntro(MeetingHud meetingHud, PlayerControl reporter, NetworkedPlayerInfo? deadBody, ReportType reportType)
    {
        var hudManager = DestroyableSingleton<HudManager>.Instance;

        meetingHud.SkipVoteButton.SetDisabled();
        meetingHud.transform.SetParent(hudManager.transform);
        meetingHud.transform.localPosition = Vector3.zero;
        meetingHud.meetingContents.localPosition = new Vector3(0f, -10f, 0f);

        hudManager.Chat.ForceClosed();
        hudManager.SetHudActive(false);
        hudManager.MapButton.gameObject.SetActive(false);

        var reporterData = reporter.Data;

        bool isEmergencyMeeting = reportType == ReportType.EmergencyMeeting;
        MeetingCalledAnimation meetingCalledAnimation = isEmergencyMeeting ? ShipStatus.Instance.EmergencyOverlay : ShipStatus.Instance.ReportOverlay;
        NetworkedPlayerInfo? networkedPlayerInfo = isEmergencyMeeting ? reporterData : deadBody;
        NetworkedPlayerInfo.PlayerOutfit outfit = NebulaGameManager.Instance!.UnknownOutfit.outfit;
        if (reportType != ReportType.ReportDissolvedBody && networkedPlayerInfo != null) outfit = networkedPlayerInfo.DefaultOutfit;
        hudManager.KillOverlay.ShowMeeting(meetingCalledAnimation, outfit);
        yield return hudManager.KillOverlay.WaitForFinish();
        var orderedDeadBodies = Helpers.AllDeadBodies(true).OrderBy(b => b == null ? 64 : b.ParentId).ToArray();
        ModMeetingIntroInit(meetingHud.MeetingIntro, reporterData, orderedDeadBodies);
        foreach (var d in orderedDeadBodies) if (d) GameObject.Destroy(d.gameObject);
        meetingHud.SetMasksEnabled(true);
        DestroyableSingleton<HudManager>.Instance.MapButton.gameObject.SetActive(true);
        yield return Effects.Slide2D(meetingHud.meetingContents, new Vector2(0f, -10f), new Vector2(0f, 0f), 0.25f);
        yield return Effects.Wait(0.5f);
        yield return meetingHud.MeetingIntro.CoRun();
        meetingHud.SetMasksEnabled(false);
        meetingHud.TitleText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingWhoIsTitle);
        meetingHud.state = MeetingHud.VoteStates.Discussion;
        ControllerManager.Instance.OpenOverlayMenu(meetingHud.name, null, meetingHud.DefaultButtonSelected, meetingHud.ControllerSelectable, false);
        ConsoleJoystick.SetMode_Menu();
        yield break;
    }

    private static void ModMeetingIntroInit(MeetingIntroAnimation intro, NetworkedPlayerInfo reporter, DeadBody[] deadBodies)
    {
        var prefab = intro.VoteAreaPrefab;
        PlayerVoteArea GeneratePva(Transform parent, Vector3 localPos, NetworkedPlayerInfo? cosmetics, bool asReporter, int maskId)
        {
            PlayerVoteArea pva = GameObject.Instantiate<PlayerVoteArea>(intro.VoteAreaPrefab, parent);
            pva.transform.localPosition = localPos;
            pva.SetMaskLayer(maskId);
            if(cosmetics != null)pva.SetCosmetics(cosmetics);
            else
            {
                pva.Background.sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate("nameplate_NoPlate").Image;

                pva.PlayerIcon.UpdateFromPlayerOutfit(NebulaGameManager.Instance!.UnknownOutfit.outfit, PlayerMaterial.MaskType.ComplexUI, false, false);
                pva.PlayerIcon.ToggleName(false);
                pva.NameText.text = "???";
                pva.LevelNumberText.transform.parent.gameObject.SetActive(false);
            }
            pva.SetDead(asReporter, !asReporter, false);
            return pva;
        }
        PlayerVoteArea playerVoteArea = GeneratePva(intro.OverlayParent, intro.ReporterPos, reporter, true, 0);
        
        float num = intro.background.size.x / 2f;
        float num2 = intro.VoteAreaPrefab.MaskArea.bounds.size.y + 0.2f;
        float x = intro.VoteAreaPrefab.MaskArea.bounds.extents.x;
        int num3 = Mathf.CeilToInt((float)deadBodies.Length / 3f);
        for (int i = 0; i < deadBodies.Length; i += 3)
        {
            float num4 = (float)(num3 / 2 - i / 3) * num2;
            int num5 = Mathf.Min(deadBodies.Length - i, 3);
            for (int j = 0; j < num5; j++)
            {
                int num6 = i + j;
                float num7 = FloatRange.SpreadEvenly(-num - x, num + x, j, num5);
                PlayerVoteArea playerVoteArea2 = GeneratePva(intro.DeadParent, new Vector3(num7, num4, 0f), GameData.Instance.GetPlayerById(deadBodies[num6]?.ParentId ?? 255), false, num6);
                intro.deadCards.Add(playerVoteArea2);
            }
        }
        if (deadBodies.Length == 0)
        {
            intro.DeadBodiesText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NoDeadBodiesFound);
            intro.DeadBodiesText.transform.localPosition = Vector3.zero;
            intro.BloodSplat.enabled = false;
        }
        intro.ProtectedRecently.SetActive(false);

        //生死を再確認
        MeetingHud.Instance.ResetPlayerState();
    }

    internal static void ModStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, ReportType reportType)
    {
        //会議前の位置を共有する
        PlayerModInfo.RpcSharePreMeetingPoint.Invoke((PlayerControl.LocalPlayer.PlayerId, PlayerControl.LocalPlayer.transform.position));

        //ShipStatus.StartMeeting ここから
        ShipStatus.Instance.StartCoroutine(ModCoStartMeeting(reporter, deadBody, reportType).WrapToIl2Cpp());
        //ShipStatus.StartMeeting ここまで
        if (reporter.AmOwner)
        {
            if (reportType == ReportType.EmergencyMeeting)
            {
                reporter.RemainingEmergencies--;
                DataManager.Player.Stats.IncrementStat(StatID.EmergenciesCalled);
                return;
            }
            DataManager.Player.Stats.IncrementStat(StatID.BodiesReported);
        }
    }
}

