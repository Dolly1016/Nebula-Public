using UnityEngine.Rendering;
using Nebula.Behavior;
using static MeetingHud;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Modules.Cosmetics;

namespace Nebula.Patches;

public static class More15Helpers
{
    static private float[] VotingAreaScale = [1f, 0.95f, 0.76f];
    static private (int x, int y)[] VotingAreaSize = [(3, 5), (3, 6), (4, 6)];
    static private Vector3[] VotingAreaOffset = [Vector3.zero, new(0.1f, 0.145f, 0f), new(-0.355f, 0f, 0f)];
    static private (float x, float y)[] VotingAreaMultiplier = [(1f, 1f), (1f, 0.89f), (0.974f, 1f)];
    static public int GetDisplayType(int players) => players <= 15 ? 0 : players <= 18 ? 1 : 2;
    public static Vector3 ConvertPos(int index, int arrangeType, (int x, int y)[] arrangement, Vector3 origin, Vector3[] originOffset, Vector3 contentsOffset, float[] scale, (float x, float y)[] contentAreaMultiplier)
    {
        int x = index % arrangement[arrangeType].x;
        int y = index / arrangement[arrangeType].x;
        return
            origin + originOffset[arrangeType] +
            new Vector3(
                contentsOffset.x * scale[arrangeType] * contentAreaMultiplier[arrangeType].x * (float)x,
                contentsOffset.y * scale[arrangeType] * contentAreaMultiplier[arrangeType].y * (float)y,
                -(float)y * 0.01f);
    }

}

[NebulaRPCHolder]
public static class MeetingModRpc
{
    static private float[] VotingAreaScale = [1f, 0.95f, 0.76f];
    static private (int x, int y)[] VotingAreaSize = [(3, 5), (3, 6), (4, 6)];
    static private Vector3[] VotingAreaOffset = [new(0f,0f,-0.9f), new(0.1f, 0.145f, -0.9f), new(-0.355f, 0f, -0.9f)];
    static private (float x, float y)[] VotingAreaMultiplier = [(1f, 1f), (1f, 0.89f), (0.974f, 1f)];
    //static private int GetVotingAreaType(int players) => players <= 15 ? 0 : players <= 18 ? 1 : 2;
    private static Vector3 ToVoteAreaPos(int index, int arrangeType) => More15Helpers.ConvertPos(index, arrangeType, VotingAreaSize, MeetingHud.Instance.VoteOrigin, VotingAreaOffset, MeetingHud.Instance.VoteButtonOffsets, VotingAreaScale, VotingAreaMultiplier);

    public static void SortVotingArea(this MeetingHud __instance, Func<GamePlayer, int> rank, float speed = 1f)
    {
        int length = __instance.playerStates.Length;
        int type = More15Helpers.GetDisplayType(length);
        __instance.playerStates.Do(p => p.transform.localScale = new(VotingAreaScale[type], VotingAreaScale[type], 1f));
        
        var ordered = __instance.playerStates.OrderBy(p => p.TargetPlayerId + 32 * rank.Invoke(NebulaGameManager.Instance!.GetPlayer(p.TargetPlayerId)!)).ToArray();

        if (speed > 0f)
        {
            for (int i = 0; i < ordered.Length; i++)
                __instance.StartCoroutine(ordered[i].transform.Smooth(ToVoteAreaPos(i, type), 1.6f / speed).WrapToIl2Cpp());
        }
        else
        {
            for (int i = 0; i < ordered.Length; i++)
                ordered[i].transform.localPosition = ToVoteAreaPos(i, type);
        }
    }

    public static readonly RemoteProcess RpcBreakEmergencyButton = new("BreakEmergencyButton",
        (_) => ShipStatus.Instance.BreakEmergencyButton());

    public static readonly RemoteProcess<(int voteMask, bool canSkip, float votingTime, bool exileEvenIfTie, bool sort)> RpcChangeVotingStyle = new("ChangeVotingStyle",
        (message,_) =>
        {
            MeetingHudExtension.VotingMask = message.voteMask;
            MeetingHudExtension.CanSkip = message.canSkip;
            MeetingHudExtension.ExileEvenIfTie = message.exileEvenIfTie;

            if (message.sort)
            {
                MeetingHud.Instance.SortVotingArea(p =>
                {
                    if (((1 << p.PlayerId) & message.voteMask) != 0) return 0;
                    if (p.IsDead) return 2;
                    return 1;
                });
            }

            MeetingHud.Instance.state = VoteStates.NotVoted;
            MeetingHud.Instance.ResetPlayerState();

            MeetingHudExtension.VotingTimer = message.votingTime;
            MeetingHud.Instance.lastSecond = Mathf.Min(11, (int)message.votingTime);
        }
        );

    public static readonly RemoteProcess<float> RpcSyncMeetingTimer = new("SyncMeetingTimer", (timer, amHost) =>
    {
        if(!amHost)MeetingHudExtension.VotingTimer = timer;
    });
    public static readonly RemoteProcess<(byte reporter,byte reported)> RpcNoticeStartMeeting = new("ModStartMeeting",
    (message,_) =>
    {
        var reporter = NebulaGameManager.Instance?.GetPlayer(message.reporter);
        var reported = NebulaGameManager.Instance?.GetPlayer(message.reported);

        if (reported != null)
            GameOperatorManager.Instance?.Run(new ReportDeadBodyEvent(reporter!, reported), true);
        else
            GameOperatorManager.Instance?.Run(new CalledEmergencyMeetingEvent(reporter!), true);
    });

    public static readonly RemoteProcess<(List<VoterState> states, byte exiled, byte[] exiledAll,  bool tie, bool isObvious)> RpcModCompleteVoting = new("CompleteVoting", 
        (writer,message) => {
            writer.Write(message.states.Count);
            foreach(var state in message.states)
            {
                writer.Write(state.VoterId);
                writer.Write(state.VotedForId);
            }
            writer.Write(message.exiled);
            writer.WriteBytesAndSize(message.exiledAll);
            writer.Write(message.tie);
            writer.Write(message.isObvious);
        },
        (reader) => {
            List<VoterState> states = new();
            int statesNum = reader.ReadInt32();
            for (int i = 0; i < statesNum; i++)
            {
                var state = new VoterState() { VoterId = reader.ReadByte(), VotedForId = reader.ReadByte() };
                states.Add(state);
            }

            return (states,reader.ReadByte(),reader.ReadBytesAndSize(), reader.ReadBoolean(), reader.ReadBoolean());
        },
        (message, _) => {
            ForcelyVotingComplete(MeetingHud.Instance, message.states, message.exiled, message.exiledAll, message.tie, message.isObvious);
        }
        );

    private static SpriteLoader ExiledFrameSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingVCFrame.png", 119f);
    private static void ForcelyVotingComplete(MeetingHud meetingHud, List<VoterState> states, byte exiled, byte[] exiledAll, bool tie, bool isObvious)
    {
        //Debug.Log("Forcely Voting Complete");

        var readonlyStates = states.ToArray();

        var votedLocal = NebulaGameManager.Instance!.GetPlayer(((VoterState?)states.FirstOrDefault(s => s.VoterId == PlayerControl.LocalPlayer.PlayerId))?.VotedForId ?? 255);

        GameOperatorManager.Instance?.Run(new PlayerVoteDisclosedLocalEvent(GamePlayer.LocalPlayer, votedLocal, exiledAll.Contains(votedLocal?.PlayerId ?? byte.MaxValue)));
        GamePlayer[] votedBy = states.Where(s => s.VotedForId == PlayerControl.LocalPlayer.PlayerId).Select(s => s.VoterId).Distinct().Select(id => NebulaGameManager.Instance.GetPlayer(id)).Where(p => p != null).ToArray()!;
        GameOperatorManager.Instance?.Run(new PlayerVotedLocalEvent(GamePlayer.LocalPlayer, votedBy!));
        GameOperatorManager.Instance?.Run(new MeetingVoteDisclosedEvent(readonlyStates));

        meetingHud.exiledPlayer = Helpers.GetPlayer(exiled)?.Data;
        meetingHud.wasTie = tie;
        MeetingHudExtension.ExiledAll = exiledAll.Select(p => Helpers.GetPlayer(p)!).ToArray();
        MeetingHudExtension.ExiledAllModCache = MeetingHudExtension.ExiledAll.Select(p => p.GetModInfo()).Where(p => p != null).ToArray()!;
        MeetingHudExtension.WasTie = tie;
        MeetingHudExtension.IsObvious = isObvious;

        if (meetingHud.state == MeetingHud.VoteStates.Results) return;
        meetingHud.state = MeetingHud.VoteStates.Results;
        meetingHud.SkipVoteButton.gameObject.SetActive(false);
        meetingHud.SkippedVoting.gameObject.SetActive(MeetingHudExtension.CanSkip);
        AmongUsClient.Instance.DisconnectHandlers.Remove(meetingHud.TryCast<IDisconnectHandler>());
        meetingHud.PopulateResults(readonlyStates);
        meetingHud.SetupProceedButton();

        if (DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening)
        {
            DestroyableSingleton<HudManager>.Instance.Chat.ForceClosed();
            ControllerManager.Instance.CloseOverlayMenu(DestroyableSingleton<HudManager>.Instance.Chat.name);
        }
        ControllerManager.Instance.CloseOverlayMenu(meetingHud.name);
        ControllerManager.Instance.OpenOverlayMenu(meetingHud.name, null, meetingHud.ProceedButtonUi);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateButtons))]
class MeetingArrangeButtonStartPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        MeetingHud.Instance.SortVotingArea(p => p.IsDead ? 2 : 1, 10f);
    }

}
[HarmonyPatch(typeof(MeetingCalledAnimation), nameof(MeetingCalledAnimation.CoShow))]
class MeetingCalledAnimationPatch
{
    public static void Prefix(MeetingCalledAnimation __instance)
    {
        if(ClientOption.AllOptions[ClientOption.ClientOptionType.ForceSkeldMeetingSE].Value == 1)
        {
            bool isEmergency = __instance.Stinger == ShipStatus.Instance.EmergencyOverlay.Stinger;
            __instance.Stinger = (isEmergency ? VanillaAsset.MapAsset[0].EmergencyOverlay : VanillaAsset.MapAsset[0].ReportOverlay).Stinger;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
    {
        //会議室が開くか否かのチェック
        if (AmongUsClient.Instance.IsGameOver || MeetingHud.Instance) return false;
        
        //フェイクタスクでない緊急タスクがある場合ボタンは押せない
        if (target == null &&
            PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)
            (task => PlayerTask.TaskIsEmergency(task) && 
                (NebulaGameManager.Instance?.LocalFakeSabotage?.MyFakeTasks.All(
                    type => ShipStatus.Instance.GetSabotageTask(type)?.TaskType != task.TaskType) ?? true))) != null)
                return false;
            
        

        if (__instance.Data.IsDead) return false;
        MeetingRoomManager.Instance.AssignSelf(__instance, target);
        if (!AmongUsClient.Instance.AmHost) return false;
        
        HudManager.Instance.OpenMeetingRoom(__instance);
        __instance.RpcStartMeeting(target);

        MeetingModRpc.RpcNoticeStartMeeting.Invoke((__instance.PlayerId, target?.PlayerId ?? 255));

        if (target == null)
        {
            NebulaGameManager.Instance!.EmergencyCalls++;
            if (NebulaGameManager.Instance!.EmergencyCalls == GeneralConfigurations.NumOfMeetingsOption) MeetingModRpc.RpcBreakEmergencyButton.Invoke();
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
class StartMeetingPatch
{
    public static void Prefix()
    {
        //会議前の位置を共有する
        PlayerModInfo.RpcSharePreMeetingPoint.Invoke((PlayerControl.LocalPlayer.PlayerId, PlayerControl.LocalPlayer.transform.position));
    }
}


[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.canBeHighlighted))]
class MeetingCanBeHighlightedPatch
{
    public static void Postfix(PlayerVoteArea __instance,ref bool __result)
    {
        __result = __result && MeetingHudExtension.CanVoteFor(__instance.TargetPlayerId);
    }
}

[HarmonyPatch(typeof(MeetingIntroAnimation), nameof(MeetingIntroAnimation.CoRun))]
class MeetingIntroStartPatch
{
    public static void Postfix(MeetingIntroAnimation __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = Effects.Sequence(
            Effects.Action((Il2CppSystem.Action)(() =>
            {
                NebulaGameManager.Instance?.OnMeetingStart();
                if (MeetingHud.Instance) MeetingHudExtension.LastReporter = GamePlayer.GetPlayer(MeetingHud.Instance.reporterId);
            })),
            __result            
            );
    }
}


[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CoIntro))]
class MeetingIntroPatch
{
    public static void Postfix(MeetingHud __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var orig = __result;
        IEnumerator CoIntro()
        {
            while (orig.MoveNext())
            {
                var current = orig.Current;
                yield return current;
                if (current != null && current.TryCast<MeetingIntroAnimation._CoRun_d__17>() != null)
                {
                    NebulaGameManager.Instance?.OnMeetingStart();
                    if (MeetingHud.Instance) MeetingHudExtension.LastReporter = GamePlayer.GetPlayer(MeetingHud.Instance.reporterId);
                }
            }
        }
        __result = CoIntro().WrapToIl2Cpp();
    }
}


[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingStartPatch
{
    static private Image LightColorSprite = SpriteLoader.FromResource("Nebula.Resources.ColorLight.png", 100f);
    static private Image DarkColorSprite = SpriteLoader.FromResource("Nebula.Resources.ColorDark.png", 100f);

    static private bool CanVote => MeetingHud.Instance && MeetingHudExtension.VotingTimer > 0f && MeetingHud.Instance.state == VoteStates.NotVoted;
    static void Postfix(MeetingHud __instance)
    {
        ClientOption.ChangeAmbientVolumeIfNecessary(true, false);

        MeetingHudExtension.LeftContents.Clear();
        MeetingHudExtension.Reset();
        MeetingHudExtension.InitMeetingTimer();

        NebulaManager.Instance.CloseAllUI();

        __instance.transform.localPosition = new Vector3(0f, 0f, -25f);

        {
            var role = GamePlayer.LocalPlayer!.Role.Role;
            if (role != Roles.Crewmate.Crewmate.MyRole && role != Roles.Impostor.Impostor.MyRole)
            {
                Tutorial.WaitAndShowTutorial(() => !MeetingHud.Instance || MeetingHud.Instance.state == VoteStates.Animating,
                            new TutorialBuilder()
                            .BindHistory("helpKey")
                            .ShowWhile(() => MeetingHud.Instance)
                            .AsSimpleTitledTextWidget(Language.Translate("tutorial.variations.helpInGame.title"), Language.Translate("tutorial.variations.helpInGame.caption").ReplaceKeyCode("%KEY%", Virial.Compat.VirtualKeyInput.Help)));
            }
        }

        GamePlayer.AllPlayers.Do(p =>
        {
            p.Unbox().SpecialStampShower = PopupStampShower.GetHudShower(p.PlayerId, MeetingHud.Instance.transform, -100f, null);
        });
        NebulaManager.Instance.StartCoroutine(ManagedEffects.Wait(() => __instance.state == VoteStates.Animating, () =>
        {
            GamePlayer.AllPlayers.Do(p =>
            {
                var unbox = p.Unbox();
                var shower = unbox.SpecialStampShower;
                var area = __instance.playerStates.FirstOrDefault(area => area.TargetPlayerId == p.PlayerId);
                unbox.SpecialStampShower = new ConditionalStampShower(
                    PopupStampShower.GetMeetingShower(area),
                    shower,
                    () => area.gameObject.active
                    );
            });
        }));

        //色の明暗を表示
        foreach (var player in __instance.playerStates)
        {
            player.NameText.rectTransform.sizeDelta = new(2.0942f, 0.3879f);
            player.NameText.fontSizeMax = 2f;
            player.NameText.fontSizeMin = 1.5f;
            player.NameText.enableAutoSizing = true;

            bool isLightColor = DynamicPalette.IsLightColor(DynamicPalette.PlayerColors[player.TargetPlayerId]);

            SpriteRenderer renderer = UnityHelper.CreateObject<SpriteRenderer>("Color", player.transform, new Vector3(1.2f, -0.18f, -1f));
            renderer.sprite = isLightColor ? LightColorSprite.GetSprite() : DarkColorSprite.GetSprite();

            //色テキストをプレイヤーアイコンそばに移動
            var localPos = player.ColorBlindName.transform.localPosition;
            localPos.x = -0.947f;
            localPos.z -= 0.15f;
            player.ColorBlindName.transform.localPosition = localPos;

            var roleText = GameObject.Instantiate(player.NameText, player.transform);
            roleText.transform.localPosition = new Vector3(0.3384f, -0.13f, -0.02f);
            roleText.transform.localScale = new Vector3(0.57f, 0.57f);
            roleText.rectTransform.sizeDelta += new Vector2(0.35f, 0f);

            player.CancelButton.GetComponent<SpriteRenderer>().material = __instance.Glass.material;
            player.ConfirmButton.GetComponent<SpriteRenderer>().material = __instance.Glass.material;
            player.CancelButton.transform.GetChild(0).GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.None;
            player.ConfirmButton.transform.GetChild(0).GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.None;
            player.Flag.gameObject.SetActive(GeneralConfigurations.ShowVoteStateOption || player.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId);

            var button = player.PlayerButton.Cast<PassiveButton>();
            button.OnClick = new();
            button.OnMouseOver = new();
            button.OnMouseOut = new();
            button.OnClick.AddListener(() =>
            {
                if (player.canBeHighlighted() && CanVote)
                {
                    if (MeetingHudExtension.CanVote)
                        player.Select();
                    else
                        GameOperatorManager.Instance?.Run(new InvokeVoteAlternateEvent(player, GamePlayer.GetPlayer(player.TargetPlayerId)!));
                }
            });
            var modPlayer = GamePlayer.GetPlayer(player.TargetPlayerId)?.Unbox();
            button.OnMouseOver.AddListener(() =>
            {
                if (player.canBeHighlighted()) player.SetHighlighted(true);
            });
            button.OnMouseOut.AddListener(() =>
            {
                player.SetHighlighted(false);
            });

            var script = player.gameObject.AddComponent<ScriptBehaviour>();
            var cosmetics = player.PlayerIcon.cosmetics;
            var nebulaCosmetics = cosmetics.GetComponent<NebulaCosmeticsLayer>();
            var nameText = player.NameText;

            script.UpdateHandler += () => {
                try
                {
                    if (nameText) modPlayer.UpdateNameText(nameText, true);
                    if (roleText) modPlayer.UpdateRoleText(roleText, true);
                }
                catch
                {
                    if (roleText) roleText.gameObject.SetActive(false);
                }

                try
                {
                    cosmetics.hat.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    cosmetics.hat.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    cosmetics.visor.Image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    cosmetics.skin.layer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    cosmetics.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    nebulaCosmetics.AdditionalRenderers().Do(r => r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask);
                    player.XMark.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
                catch { }
            };
        }

        //会議開始時に死亡したプレイヤーを考慮したソート
        __instance.StartCoroutine(Effects.Sequence(Effects.Wait(2f), ManagedEffects.Action(() => MeetingHud.Instance.SortVotingArea(p => p.IsDead ? 2 : 1)).WrapToIl2Cpp()));
    }
}


[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
class MeetingHudUpdatePatch
{
    static bool Prefix(MeetingHud __instance)
    {
        if (__instance.state == MeetingHud.VoteStates.Animating) return false;
        
        __instance.UpdateButtons();

        if (MeetingHudExtension.ActionCoolDown > 0f) MeetingHudExtension.ActionCoolDown -= Time.deltaTime;

        switch (__instance.state)
        {
            case MeetingHud.VoteStates.Discussion:

                MeetingHudExtension.DiscussionTimer -= Time.deltaTime;
                if (MeetingHudExtension.DiscussionTimer > 0f)
                {
                    //議論時間中
                    __instance.UpdateTimerText(StringNames.MeetingVotingBegins, Mathf.CeilToInt(MeetingHudExtension.DiscussionTimer));
                    for (int i = 0; i < __instance.playerStates.Length; i++) __instance.playerStates[i].SetDisabled();
                    __instance.SkipVoteButton.SetDisabled();
                    return false;
                }

                //議論時間から投票時間へ
                __instance.state = MeetingHud.VoteStates.NotVoted;

                bool active = MeetingHudExtension.VotingTimer > 0;
                __instance.TimerText.gameObject.SetActive(active);

                __instance.discussionTimer = (float)GameManager.Instance.LogicOptions.CastFast<LogicOptionsNormal>().GetDiscussionTime();

                MeetingHud.Instance!.lastSecond = 11;

                MeetingHudExtension.ReflectVotingMask();

                return false;
            case MeetingHud.VoteStates.NotVoted:
            case MeetingHud.VoteStates.Voted:
                MeetingHudExtension.VotingTimer -= Time.deltaTime;
                if (MeetingHudExtension.VotingTimer > 0f)
                {
                    //投票時間中
                    int intCnt = Mathf.CeilToInt(MeetingHudExtension.VotingTimer);
                    __instance.UpdateTimerText(StringNames.MeetingVotingEnds, intCnt);
                    if (__instance.state == MeetingHud.VoteStates.NotVoted && intCnt < __instance.lastSecond)
                    {
                        __instance.lastSecond = intCnt;
                        __instance.StartCoroutine(Effects.PulseColor(__instance.TimerText, Color.red, Color.white, 0.25f));
                        SoundManager.Instance.PlaySound(__instance.VoteEndingSound, false, 1f, null).pitch = Mathf.Lerp(1.5f, 0.8f, (float)__instance.lastSecond / 10f);
                    }

                    //定期的に時間を同期させる。投票漏れを防ぐためにわずかに時間を短く見積もる。
                    if (MeetingHudExtension.LastSharedCount > MeetingHudExtension.VotingTimer && AmongUsClient.Instance.AmHost && intCnt % 20 == 11)
                    {
                        MeetingModRpc.RpcSyncMeetingTimer.Invoke(MeetingHudExtension.VotingTimer - 0.05f);
                    }
                }
                else
                {
                    __instance.TimerText.text = Language.Translate("options.meeting.waitingForHost");

                    if (AmongUsClient.Instance.AmHost)
                    {
                        //結果開示へ (ForceSkipAll)
                        __instance.playerStates.Do(state => { if (!state.DidVote) state.VotedFor = 254; });
                        __instance.SetDirtyBit(1U);
                        __instance.CheckForEndVoting();
                    }
                }
                break;
            case MeetingHud.VoteStates.Results:
                if (AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
                {
                    MeetingHudExtension.ResultTimer -= Time.deltaTime;
                    __instance.UpdateTimerText(StringNames.MeetingProceeds, Mathf.CeilToInt(MeetingHudExtension.ResultTimer));
                    if (AmongUsClient.Instance.AmHost && MeetingHudExtension.ResultTimer <= 0f) __instance.HandleProceed();
                }
                break;
        }

        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
class MeetingClosePatch
{ 
    public static void Postfix(MeetingHud __instance)
    {
        //ベント内のプレイヤー情報をリセットしておく
        VentilationSystem? ventilationSystem = ShipStatus.Instance.Systems[SystemTypes.Ventilation].TryCast<VentilationSystem>();
        if (ventilationSystem != null) ventilationSystem.PlayersInsideVents.Clear();

        GameOperatorManager.Instance?.Run(new ExileScenePreStartEvent(MeetingHudExtension.ExiledAllModCache!));

        NebulaManager.Instance.CloseAllUI();

        ClientOption.ChangeAmbientVolumeIfNecessary(false, true);
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetMaskLayer))]
class VoteMaskPatch
{
    public static bool Prefix(PlayerVoteArea __instance)
    {
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateButtons))]
class VoteAreaVCPatch
{
    private static SpriteLoader VCFrameSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingVCFrame.png", 119f);
    public static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (GeneralConfigurations.UseVoiceChatOption)
            {
                foreach (var pva in __instance.playerStates)
                {
                    var frame = UnityHelper.CreateObject<SpriteRenderer>("VCFrame", pva.transform, new Vector3(0, 0, -0.5f));
                    frame.sprite = VCFrameSprite.GetSprite();
                    frame.color = Color.clear;
                    var col = DynamicPalette.PlayerColors[pva.TargetPlayerId];
                    if (Mathf.Max((int)col.r, (int)col.g, (int)col.b) < 100) col = Color.Lerp(col, Color.white, 0.4f);

                    var client = NebulaGameManager.Instance?.VoiceChatManager?.GetClient(pva.TargetPlayerId);
                    float alpha = 0f;
                    if (client != null)
                    {
                        var script = frame.gameObject.AddComponent<ScriptBehaviour>();
                        script.UpdateHandler += () =>
                        {
                            if (client.IsSpeaking)
                                alpha = Mathf.Clamp(alpha + Time.deltaTime * 8f, 0f, 1f);
                            else
                                alpha = Mathf.Clamp(alpha - Time.deltaTime * 8f, 0f, 1f);
                            col.a = alpha;
                            frame.color = col;
                        };
                    }
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Start))]
class VoteAreaPatch
{
    public static void Postfix(PlayerVoteArea __instance)
    {
        if (!MeetingHud.Instance) return;

        if(__instance.MaskArea != null) { 
        
            var maskParent = UnityHelper.CreateObject<SortingGroup>("MaskedObjects", __instance.transform, new Vector3(0, 0, -0.1f));
            __instance.MaskArea.transform.SetParent(maskParent.transform);
            __instance.PlayerIcon.transform.SetParent(maskParent.transform);
            __instance.Overlay.maskInteraction = SpriteMaskInteraction.None;
            __instance.Overlay.material = __instance.Megaphone.material;

            var mask = __instance.MaskArea.gameObject.AddComponent<SpriteMask>();
            mask.sprite = __instance.MaskArea.sprite;
            mask.transform.localScale = __instance.MaskArea.size;
            __instance.MaskArea.enabled = false;

            __instance.Background.material = __instance.Megaphone.material;

            __instance.PlayerIcon.cosmetics.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            __instance.PlayerIcon.cosmetics.hat.FrontLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.hat.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.PlayerIcon.cosmetics.hat.BackLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.hat.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.PlayerIcon.cosmetics.visor.Image.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.visor.Image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.PlayerIcon.cosmetics.skin.layer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.skin.layer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.PlayerIcon.cosmetics.currentBodySprite.BodySprite.gameObject.AddComponent<ZOrderedSortingGroup>();
            var nebulaLayer = __instance.PlayerIcon.cosmetics.GetComponent<NebulaCosmeticsLayer>();
            nebulaLayer.AdditionalRenderers().Do(r =>
            {
                r.gameObject.AddComponent<ZOrderedSortingGroup>().SetConsiderParentsTo(nebulaLayer.transform);
                r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            });
        }
    }
}



[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Confirm))]
class CastVotePatch
{
    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte suspectStateIdx)
    {
        if (PlayerControl.LocalPlayer.Data.IsDead) return false;

        foreach (var state in __instance.playerStates)
        {
            state.ClearButtons();
            state.voteComplete = true;
        }

        __instance.SkipVoteButton.ClearButtons();
        __instance.SkipVoteButton.voteComplete = true;
        __instance.SkipVoteButton.gameObject.SetActive(false);

        if (__instance.state != MeetingHud.VoteStates.NotVoted) return false;
        
        __instance.state = MeetingHud.VoteStates.Voted;

        //CmdCastVote(Mod)
        int vote = GameOperatorManager.Instance?.Run(new PlayerVoteCastLocalEvent(GamePlayer.LocalPlayer, NebulaGameManager.Instance!.GetPlayer(suspectStateIdx), 1)).Vote ?? 1;
        __instance.ModCastVote(PlayerControl.LocalPlayer.PlayerId, suspectStateIdx, vote);
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
static class CheckForEndVotingPatch
{
    public static void AddValue(this Dictionary<byte,int> self, byte target,int num)
    {
        if (self.TryGetValue(target, out var last))
            self[target] = last + num;
        else
            self[target] = num;
    }

    public static Dictionary<byte, int> ModCalculateVotes(MeetingHud __instance)
    {
        Dictionary<byte, int> dictionary = new();

        List<string> log = new();
        for (int i = 0; i < __instance.playerStates.Length; i++)
        {
            PlayerVoteArea playerVoteArea = __instance.playerStates[i];
            var player = NebulaGameManager.Instance?.GetPlayer(playerVoteArea.TargetPlayerId);
            if (player?.IsDead ?? true) continue;

            bool didVote = playerVoteArea.VotedFor != 252 && playerVoteArea.VotedFor != 255 && playerVoteArea.VotedFor != 254;
            if (!MeetingHudExtension.WeightMap.TryGetValue((byte)playerVoteArea.TargetPlayerId, out var vote)) vote = 1;
            var ev = GameOperatorManager.Instance!.Run(new PlayerFixVoteHostEvent(player, didVote, NebulaGameManager.Instance?.GetPlayer(playerVoteArea.VotedFor), vote));

            if (ev.DidVote)
            {
                dictionary.AddValue(ev.VoteTo?.PlayerId ?? PlayerVoteArea.SkippedVote, ev.Vote);
                playerVoteArea.VotedFor = ev.VoteTo?.PlayerId ?? PlayerVoteArea.SkippedVote;
                MeetingHudExtension.WeightMap[player.PlayerId] = ev.Vote;
            }
            else
            {
                playerVoteArea.VotedFor = PlayerVoteArea.MissedVote;
            }
        }


        return dictionary;
    }

    public static KeyValuePair<byte, int> MaxPair(this Dictionary<byte, int> self, out bool tie)
    {
        tie = true;
        KeyValuePair<byte, int> result = new KeyValuePair<byte, int>(PlayerVoteArea.SkippedVote, 0);
        foreach (KeyValuePair<byte, int> keyValuePair in self)
        {
            if (keyValuePair.Value > result.Value)
            {
                result = keyValuePair;
                tie = false;
            }
            else if (keyValuePair.Value == result.Value)
            {
                tie = true;
            }
        }
        return result;
    }

    public static bool Prefix(MeetingHud __instance)
    {
        //投票結果が自明な場合、早回しで終わらせる。
        {
            int canVoteTo = NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && (MeetingHudExtension.VotingMask & (1 << p.PlayerId)) != 0);
            if (MeetingHudExtension.CanSkip) canVoteTo++;
            
            //選択肢が1つ以下の場合、結果は自明
            if (canVoteTo <= 1) 
            {
                var exiled = NebulaGameManager.Instance!.AllPlayerInfo.FirstOrDefault(p => !p.IsDead && (MeetingHudExtension.VotingMask & (1 << p.PlayerId)) != 0);

                if (exiled == null)
                    MeetingModRpc.RpcModCompleteVoting.Invoke(([], byte.MaxValue, [], false, true));
                else
                {
                    MeetingModRpc.RpcModCompleteVoting.Invoke(([], exiled!.PlayerId, [exiled.PlayerId], false, true));
                }

                return false;
            }
        }

        //投票が済んでない場合、なにもしない
        if (!__instance.playerStates.All((PlayerVoteArea ps) => ps.AmDead || ps.DidVote)) return false;

        {
            Dictionary<byte, int> dictionary = ModCalculateVotes(__instance);
            KeyValuePair<byte, int> max = dictionary.MaxPair(out bool tie);

            List<byte> extraVotes = new();

            if (tie)
            {
                Dictionary<byte, GamePlayer?> voteForMap = new();

                foreach (var state in __instance.playerStates)
                {
                    if (!state.DidVote) continue;
                    voteForMap[state.TargetPlayerId] = NebulaGameManager.Instance?.GetPlayer(state.VotedFor);
                }

                foreach (var target in GameOperatorManager.Instance?.Run(new MeetingTieVoteHostEvent(voteForMap))?.ExtraVotes ?? [])
                {
                    dictionary.AddValue(target?.PlayerId ?? 253, 1);
                    extraVotes.Add(target?.PlayerId ?? 253);
                }

                //再計算する
                max = dictionary.MaxPair(out tie);
            }


            NetworkedPlayerInfo exiled = null!;
            NetworkedPlayerInfo[] exiledAll = new NetworkedPlayerInfo[0];

            if (MeetingHudExtension.ExileEvenIfTie) tie = false;
            try
            {
                if (!tie)
                {
                    //投票対象で最高票を獲得しているプレイヤー全員
                    var exiledPlayers = GameData.Instance.AllPlayers.ToArray().Where(v => !v.IsDead && dictionary.GetValueOrDefault(v.PlayerId) == max.Value && ((MeetingHudExtension.VotingMask & (1 << v.PlayerId)) != 0)).ToArray();
                    exiled = exiledPlayers.First();
                    if (exiledPlayers.Length > 0) exiledAll = exiledPlayers.ToArray();
                }
            }
            catch { }
            List<MeetingHud.VoterState> allStates = new();

            //記名投票分
            foreach (var state in __instance.playerStates)
            {
                if (!state.DidVote) continue;

                if (!MeetingHudExtension.WeightMap.TryGetValue((byte)state.TargetPlayerId, out var vote)) vote = 1;

                for (int i = 0; i < vote; i++)
                {
                    allStates.Add(new MeetingHud.VoterState
                    {
                        VoterId = state.TargetPlayerId,
                        VotedForId = state.VotedFor
                    });
                }
            }

            //追加投票分
            foreach(var votedFor in extraVotes)
            {
                allStates.Add(new MeetingHud.VoterState
                {
                    VoterId = byte.MaxValue,
                    VotedForId = votedFor
                });
            }

            //Debug.Log($"Exiled: ({string.Join(',', (exiledAll ?? []).Select(b => b.ToString()))})");
            MeetingModRpc.RpcModCompleteVoting.Invoke((allStates, exiled?.PlayerId ?? byte.MaxValue, exiledAll?.Select(e => e.PlayerId).ToArray() ?? [], tie, false));
            //サーバー用、プレイヤーは全員このメッセージを無視する
            //__instance.RpcVotingComplete(allStates.ToArray(), Helpers.GetPlayer(exiled?.PlayerId)?.Data, tie);

        }

        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
class CancelVotingCompleteByRPCPatch
{
    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte callId)
    {
        if (callId == 23)
        {
            Debug.Log($"Canceled VotingComplete on HandleRpc");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
class PopulateResultPatch
{
    private static void ModBloopAVoteIcon(MeetingHud __instance,NetworkedPlayerInfo? voterPlayer, int index, Transform parent,bool isExtra)
    {
        SpriteRenderer spriteRenderer = GameObject.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
        if ((GameManager.Instance.LogicOptions.GetAnonymousVotes() && !(NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) || voterPlayer == null)
            PlayerMaterial.SetColors(Palette.DisabledGrey, spriteRenderer);
        else
            PlayerMaterial.SetColors(voterPlayer.DefaultOutfit.ColorId, spriteRenderer);
        
        spriteRenderer.transform.SetParent(parent);
        spriteRenderer.transform.localScale = Vector3.zero;
        __instance.StartCoroutine(Effects.Bloop((float)index * 0.3f + (isExtra ? 0.85f : 0f), spriteRenderer.transform, 1f, isExtra ? 0.5f : 0.7f));

        if (isExtra)
            __instance.StartCoroutine(Effects.Sequence(Effects.Wait((float)index * 0.3f + 0.85f), ManagedEffects.Action(() => parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer)).WrapToIl2Cpp()));
        else
            parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer);
    }


    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)]Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<MeetingHud.VoterState> states)
    {
        Debug.Log("Called PopulateResults");

        GameOperatorManager.Instance?.Run(new MeetingVoteEndEvent());

        __instance.TitleText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingVotingResults);
        foreach (var voteArea in __instance.playerStates)
        {
            voteArea.ClearForResults();
            MeetingHudExtension.LastVotedForMap[voteArea.TargetPlayerId]= voteArea.VotedFor;
        }

        int lastVoteFor = -1;
        int num = 0;
        Transform? voteFor = null;

        //OrderByは安定ソート
        foreach (var state in states.OrderBy(s => s.VotedForId)){
            if (state.VoterId == byte.MaxValue - 1) continue;
            if(state.VotedForId != lastVoteFor)
            {
                lastVoteFor = state.VotedForId;
                num = 0;
                if (state.SkippedVote)
                    voteFor = __instance.SkippedVoting.transform;
                else
                    voteFor = __instance.playerStates.FirstOrDefault((area) => area.TargetPlayerId == lastVoteFor)?.transform ?? null;
            }

            if (voteFor != null)
            {
                NetworkedPlayerInfo? playerById = GameData.Instance.GetPlayerById(state.VoterId);

                ModBloopAVoteIcon(__instance, playerById, num, voteFor, state.VoterId == byte.MaxValue);
                num++;
            }
        }

        return false;
    }
}


//死体の拾い漏れチェック
[HarmonyPatch(typeof(MeetingIntroAnimation), nameof(MeetingIntroAnimation.Init))]
class MeetingIntroAnimationPatch
{
    public static void Prefix(MeetingIntroAnimation __instance, [HarmonyArgument(1)] ref Il2CppReferenceArray<NetworkedPlayerInfo> deadBodies)
    {
        List<NetworkedPlayerInfo> dBodies = new List<NetworkedPlayerInfo>();
        //既に発見されている死体
        foreach (var dBody in deadBodies) dBodies.Add(dBody);
        
        //遅れて発見された死体
        foreach (var dBody in Helpers.AllDeadBodies())
        {
            dBodies.Add(GameData.Instance.GetPlayerById(dBody.ParentId));
            GameObject.Destroy(dBody.gameObject);
        }
        deadBodies = new Il2CppReferenceArray<NetworkedPlayerInfo>(dBodies.OrderBy(d => d.PlayerId).ToArray());

        //生死を再確認
        MeetingHud.Instance.ResetPlayerState();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Deserialize))]
class MeetingDeserializePatch
{
    public static void Postfix(MeetingHud __instance)
    {
        if (__instance.CurrentState is VoteStates.Animating or VoteStates.Discussion) return;
        __instance.UpdatePlayerState();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
class MeetingDestroyPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        ClientOption.ChangeAmbientVolumeIfNecessary(false, true);
    }
}