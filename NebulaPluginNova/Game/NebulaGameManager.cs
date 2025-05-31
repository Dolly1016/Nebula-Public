using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Abilities;
using Nebula.Roles.Crewmate;
using Nebula.VoiceChat;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Utilities;

namespace Nebula.Game;

public enum NebulaGameStates
{
    NotStarted,
    Initialized,
    WaitGameResult,
    Finished
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

public static class RoleHistoryHelper { 
    static internal IEnumerable<T> EachMoment<T>(this IReadOnlyList<RoleHistory> history, Predicate<RoleHistory> predicate, Func<RuntimeRole, RuntimeGhostRole?, List<RuntimeAssignable>, T> converter)
    {
        RuntimeRole? role = null;
        RuntimeGhostRole? ghostRole = null;
        List<RuntimeAssignable> modifiers = new();
        bool isDead = false;

        float lastTime = history[0].Time;
        T last;
        
        bool isFirst = true;
        foreach(var h in history.Append(null))
        {
            if (h != null && !predicate(h)) continue;

            //情報の更新前に直前の様子を記録
            if (role != null)
            {
                last = converter.Invoke(role, isDead ? ghostRole : null, modifiers);
                if (h == null || (lastTime + 1f < h.Time && !isFirst)) yield return last;
            }
            
                


            if (h == null) break;
            lastTime = h.Time;

            isDead = true;
            if (!h.IsModifier && h.Assignable is RuntimeRole ri) role = ri;
            else if (!h.IsModifier && h.Assignable is RuntimeGhostRole gri) ghostRole = gri;
            else if (h.IsSet) modifiers.Add(h.Assignable);
            else modifiers.Remove(h.Assignable);

            isFirst = false;
        }
    }

    static public string ConvertToRoleName(RuntimeRole role, RuntimeGhostRole? ghostRole, List<RuntimeAssignable> modifier, bool isShort)
    {
        string result;

        Color color;

        if (ghostRole != null)
        {
            result = isShort ? ghostRole.Role.DisplayColoredShort : ghostRole.Role.DisplayColoredName;
            color = ghostRole.Role.UnityColor;
            ghostRole.DecorateNameConstantly(ref result, true);
        }
        else
        {
            result = isShort ? role.DisplayColoredShort : role.DisplayColoredName;
            color = role.Role.UnityColor;
            role.DecorateNameConstantly(ref result, true);
        }

        foreach (var m in modifier)
        {
            var newName = m.OverrideRoleName(result, isShort);
            if (newName != null) result = newName;
        }
        
        foreach (var m in modifier) m.DecorateNameConstantly(ref result, true);
        return result.Replace(" ", "").Color(color);
    }
}


public class TitleShower : AbstractModule<Virial.Game.Game>, IGameOperator
{
    TextMeshPro mainText, shadowText;
    Transform textHolder;

    public TitleShower()
    {
        var holder = UnityHelper.CreateObject("TitleShower", HudManager.Instance.transform, new(0f, 0f, -100f), LayerExpansion.GetUILayer());
        mainText = GameObject.Instantiate(HudManager.Instance.IntroPrefab.ImpostorTitle, holder.transform);
        mainText!.GetComponent<TextTranslatorTMP>().enabled = false;
        mainText.transform.localPosition = new(0f, 0f, 0f);
        mainText.rectTransform.pivot = new(0.5f, 0.5f);
        mainText.rectTransform.localScale = new(3f, 3f, 1f);
        mainText.rectTransform.sizeDelta = new(2.4f, 1.8f);
        mainText.outlineColor = Color.clear;
        mainText.color = Color.white;

        mainText.text = "";

        shadowText = GameObject.Instantiate(mainText, holder.transform);
        shadowText.transform.localPosition = new(0.05f, -0.05f, 1f);
        shadowText.color = Color.black.AlphaMultiplied(0.6f);

        textHolder = holder.transform;

        SetPivot(new(0.5f, 0.5f));

        this.Register(NebulaAPI.CurrentGame!);
    }

    public TitleShower SetPivot(Vector2 pivot)
    {
        mainText.rectTransform.pivot = pivot;
        shadowText.rectTransform.pivot = pivot;
        return this;
    }

    public TitleShower SetText(string text, Color color, float duration, bool shake = false)
    {
        mainText.text = text;
        shadowText.text = text;
        textColor = color;

        HudUpdate(null!);

        alpha = 1f;
        timer = duration;

        this.shake = shake;

        return this;
    }

    float alpha = 1f;
    float timer = 0f;
    Color textColor = Color.white;
    bool shake = false;
    float shakeTimer = 0f;

    void HudUpdate(GameHudUpdateEvent? ev)
    {
        if (shake)
        {
            shakeTimer -= Time.deltaTime;
            if(shakeTimer < 0f)
            {
                shakeTimer = 0.08f;
                textHolder.localPosition = new(
                    ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.06f,
                    ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.06f, -100f);
            }
        }
        else
        {
            textHolder.localPosition = new(0f, 0f, -100f);
        }

        if(timer > 0f)
        {
            timer -= Time.deltaTime;
            alpha = 1f;
        }
        else
        {
            alpha -= Time.deltaTime * 0.5f;
        }
        alpha = Mathf.Clamp01(alpha);

        mainText.color = textColor.AlphaMultiplied(alpha);
        shadowText.color = Color.black.AlphaMultiplied(0.6f * alpha);
    }
}


[NebulaRPCHolder]
internal class NebulaGameManager : AbstractModuleContainer, IRuntimePropertyHolder, Virial.Game.Game
{
    static private NebulaGameManager? instance = null;
    static public NebulaGameManager? Instance { get => instance; }

    private Dictionary<byte, GamePlayer> allModPlayers;
    private GamePlayer[] allOrderedPlayers;

    public List<AchievementTokenBase> AllAchievementTokens = new();
    public T? GetAchievementToken<T>(string achievement) where T : AchievementTokenBase {
        return AllAchievementTokens.FirstOrDefault(a => a.Achievement.Id == achievement) as T;
    }


    //ゲーム開始時からの経過時間
    public float CurrentTime { get; private set; } = 0f;

    //各種進行状況
    public NebulaGameStates GameState { get; private set; } = NebulaGameStates.NotStarted;
    public EndState? EndState { get; set; } = null;

    //ゲーム内アセット
    public RuntimeGameAsset RuntimeAsset { get; private init; }

    //エンティティマネージャ
    public GameOperatorManager GameEntityManager { get; init; } = new();

    //各種モジュール
    public HudGrid HudGrid { get; private set; }
    public GameStatistics GameStatistics { get; private set; } = new();
    public CriteriaManager CriteriaManager { get; private set; } = new();
    internal LobbySlideManager LobbySlideManager { get; private set; } = new();
    internal VoiceChatManager? VoiceChatManager { get; set; }
    public ConsoleRestriction ConsoleRestriction { get; private set; } = new();
    public AttributeShower AttributeShower { get; private set; } = new();
    public RPCScheduler Scheduler { get; private set; } = new();
    public FakeSabotageStatus? LocalFakeSabotage => GamePlayer.LocalPlayer?.Unbox().FakeSabotage;
    public IRoleAllocator? RoleAllocator { get; internal set; } = null;

    private KillButtonLikeHandler killButtonLikeHandler = new KillButtonLikeHandlerImpl(new VanillaKillButtonHandler(HudManager.Instance.KillButton));
    internal KillRequestHandler KillRequestHandler { get; private init; } = new();

    public bool IgnoreWalls => LocalPlayer?.EyesightIgnoreWalls ?? false;
    public Dictionary<byte, INebulaAchievement?> TitleMap = new();
    public bool TryGetTitle(byte playerId, [MaybeNullWhen(false)] out INebulaAchievement title) => TitleMap.TryGetValue(playerId, out title);

    static private OutfitDefinition.OutfitId UnknownOutfitId = new(-1, 0);
    public OutfitDefinition UnknownOutfit => OutfitMap[UnknownOutfitId];
    public Dictionary<OutfitDefinition.OutfitId, Virial.Game.OutfitDefinition> OutfitMap = new(
        [KeyValuePair.Create<OutfitDefinition.OutfitId, Virial.Game.OutfitDefinition>(UnknownOutfitId, new(UnknownOutfitId, new() { PlayerName = "", ColorId = NebulaPlayerTab.CamouflageColorId, HatId = "hat_NoHat", SkinId = "skin_None", VisorId = "visor_EmptyVisor", PetId = "pet_EmptyPet" }, []))]);
    public bool TryGetOutfit(OutfitDefinition.OutfitId id, [MaybeNullWhen(false)] out OutfitDefinition outfit) => OutfitMap.TryGetValue(id, out outfit);
    public WideCamera WideCamera { get; init; } = new();

    //自身のキルボタン用トラッカー
    private ObjectTracker<GamePlayer> KillButtonTracker = null!;
    public int EmergencyCalls = 0;

    //天界視点フラグ
    public bool CanBeSpectator { get; private set; }
    public bool CanSeeAllInfo => CanBeSpectator && (ClientOption.GetValue(ClientOption.ClientOptionType.SpoilerAfterDeath) == 1 || !HudManager.InstanceExists);
    public void ChangeToSpectator(bool tryGhostAssignment = true)
    {
        if (CanBeSpectator) return;
        CanBeSpectator = true;

        if (!HudManager.InstanceExists) return;

        if (!LocalPlayer.AttemptedGhostAssignment) RpcTryAssignGhostRole.Invoke(LocalPlayer);
        

        new SpectatorsAbility().Register(this);
    }

    //ゲーム内履歴
    public List<RoleHistory> RoleHistory = new();
    IReadOnlyList<RoleHistory> IArchivedGame.RoleHistory => RoleHistory;
    IArchivedPlayer? IArchivedGame.GetPlayer(byte playerId) => GetPlayer(playerId);
    IEnumerable<IArchivedPlayer> IArchivedGame.GetAllPlayers() => AllPlayerInfo;
    IArchivedEvent[] IArchivedGame.ArchivedEvents => GameStatistics.Sealed;
    byte IArchivedGame.MapId => AmongUsUtil.CurrentMapId;
    ArchivedColor IArchivedGame.GetColor(byte colorId) => new(new(DynamicPalette.PlayerColors[colorId]), new(DynamicPalette.ShadowColors[colorId]), new(DynamicPalette.VisorColors[colorId]));

    static private SpriteLoader vcConnectSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.VCReconnectButton.png", 100f);
    public NebulaGameManager()
    {
        allModPlayers = new();
        instance = this;
        HudGrid = HudManager.Instance.gameObject.AddComponent<HudGrid>();
        RuntimeAsset = new();

        var vcConnectButton = new Modules.ScriptComponents.ModAbilityButtonImpl(true).Register(this);
        vcConnectButton.Visibility = (_) => VoiceChatManager != null && GameState == NebulaGameStates.NotStarted;
        vcConnectButton.Availability = (_) =>true;
        vcConnectButton.SetSprite(vcConnectSprite.GetSprite());
        vcConnectButton.OnClick = (_) => VoiceChatManager!.Rejoin();
        vcConnectButton.SetLabel("rejoin");

        VoiceChatManager = GeneralConfigurations.UseVoiceChatOption ? new() : null;

        localPlayerCache = new(() => GetPlayer(PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.PlayerId : (byte)255)!);
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
        if (SceneManager.GetActiveScene().name == "EndGame")
            VoiceChatManager?.OnGameEndScene();
        else
        {
            VoiceChatManager?.Dispose();
            VoiceChatManager = null;
        }
    }

    void Virial.Game.Game.RegisterEntity(IGameOperator entity, ILifespan lifespan) => GameEntityManager.Subscribe(entity, lifespan);
    

    public GamePlayer RegisterPlayer(PlayerControl player)
    {
        if(allModPlayers.ContainsKey(player.PlayerId)) return allModPlayers[player.PlayerId];

        Debug.Log("Registered: " + player.name);
        var info = DIManager.Instance.Instantiate<GamePlayer>(p => p.Unbox().SetPlayer(player))!;
        allModPlayers.Add(player.PlayerId, info);

        if (player.AmHost() && (GeneralConfigurations.AssignOpToHostOption) || GeneralConfigurations.CurrentGameMode == GameModes.FreePlay) info.Unbox().PermissionHolder.AddPermission(PlayerModInfo.OpPermission);

        return info;
    }

    public void RpcPreSpawn(byte playerId,Vector2 spawnPos)
    {
        CombinedRemoteProcess.CombinedRPC.Invoke(
            GameStatistics.RpcPoolPosition.GetInvoker(new(GameStatisticsGatherTag.Spawn, playerId, spawnPos)),
            Modules.Synchronizer.RpcSync.GetInvoker(new(SynchronizeTag.PreSpawnMinigame, PlayerControl.LocalPlayer.PlayerId))
            );
    }

    internal void InvokeEndGame(Virial.Game.GameEnd? endCondition, GameEndReason endReason, int winnersMask = 0)
    {
        if(endCondition == null) return;
        if (GameState != NebulaGameStates.Initialized) return;
        
        var finallyEnd = GameOperatorManager.Instance!.Run(new EndCriteriaMetEvent(endCondition, endReason, winnersMask, CheckWinners));

        NebulaGameEnd.RpcSendGameEnd(finallyEnd.OverwrittenGameEnd!, (int)finallyEnd.Winners.AsRawPattern, finallyEnd.ExtraWinRawMask, finallyEnd.OverwrittenEndReason, finallyEnd.GameEnd, finallyEnd.EndReason);

        (BitMask<GamePlayer> winnersRawMask, ulong extraWinRawMask) CheckWinners(int prewinnersMask, Virial.Game.GameEnd gameEnd, GameEndReason reason, BitMask<GamePlayer>? lastWinners)
        {
            lastWinners ??= new EmptyMask<GamePlayer>();

            int winnersRawMask = prewinnersMask;
            int blockedRawMask = 0;

            if (gameEnd.AllowWin)
            {
                //勝利者を洗い出す
                foreach (var p in allModPlayers.Values) winnersRawMask |= GameEntityManager.Run(new PlayerCheckWinEvent(p, gameEnd, lastWinners)).IsWin ? (1 << p.PlayerId) : 0;

                //勝利のブロックチェック
                FunctionalMask<GamePlayer> winnerMask = new(p => (winnersRawMask & (1 << (p?.PlayerId ?? 24))) != 0);

                foreach (var p in allModPlayers.Values) blockedRawMask |= GameEntityManager.Run(new PlayerBlockWinEvent(p, winnerMask, gameEnd, lastWinners)).IsBlocked ? 1 << p.PlayerId : 0;

                //ブロックチェックの結果を統合
                winnersRawMask &= ~blockedRawMask;

                //追加勝利の判定
                EditableBitMask<Virial.Game.ExtraWin> extraWinMask = new HashSetMask<Virial.Game.ExtraWin>();
                for (int phase = 0; phase < (int)ExtraWinCheckPhase.PhaseMax; phase++)
                {
                    int extraMask = 0;
                    foreach (var p in allModPlayers.Values) extraMask |= GameEntityManager.Run(new PlayerCheckExtraWinEvent(p, winnerMask, extraWinMask, gameEnd, (ExtraWinCheckPhase)phase, lastWinners)).IsExtraWin ? 1 << p.PlayerId : 0;

                    //追加勝利の結果を統合
                    winnersRawMask |= extraMask;
                }

                //追加勝利の理由を拾い出す
                ulong extraWinRawMask = 0;
                foreach (var exWin in ExtraWin.AllExtraWins) if (extraWinMask.Test(exWin)) extraWinRawMask |= exWin.ExtraWinMask;

                return (BitMasks.AsPlayer((uint)winnersRawMask), extraWinRawMask);
            }
            else
            {
                return (new EmptyMask<GamePlayer>(), 0);
            }
        }
    }

    public void OnTaskUpdated(GamePlayer player)
    {
        GameEntityManager.Run(new PlayerTaskUpdateEvent(player));
    }

    public void OnMeetingStart()
    {

        if (PlayerControl.LocalPlayer.Data.IsDead) ChangeToSpectator();

        foreach (var p in allModPlayers) p.Value.Unbox().OnMeetingStart();

        var meetingProps = GameEntityManager.Run(new MeetingStartEvent());
        MeetingHudExtension.CanVote = meetingProps.CanVote;

        Scheduler.Execute(RPCScheduler.RPCTrigger.PreMeeting);
    }

    public void OnMeetingEnd(PlayerControl[]? players)
    {
        if (PlayerControl.LocalPlayer.Data.IsDead) ChangeToSpectator();

        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) p.onLadder = false;

        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) p.onLadder = false;

        ConsoleRestriction?.OnMeetingEnd();
        Scheduler.Execute(RPCScheduler.RPCTrigger.AfterMeeting);
    }

    public void OnFollowerCameraUpdate()
    {
        if(HudManager.InstanceExists) WideCamera.Update();
    }

    public void OnLateUpdate()
    {
        GameEntityManager.Run(new GameLateUpdateEvent(this));
    }

    public void OnUpdate() {
        CurrentTime += Time.deltaTime;

        //WideCamera.Update();
        GameEntityManager.Update();

        if (VoiceChatManager == null && GeneralConfigurations.UseVoiceChatOption) VoiceChatManager = new();
        VoiceChatManager?.Update();

        GameEntityManager.Run(new GameHudUpdateFasterEvent(this));
        GameEntityManager.Run(new GameHudUpdateEvent(this));
        GameEntityManager.Run(new GameHudUpdateLaterEvent(this));

        if (!PlayerControl.LocalPlayer) return;
        //バニラボタンの更新
        var localModInfo = GamePlayer.LocalPlayer?.Unbox();
        if (localModInfo != null)
        {

            //ベントボタン
            var ventTimer = PlayerControl.LocalPlayer.inVent ? localModInfo.Role?.VentDuration : localModInfo.Role?.VentCoolDown;
            string ventText = "";
            float ventPercentage = 0f;
            if (ventTimer != null && ventTimer.IsProgressing)
            {
                ventText = Mathf.CeilToInt(ventTimer.CurrentTime).ToString();
                ventPercentage = ventTimer.Percentage;
            }
            if (ventTimer != null && !ventTimer.IsProgressing && PlayerControl.LocalPlayer.inVent)
            {
                Vent.currentVent.SetButtons(false);
                var exitVent = Vent.currentVent.GetValidVent();
                if(exitVent != null) PlayerControl.LocalPlayer.MyPhysics.RpcExitVent(exitVent.Id);
            }

            var ventButton = HudManager.Instance.ImpostorVentButton;
            ventButton.SetCooldownFill(ventPercentage);
            CooldownHelpers.SetCooldownNormalizedUvs(ventButton.graphic);
            ventButton.cooldownTimerText.text = ventText;
            ventButton.cooldownTimerText.color = PlayerControl.LocalPlayer.inVent ? Color.green : Color.white;
            
            //サボタージュボタン


            //ローカルモジュール
            AttributeShower.Update(localModInfo);
        }
    }

    public void OnFixedAlwaysUpdate()
    {
        GameEntityManager.Run(new UpdateEvent());

        //ベントのターゲットを修正する
        var localModInfo = LocalPlayer;
        if (localModInfo != null)
        {
            var ventButton = HudManager.Instance.ImpostorVentButton;
            if (localModInfo.Role?.PreventUsingVent ?? false) ventButton.SetTarget(null);

            //無効なベントをターゲットしている場合は対象から外す。
            UtilityInvalidationSystem.Instance.CurrentInvalidVent = null;
            if (ventButton.currentTarget != null && ventButton.currentTarget.TryGetComponent<InvalidVent>(out var invalidVent))
            {
                UtilityInvalidationSystem.Instance.CurrentInvalidVent = invalidVent;
                ventButton.SetTarget(null);
            }
        }

    }

    public void OnFixedUpdate() {
        GameEntityManager.Run(new GameUpdateEvent(this));
        if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && HudManager.Instance.KillButton.gameObject.active)
        {
            var info = GamePlayer.LocalPlayer;
            if (info != null && (info.IsDived || info.IsBlown))
            {
                HudManager.Instance.KillButton.SetTarget(null);
            }
            else if(info != null)
            {
                KillButtonTracker ??= ObjectTrackers.ForPlayer(null, info, (p) => (info.AllAbilities.Any(a => a.KillIgnoreTeam) ? ObjectTrackers.StandardPredicate(p) : ObjectTrackers.LocalKillablePredicate(p)) && HudManager.Instance.KillButton.gameObject.active, Palette.ImpostorRed, Roles.Impostor.Impostor.CanKillHidingPlayerOption).Register(NebulaAPI.CurrentGame!);
                HudManager.Instance.KillButton.SetTarget(KillButtonTracker.CurrentTarget?.VanillaPlayer);
            }
            else
            {
                HudManager.Instance.KillButton.SetTarget(null);
            }
        }

    }
    public void OnGameStart()
    {
        WideCamera.OnGameStart();

        //マップの取得
        RuntimeAsset.MinimapPrefab = ShipStatus.Instance.MapPrefab;
        RuntimeAsset.MinimapPrefab.gameObject.MarkDontUnload();
        RuntimeAsset.MapScale = ShipStatus.Instance.MapScale;

        //VC
        VoiceChatManager?.OnGameStart();

        foreach (var p in allModPlayers) p.Value.Unbox().OnGameStart();
        GameEntityManager.Run(new GameStartEvent(this), true);

        HudManager.Instance.UpdateHudContent();

        ConsoleRestriction?.OnGameStart();

        //統計の更新
        new StaticAchievementToken("stats.gamePlay");
        new StaticAchievementToken("stats.role." + LocalPlayer.Role.Role.Id + ".assigned");
        LocalPlayer.Modifiers.Do(m => new StaticAchievementToken("stats.modifier." + m.Modifier.Id + ".assigned"));

        if (GeneralConfigurations.LowLatencyPlayerSyncOption && (AmongUsUtil.IsCustomServer() || AmongUsUtil.IsLocalServer()))
        {
            AmongUsClient.Instance.MinSendInterval = 0.05f;
        }
    }

    public void OnGameEnd()
    {
        GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.GameEnd, null, 0) { RelatedTag = EventDetail.GameEnd });

        //幽霊役職の割り当てはここで確認する
       if(LocalPlayer.GhostRole != null)new StaticAchievementToken("stats.ghostRole." + LocalPlayer.GhostRole.Role.Id + ".assigned");

        //実績

        //自身が勝利している場合
        bool wasWon = EndState!.Winners.Test(GamePlayer.LocalPlayer);
        if (wasWon) {
            //生存しているマッドメイト除くクルー陣営
            var aliveCrewmate = allModPlayers.Values.Where(p => !p.IsDead && p.IsTrueCrewmate);
            int aliveCrewmateCount = aliveCrewmate?.Count() ?? 0;
            if (EndState!.EndReason == GameEndReason.Task)
            {
                if(allModPlayers.Values.All(p => !p.IsDead)) new StaticAchievementToken("challenge.crewmate");
                if (aliveCrewmateCount == 0 && LocalPlayer?.Role.Role.Category == RoleCategory.CrewmateRole) new StaticAchievementToken("noCrewmate");
            }
            if(EndState!.EndCondition == NebulaGameEnd.CrewmateWin && aliveCrewmateCount == 1 && aliveCrewmate!.First().AmOwner) new StaticAchievementToken("lastCrewmate");
            
            if(Helpers.CurrentMonth == 2 && !LocalPlayer!.IsDead && EndState!.EndCondition == NebulaGameEnd.CrewmateWin && EndState!.EndReason == GameEndReason.Situation && GamePlayer.LocalPlayer.Tasks.IsCompletedCurrentTasks) new StaticAchievementToken("setsubun");

            if (EndState!.EndReason == GameEndReason.Situation && EndState!.EndCondition == NebulaGameEnd.ImpostorWin &&
                /*キル数2以上*/ allModPlayers.Values.Count(p => p.MyKiller?.AmOwner ?? false) >= 2 &&
                /*最後の死亡者をキルしている*/ (allModPlayers.Values.MaxBy(p => p.Unbox().DeathTimeStamp ?? 0f)?.MyKiller?.AmOwner ?? false))
                new StaticAchievementToken("challenge.impostor");

            //各役職・終了条件の勝利回数に加算
            new StaticAchievementToken("stats.role." + LocalPlayer!.Role.Role.Id + ".won");
            LocalPlayer.Modifiers.Do(m => new StaticAchievementToken("stats.modifier." + m.Modifier.Id + ".won"));
            if (LocalPlayer.GhostRole != null) new StaticAchievementToken("stats.ghostRole." + LocalPlayer.GhostRole.Role.Id + ".won");
        }

        new StaticAchievementToken($"stats.end.{(wasWon ? "win" : "lose")}.{(EndState.EndCondition.ImmutableId ?? "-")}");


        if (Helpers.CurrentMonth == 9 && NebulaGameManager.Instance!.RoleHistory.Count(h => !h.IsModifier && h.PlayerId == GamePlayer.LocalPlayer.PlayerId) >= 6) new StaticAchievementToken("autumnSky");
    }

    public GamePlayer? GetPlayer(byte playerId)
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
        while ((HudManager.Instance.shhhEmblem.isActiveAndEnabled || IntroCutscene.Instance) && !Minigame.Instance) yield return null;

        yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, 0.5f, false);
        if (AmongUsClient.Instance.AmHost) GameManager.Instance.RpcEndGame(EndState?.EndCondition == NebulaGameEnd.CrewmateWin ? GameOverReason.CrewmatesByTask : GameOverReason.ImpostorsByKill, false);

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

    public IEnumerable<GamePlayer> AllPlayerInfo => allModPlayers.Values;
    public int AllPlayersNum => allModPlayers.Count;

    public void AllAssignableAction(Action<RuntimeAssignable> action)
    {
        foreach (var p in AllPlayerInfo) p.Unbox().AssignableAction(action);
    }

    public void AllRoleAction(Action<RuntimeRole> action)
    {
        foreach (var p in AllPlayerInfo) action.Invoke(p.Role);
    }

    public void RpcInvokeSpecialWin(Virial.Game.GameEnd endCondition, int winnersMask)
    {
        RpcInvokeSpecialTrigger.Invoke((endCondition.Id, winnersMask));
    }

    private static readonly RemoteProcess<(int id, int winnersMask)> RpcInvokeSpecialTrigger = new("SpecialTrigger", (message, _) => {
        if (NebulaAPI.CurrentGame?.GetModule<IGameModeModule>()?.AllowSpecialGameEnd ?? false)
        {
            GameEnd.TryGet((byte)message.id, out var end);
            Instance!.CriteriaManager.Trigger(end!, GameEndReason.Special, BitMasks.AsPlayer((uint)message.winnersMask));
        }
    });

    public void RpcInvokeForcelyWin(GameEnd endCondition, int winnersMask)
    {
        RpcSpecialWin.Invoke(new(endCondition.Id, winnersMask));
    }

    public bool TryGetProperty(string id,out INebulaProperty? property)
    {
        foreach (var p in allModPlayers.Values) if (p.Unbox().TryGetProperty(id, out property)) return true;

        property = null;
        return false;
    }

    static RemoteProcess<(byte id, int winnersMask)> RpcSpecialWin = new(
        "SpecialWin",
        (message, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            GameEnd.TryGet(message.id, out var end);
            NebulaGameManager.Instance?.InvokeEndGame(end, GameEndReason.Special, message.winnersMask);
        }
        );

    public readonly static RemoteProcess RpcStartGame = new RemoteProcess(
        "StartGame",
        (_) =>
        {
            NebulaGameManager.Instance?.CheckGameState();
            NebulaGameManager.Instance?.AllAssignableAction(r=> {
                r.OnActivated(); (r as IGameOperator)?.Register(r);
                if (r is RuntimeRole role)
                    GameOperatorManager.Instance?.Run(new PlayerRoleSetEvent(r.MyPlayer, role));
                if (r is RuntimeModifier modifier)
                    GameOperatorManager.Instance?.Run(new PlayerModifierSetEvent(r.MyPlayer, modifier));
                
            });
        }

        );

    public GamePlayer? LastDead => allModPlayers.Values.MaxBy(p => p.Unbox().DeathTimeStamp ?? 0f);

    // Virial.Game.Game
    Virial.Game.Player? Virial.Game.Game.GetPlayer(byte playerId)=>GetPlayer(playerId);

    IEnumerable<Virial.Game.Player> Virial.Game.Game.GetAllPlayers() => AllPlayerInfo;
    IReadOnlyList<Virial.Game.Player> Virial.Game.Game.GetAllOrderedPlayers()
    {
        if(allOrderedPlayers == null || allOrderedPlayers.Length != AllPlayerInfo.Count()) allOrderedPlayers = allModPlayers.Values.OrderBy(p => p.PlayerId).ToArray();
        return allOrderedPlayers;
    }

    void Virial.Game.Game.TriggerGameEnd(GameEnd gameEnd, GameEndReason reason, EditableBitMask<GamePlayer>? additionalWinners) => CriteriaManager.Trigger(gameEnd, reason, additionalWinners);
    void Virial.Game.Game.RequestGameEnd(GameEnd gameEnd, BitMask<GamePlayer> winners) => RpcInvokeSpecialWin(gameEnd, AllPlayerInfo.Where(p => winners.Test(p)).Aggregate(0, (v, p) => v | (1 << p.PlayerId)));

    private Cache<GamePlayer> localPlayerCache;
    public GamePlayer LocalPlayer => localPlayerCache.Get();
    KillButtonLikeHandler Virial.Game.Game.KillButtonLikeHandler => killButtonLikeHandler;

    bool Virial.ILifespan.IsDeadObject => NebulaGameManager.Instance != this;

    public readonly static RemoteProcess<GamePlayer> RpcTryAssignGhostRole = new(
        "TryAssignGhostRole",
        (message, _) =>
        {
            if (message.AttemptedGhostAssignment) return;

            if (AmongUsClient.Instance.AmHost)
            {
                var role = Instance?.RoleAllocator?.AssignToGhost(message);
                if (role != null) message.Unbox().RpcInvokerSetGhostRole(role, null).InvokeSingle();
            }
            message.AttemptedGhostAssignment = true;
        }
        );

    private readonly static RemoteProcess<(GamePlayer player, UnityEngine.Vector2 position, string id)> RpcGameAction = new(
        "GameAction",
        (message, _) =>
        {
            if (GameActionType.TryGetActionType(message.id, out var actionType))
            {
                var ev = new PlayerDoGameActionEvent(message.player, actionType, message.position);
                GameOperatorManager.Instance?.Run(ev);
            }
        }
        );

    public void RpcDoGameAction(GamePlayer player, UnityEngine.Vector2 position, GameActionType actionType) => RpcGameAction.Invoke((player, position, actionType.Id));

    public bool HavePassed(float since, float duration) => since + duration < CurrentTime;
}

internal static class NebulaGameManagerExpansion
{
    static internal GamePlayer? GetModInfo(this PlayerControl? player)
    {
        if (!player) return null;
        return NebulaGameManager.Instance?.GetPlayer(player!.PlayerId);
    }
}

