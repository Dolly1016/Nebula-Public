using AmongUs.GameOptions;
using Assets.CoreScripts;
using Il2CppSystem.Net.NetworkInformation;
using Nebula.Behaviour;
using Nebula.Configuration;
using Nebula.Events;
using Nebula.Modules;
using Nebula.Player;
using Nebula.Roles;
using Nebula.Roles.Assignment;
using Nebula.Roles.Crewmate;
using Nebula.VoiceChat;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking.Types;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Virial;
using Virial.Assignable;
using Virial.Events.Meeting;
using Virial.Game;
using static Rewired.UI.ControlMapper.ControlMapper;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Game;

public enum NebulaGameStates
{
    NotStarted,
    Initialized,
    WaitGameResult,
    Finished
}

public class NebulaEndState {
    static public NebulaEndState? CurrentEndState => NebulaGameManager.Instance?.EndState;

    public int WinnersMask { get; init; }
    public byte ConditionId { get; init; }
    public ulong ExtraWinMask { get; init; }
    public GameEndReason EndReason { get; init; }
    public CustomEndCondition? EndCondition => CustomEndCondition.GetEndCondition(ConditionId);
    public IEnumerable<CustomExtraWin> ExtraWins => CustomExtraWin.AllExtraWins.Where(e => ((ulong)(e.ExtraWinMask) & ExtraWinMask) != 0ul);
    public bool CheckWin(byte playerId) => ((1 << playerId) & WinnersMask) != 0;
    public NebulaEndState(byte conditionId, int winnersMask,ulong extraWinMask, GameEndReason reason)
    {
        WinnersMask = winnersMask;
        ConditionId = conditionId;
        ExtraWinMask = extraWinMask;
        EndReason = reason;
    }
}

public class RuntimeGameAsset
{
    AsyncOperationHandle<GameObject>? handle = null;
    public MapBehaviour MinimapPrefab = null!;
    public float MapScale;
    public GameObject MinimapObjPrefab => MinimapPrefab.transform.GetChild(1).gameObject;
    public void SetHandle(AsyncOperationHandle<GameObject> handle) => this.handle = handle;
    public void Abandon()
    {
        if (handle?.IsValid() ?? false) handle.Release();
    }
}

public record RoleHistory
{
    public float Time;
    public byte PlayerId;
    public bool IsModifier;
    public bool IsSet;
    public AssignableInstance Assignable;

    public RoleHistory(byte playerId, ModifierInstance modifier, bool isSet)
    {
        Time = NebulaGameManager.Instance!.CurrentTime;
        PlayerId = playerId;
        IsModifier = true;
        IsSet = isSet;
        Assignable = modifier;
    }

    public RoleHistory(byte playerId, RoleInstance role)
    {
        Time = NebulaGameManager.Instance!.CurrentTime;
        PlayerId = playerId;
        IsModifier = false;
        IsSet = true;
        Assignable = role;
    }
}

public static class RoleHistoryHelper { 
    static public IEnumerable<T> EachMoment<T>(this List<RoleHistory> history, Predicate<RoleHistory> predicate, Func<RoleInstance, List<AssignableInstance>, T> converter)
    {
        RoleInstance? role = null;
        List<AssignableInstance> modifiers = new();

        float lastTime = history[0].Time;
        foreach(var h in history.Append(null))
        {
            if (h != null && !predicate(h)) continue;

            if(h == null || lastTime + 1f < h.Time)
            {
                if(role != null) yield return converter.Invoke(role, modifiers);

                if (h == null) break;

                lastTime = h.Time;
            }

            if (!h.IsModifier && h.Assignable is RoleInstance ri) role = ri;
            else if (h.IsSet) modifiers.Add(h.Assignable);
            else modifiers.Remove(h.Assignable);
        }
    }

    static public string ConvertToRoleName(RoleInstance role, List<AssignableInstance> modifier, bool isShort)
    {
        string result = isShort ? role.Role.ShortName : role.Role.DisplayName;
        foreach (var m in modifier)
        {
            var newName = m.OverrideRoleName(result, isShort);
            if(newName != null) result = newName;
        }
        Color color = role.Role.RoleColor;
        foreach (var m in modifier) m.DecoratePlayerName(ref result, ref color);
        foreach (var m in modifier) m.DecorateRoleName(ref result);
        return result.Replace(" ","").Color(color);
    }
}

[NebulaRPCHolder]
public class NebulaGameManager : IRuntimePropertyHolder, Virial.Game.Game, Virial.Game.HUD
{
    static private NebulaGameManager? instance = null;
    static public NebulaGameManager? Instance { get => instance; }

    private Dictionary<byte, PlayerModInfo> allModPlayers;

    public List<AchievementTokenBase> AllAchievementTokens = new();
    public T? GetAchievementToken<T>(string achievement) where T : AchievementTokenBase {
        return AllAchievementTokens.FirstOrDefault(a=>a.Achievement.Id == achievement) as T;
    }


    //ゲーム開始時からの経過時間
    public float CurrentTime { get; private set; } = 0f;

    //各種進行状況
    public NebulaGameStates GameState { get; private set; } = NebulaGameStates.NotStarted;
    public NebulaEndState? EndState { get; set; } = null;

    //ゲーム内アセット
    public RuntimeGameAsset RuntimeAsset { get; private init; }

    //エンティティマネージャ
    public GameEntityManager GameEntityManager { get; init; } = new();

    //各種モジュール
    public HudGrid HudGrid { get; private set; }
    public GameStatistics GameStatistics { get; private set; } = new();
    public CriteriaManager CriteriaManager { get; private set; } = new();
    public Synchronizer Syncronizer { get; private set; } = new();
    public LobbySlideManager LobbySlideManager { get; private set; } = new();
    public VoiceChatManager? VoiceChatManager { get; set; } = GeneralConfigurations.UseVoiceChatOption ? new() : null;
    public ConsoleRestriction ConsoleRestriction { get; private set; } = new();
    public AttributeShower AttributeShower { get; private set; } = new();
    public RPCScheduler Scheduler { get; private set; } = new();
    public FakeSabotageStatus? LocalFakeSabotage => PlayerControl.LocalPlayer.GetModInfo()?.FakeSabotage;
    public MeetingPlayerButtonManager MeetingPlayerButtonManager { get; private set; } = null!;
    internal MeetingOverlayHolder MeetingOverlay { get; private init; }
    public bool IgnoreWalls => LocalPlayerInfo?.Role?.EyesightIgnoreWalls ?? false;
    public Dictionary<byte, AbstractAchievement?> TitleMap = new();

    private PlayerModInfo? localInfoCache = null;
    
    public PlayerModInfo LocalPlayerInfo { get
        {
            if (localInfoCache == null) localInfoCache = GetModPlayerInfo(PlayerControl.LocalPlayer.PlayerId);
            return localInfoCache!;
        } }

    public WideCamera WideCamera { get; init; } = new();

    //自身のキルボタン用トラッカー
    private ObjectTracker<PlayerControl> KillButtonTracker = null!;
    public int EmergencyCalls = 0;

    //天界視点フラグ
    public bool CanSeeAllInfo { get; set; }

    //ゲーム内履歴
    public List<RoleHistory> RoleHistory = new();

    static private SpriteLoader vcConnectSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.VCReconnectButton.png", 100f);
    public NebulaGameManager()
    {
        allModPlayers = new Dictionary<byte, PlayerModInfo>();
        instance = this;
        HudGrid = HudManager.Instance.gameObject.AddComponent<HudGrid>();
        RuntimeAsset = new();

        var vcConnectButton = new ModAbilityButton(true);
        vcConnectButton.Visibility = (_) => VoiceChatManager != null && GameState == NebulaGameStates.NotStarted;
        vcConnectButton.Availability = (_) =>true;
        vcConnectButton.SetSprite(vcConnectSprite.GetSprite());
        vcConnectButton.OnClick = (_) => VoiceChatManager!.Rejoin();
        vcConnectButton.SetLabel("rejoin");

        MeetingOverlay = new MeetingOverlayHolder().Register(this);
    }


    public void Abandon()
    {
        RuntimeAsset.Abandon();
        LobbySlideManager.Abandon();
        GameEntityManager.Abandon();

        instance = null;
    }

    public void OnSceneChanged()
    {
        VoiceChatManager?.Dispose();
        VoiceChatManager = null;
    }

    public void OnTerminal()
    {
        VoiceChatManager?.Dispose();
        VoiceChatManager = null;
    }

    void Virial.Game.Game.RegisterEntity(IGameEntity entity, ILifespan lifespan) => GameEntityManager.Register(entity, lifespan);
    

    public PlayerModInfo RegisterPlayer(PlayerControl player)
    {
        if(allModPlayers.ContainsKey(player.PlayerId))return allModPlayers[player.PlayerId];

        Debug.Log("Registered: " + player.name);
        var info = new PlayerModInfo(player);
        allModPlayers.Add(player.PlayerId, info);
        return info;
    }

    public void RpcPreSpawn(byte playerId,Vector2 spawnPos)
    {
        CombinedRemoteProcess.CombinedRPC.Invoke(
            GameStatistics.RpcPoolPosition.GetInvoker(new(GameStatisticsGatherTag.Spawn, playerId, spawnPos)),
            Modules.Synchronizer.RpcSync.GetInvoker(new(SynchronizeTag.PreSpawnMinigame, PlayerControl.LocalPlayer.PlayerId))
            );
    }

    private void CheckAndEndGame(CustomEndCondition? endCondition, GameEndReason endReason, int winnersMask = 0)
    {
        if(endCondition == null) return;
        if (GameState != NebulaGameStates.Initialized) return;

        if(endCondition == NebulaGameEnd.SabotageWin)
        {
            endCondition = NebulaGameEnd.ImpostorWin;
            endReason = GameEndReason.Sabotage;
        }

        CustomEndCondition finallyCondition = endCondition!;
        GameEndReason finallyReason = endReason;

        //乗っ取り勝利の判定
        GameEntityManager.AllEntities.Do(e => {
            var tuple = e.OnCheckGameEnd(endCondition, endReason);
            if(tuple != null && tuple.Value.end.Priority >= finallyCondition.Priority)
            {
                finallyCondition = tuple.Value.end.Unbox();
                finallyReason = tuple.Value.reason;
            }
        });

        endCondition = finallyCondition;
        endReason = finallyReason;

        int extraMask = 0;
        ulong extraWinMask = 0;

        //勝利判定
        foreach (var p in allModPlayers) {
            (bool canWin, bool blockWin) val = p.Value.AllAssigned().Aggregate((false, false), (val, a) => (val.Item1 | a.CheckWins(endCondition, ref extraWinMask), val.Item2 | a.BlockWins(endCondition)));
            if (val.canWin && !val.blockWin) winnersMask |= (1 << p.Value.PlayerId);
        }

        //追加勝利の判定
        for(int phase = 0; phase < (int)ExtraWinCheckPhase.PhaseMax; phase++)
        {
            foreach (var p in allModPlayers) if (p.Value.AllAssigned().Any(a => a.CheckExtraWins(endCondition, (ExtraWinCheckPhase)phase, winnersMask, ref extraWinMask))) extraMask |= (1 << p.Value.PlayerId);
            winnersMask |= extraMask;
        }
        
        

        NebulaGameEnd.RpcSendGameEnd(endCondition!, winnersMask, extraWinMask,endReason);
    }

    public void OnTaskUpdated(PlayerModInfo player)
    {
        CheckAndEndGame(CriteriaManager.OnTaskUpdated(), GameEndReason.Task);
        AllEntitiesAction(e => e.OnTaskUpdated(player));
    }

    public void OnMeetingStart()
    {

        if (PlayerControl.LocalPlayer.Data.IsDead) CanSeeAllInfo = true;

        foreach (var p in allModPlayers) p.Value.OnMeetingStart();

        AllEntitiesAction(e=>e.OnMeetingStart());

        Scheduler.Execute(RPCScheduler.RPCTrigger.PreMeeting);

        EventManager.HandleEvent(new MeetingStartEvent());
    }

    public void OnMeetingEnd(PlayerControl? player)
    {
        if (PlayerControl.LocalPlayer.Data.IsDead) CanSeeAllInfo = true;

        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) p.onLadder = false;

        ConsoleRestriction?.OnMeetingEnd();
        Scheduler.Execute(RPCScheduler.RPCTrigger.AfterMeeting);

        var tuple = CriteriaManager.OnExiled(player);
        if(tuple == null) return;
        CheckAndEndGame(tuple.Item1, GameEndReason.Situation, tuple.Item2);
    }

    public void OnUpdate() {
        CurrentTime += Time.deltaTime;

        WideCamera.Update();
        GameEntityManager.Update();

        if (VoiceChatManager == null && GeneralConfigurations.UseVoiceChatOption) VoiceChatManager = new();
        VoiceChatManager?.Update();

        AllEntitiesAction(e => e.HudUpdate());

        if (!PlayerControl.LocalPlayer) return;
        //バニラボタンの更新
        var localModInfo = PlayerControl.LocalPlayer.GetModInfo();
        if (localModInfo != null)
        {
            localModInfo.AssignableAction(r => r.LocalHudUpdate());

            //ベントボタン
            var ventTimer = PlayerControl.LocalPlayer.inVent ? localModInfo.Role?.VentDuration : localModInfo.Role?.VentCoolDown;
            string ventText = "";
            float ventPercentage = 0f;
            if (ventTimer != null && ventTimer.IsInProcess)
            {
                ventText = Mathf.CeilToInt(ventTimer.CurrentTime).ToString();
                ventPercentage = ventTimer.Percentage;
            }
            if (ventTimer != null && !ventTimer.IsInProcess && PlayerControl.LocalPlayer.inVent)
            {
                Vent.currentVent.SetButtons(false);
                PlayerControl.LocalPlayer.MyPhysics.RpcExitVent(Vent.currentVent!.Id);
            }
            HudManager.Instance.ImpostorVentButton.SetCooldownFill(ventPercentage);
            CooldownHelpers.SetCooldownNormalizedUvs(HudManager.Instance.ImpostorVentButton.graphic);
            HudManager.Instance.ImpostorVentButton.cooldownTimerText.text = ventText;
            HudManager.Instance.ImpostorVentButton.cooldownTimerText.color = PlayerControl.LocalPlayer.inVent ? Color.green : Color.white;

            //サボタージュボタン


            //ローカルモジュール
            AttributeShower.Update(localModInfo);
        }

        if(!ExileController.Instance || Minigame.Instance) CheckAndEndGame(CriteriaManager.OnUpdate(), GameEndReason.Situation);
    }

    public void OnFixedUpdate() {
        AllEntitiesAction(e => e.Update());

        if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && HudManager.Instance.KillButton.gameObject.active)
        {
            KillButtonTracker ??= ObjectTrackers.ForPlayer(null, PlayerControl.LocalPlayer, (p) => p.PlayerId != PlayerControl.LocalPlayer.PlayerId && !p.Data.IsDead && !p.Data.Role.IsImpostor, Roles.Impostor.Impostor.MyRole.CanKillHidingPlayerOption);
            (KillButtonTracker as IGameEntity).HudUpdate();
            HudManager.Instance.KillButton.SetTarget(KillButtonTracker.CurrentTarget);
        }

    }
    public void OnGameStart()
    {
        WideCamera.OnGameStart();

        //会議ボタンマネジャーを登録
        this.MeetingPlayerButtonManager = new();
        this.MeetingPlayerButtonManager.Register(this);

        //マップの取得
        RuntimeAsset.MinimapPrefab = ShipStatus.Instance.MapPrefab;
        RuntimeAsset.MinimapPrefab.gameObject.MarkDontUnload();
        RuntimeAsset.MapScale = ShipStatus.Instance.MapScale;

        //VC
        VoiceChatManager?.OnGameStart();

        //ゲームモードによる終了条件を追加
        foreach(var c in GeneralConfigurations.CurrentGameMode.GameModeCriteria)CriteriaManager.AddCriteria(c);

        foreach (var p in allModPlayers) p.Value.OnGameStart();
        GameEntityManager.AllEntities.Do(e => e.OnGameStart());
        HudManager.Instance.UpdateHudContent();

        ConsoleRestriction?.OnGameStart();

        //実績
        new AchievementToken<int>("challenge.death2", 0, (exileAnyone, _) => (NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.IsDead && p.MyKiller == LocalPlayerInfo && p != LocalPlayerInfo).Select(p => p.MyState).Distinct().Count() + exileAnyone) >= 4);
        new AchievementToken<int>("graduation2", 0, (exileAnyone, _) => exileAnyone >= 3 && Helpers.CurrentMonth == 3);
    }

    public void OnGameEnd()
    {
        GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.GameEnd, null, 0) { RelatedTag = EventDetail.GameEnd });

        //実績

        //自身が勝利している場合
        if (EndState!.CheckWin(PlayerControl.LocalPlayer.PlayerId)) {
            //生存しているマッドメイト除くクルー陣営
            var aliveCrewmate = allModPlayers.Values.Where(p => !p.IsDead && p.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole && p.Role.Role != Madmate.MyRole);
            int aliveCrewmateCount = aliveCrewmate?.Count() ?? 0;
            if (EndState!.EndReason == GameEndReason.Task)
            {
                if(allModPlayers.Values.All(p => !p.IsDead)) new StaticAchievementToken("challenge.crewmate");
                if (aliveCrewmateCount == 0 && NebulaGameManager.Instance!.LocalPlayerInfo.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole) new StaticAchievementToken("noCrewmate");
            }
            if(EndState!.EndCondition == NebulaGameEnd.CrewmateWin && aliveCrewmateCount == 1 && aliveCrewmate!.First().AmOwner) new StaticAchievementToken("lastCrewmate");
            
            if(Helpers.CurrentMonth == 2 && !NebulaGameManager.Instance!.LocalPlayerInfo.IsDead && EndState!.EndCondition == NebulaGameEnd.CrewmateWin && EndState!.EndReason == GameEndReason.Situation && NebulaGameManager.Instance!.LocalPlayerInfo.Tasks.IsCompletedCurrentTasks) new StaticAchievementToken("setsubun");

            if (EndState!.EndReason == GameEndReason.Situation && EndState!.EndCondition == NebulaGameEnd.ImpostorWin &&
                /*キル数2以上*/ allModPlayers.Values.Count(p => p.MyKiller?.AmOwner ?? false) >= 2 &&
                /*最後の死亡者をキルしている*/ (allModPlayers.Values.MaxBy(p => p.DeathTimeStamp ?? 0f)?.MyKiller?.AmOwner ?? false))
                new StaticAchievementToken("challenge.impostor");
        }
    }

    public PlayerModInfo? GetModPlayerInfo(byte playerId)
    {
        return allModPlayers.TryGetValue(playerId, out var v) ? v : null;
    }

    public void CheckGameState()
    {
        switch (GameState)
        {
            case NebulaGameStates.NotStarted:
                if (PlayerControl.AllPlayerControls.Count == allModPlayers.Count)
                {
                    LobbySlideManager.Abandon();
                    DestroyableSingleton<HudManager>.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
                    DestroyableSingleton<HudManager>.Instance.HideGameLoader();
                    GameState = NebulaGameStates.Initialized;
                }
                break;
        }
    }

    public IEnumerator CoWaitAndEndGame()
    {
        if(GameState != NebulaGameStates.Finished) GameState = NebulaGameStates.WaitGameResult;

        while (ExileController.Instance && !Minigame.Instance) yield return null;

        yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, 0.5f, false);
        if (AmongUsClient.Instance.AmHost) GameManager.Instance.RpcEndGame(EndState?.EndCondition == NebulaGameEnd.CrewmateWin ? GameOverReason.HumansByTask : GameOverReason.ImpostorByKill, false);

        while (GameState != NebulaGameStates.Finished) yield return null;

        SceneManager.LoadScene("EndGame");
        yield break;
    }

    public void ReceiveVanillaGameResult()
    {
        GameState = NebulaGameStates.Finished;
    }

    public void ToGameEnd()
    {
        if (Minigame.Instance)
        {
            try
            {
                Minigame.Instance.Close();
                Minigame.Instance.Close();
            }
            catch
            {
            }
        }

        HudManager.Instance.StartCoroutine(CoWaitAndEndGame().WrapToIl2Cpp());
    }

    public IEnumerable<PlayerModInfo> AllPlayerInfo() => allModPlayers.Values;
    public void AllEntitiesAction(Action<IGameEntity> action) => GameEntityManager.AllEntities.Do(action);
    

    public void AllAssignableAction(Action<AssignableInstance> action)
    {
        foreach (var p in AllPlayerInfo()) p.AssignableAction(action);
    }

    public void AllRoleAction(Action<RoleInstance> action)
    {
        foreach (var p in AllPlayerInfo()) action.Invoke(p.Role);
    }

    public void RpcInvokeSpecialWin(CustomEndCondition endCondition, int winnersMask)
    {
        if (GeneralConfigurations.CurrentGameMode.AllowSpecialEnd) RpcSpecialWin.Invoke(new(endCondition.Id, winnersMask));
    }

    public void RpcInvokeForcelyWin(CustomEndCondition endCondition, int winnersMask)
    {
        RpcSpecialWin.Invoke(new(endCondition.Id, winnersMask));
    }

    public bool TryGetProperty(string id,out INebulaProperty? property)
    {
        foreach (var p in allModPlayers.Values) if (p.TryGetProperty(id, out property)) return true;

        property = null;
        return false;
    }

    static RemoteProcess<Tuple<int, int>> RpcSpecialWin = new RemoteProcess<Tuple<int, int>>(
        "SpecialWin",
        (writer, message) =>
        {
            writer.Write(message.Item1);
            writer.Write(message.Item2);
        },
        (reader) => new(reader.ReadInt32(),reader.ReadInt32()),
        (message, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            NebulaGameManager.Instance?.CheckAndEndGame(CustomEndCondition.GetEndCondition((byte)message.Item1), GameEndReason.Special, message.Item2);
        }
        );

    public readonly static RemoteProcess RpcStartGame = new RemoteProcess(
        "StartGame",
        (_) =>
        {
            NebulaGameManager.Instance?.CheckGameState();
            NebulaGameManager.Instance?.AllAssignableAction(r=> { 
                r.OnActivated(); r.Register();
                if (r is RuntimeRole role)
                {
                    GameEntityManager.Instance?.GetPlayerEntities(r.MyPlayer).Do(e => e.OnSetRole(role));
                    GameEntityManager.Instance?.AllEntities.Do(e => e.OnSetRole(r.MyPlayer, role));
                }
                if (r is RuntimeModifier modifier)
                {
                    GameEntityManager.Instance?.GetPlayerEntities(r.MyPlayer).Do(e => e.OnAddModifier(modifier));
                    GameEntityManager.Instance?.AllEntities.Do(e => e.OnAddModifier(r.MyPlayer, modifier));
                }
            });
        }

        );

    public PlayerModInfo? GetLastDead => allModPlayers.Values.MaxBy(p => p.DeathTimeStamp ?? 0f);

    // Virial.Game.Game
    Virial.Game.Player? Virial.Game.Game.GetPlayer(byte playerId)=>GetModPlayerInfo(playerId);

    IEnumerable<Virial.Game.Player> Virial.Game.Game.GetAllPlayers() => AllPlayerInfo();

    Virial.Game.Player Virial.Game.Game.LocalPlayer => GetModPlayerInfo(PlayerControl.LocalPlayer.PlayerId)!;

    Virial.Game.HUD Virial.Game.Game.CurrentHud => this;

    bool Virial.ILifespan.IsDeadObject => NebulaGameManager.Instance != this;
}

public static class NebulaGameManagerExpansion
{
    static public PlayerModInfo? GetModInfo(this PlayerControl? player)
    {
        if (!player) return null;
        return NebulaGameManager.Instance?.GetModPlayerInfo(player!.PlayerId);
    }


}

