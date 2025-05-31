using Nebula.Behavior;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Roles.Neutral;

public class PlayerDanceEvent : AbstractPlayerEvent
{
    public BitMask<GamePlayer> Players { get; private init; }
    public BitMask<GamePlayer> Corpses { get; private init; }
    public PlayerDanceEvent(GamePlayer player, BitMask<GamePlayer> players, BitMask<GamePlayer> corpses) : base(player) { 
        Players = players;
        Corpses = corpses;
    }
}

[NebulaRPCHolder]
[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class DanceModule : AbstractModule<GamePlayer>, IGameOperator
{
    public static RemoteProcess<(GamePlayer player, Vector2 startPos, uint playerMask, uint corpseMask)> RpcDance = new("Dance", (message, _) => GameOperatorManager.Instance?.Run(new PlayerDanceEvent(message.player, BitMasks.AsPlayer((uint)message.playerMask), BitMasks.AsPlayer((uint)message.corpseMask))));
    //ダンス完遂に必要な時間
    private static float DanceDuration => Dancer.DanceDurationOption;
    //ダンスの効力が及ぶ範囲
    private static float DanceRange => Dancer.DanceRangeOption;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        DIManager.Instance.RegisterModule(() => new DanceModule());
    }

    public DanceModule()
    {
        this.Register(NebulaAPI.CurrentGame!);
    }

    private Vector2? lastPos = null;
    private Vector2 displacement = new();
    private float distance = 0f;
    private float danceGuage = 0f;

    //ダンス中の場合true
    public bool IsDancing => danceGuage > 0.7f;

    //ダンスの開始位置
    public Vector2 DanceStartPos { get; private set; } = new(-100f, -100f);
    //ダンス継続時間
    public float DancingProgress { get; private set; } = 0f;
    public float Percentage => DancingProgress / DanceDuration;
    //ダンス非継続時間
    public float NotDancingProgress { get; private set; } = 0f;
    private uint playerMask = 0;
    private uint corpseMask = 0;
    private bool eventInvoked = false;
    void Update(GameUpdateEvent ev)
    {
        //死んでいるときはダンスできない
        if (MyContainer.IsDead)
        {
            ResetState();
            return;
        }

        //ダンス状態のチェック
        Vector2 currentPos = MyContainer.VanillaPlayer.transform.position;
        if (lastPos != null)
        {
            distance *= 0.89f;
            distance += currentPos.Distance(lastPos.Value);

            displacement *= 0.89f;
            displacement += currentPos - lastPos.Value;
        }
        lastPos = currentPos;

        if (distance > 0.3f && displacement.magnitude < 0.18f)
            danceGuage = Math.Min(danceGuage + Time.deltaTime * 4.2f, 1f);
        else
            danceGuage = Math.Max(danceGuage - Time.deltaTime * 2.7f, 0f);

        if (IsDancing)
        {
            if (DancingProgress < 0.1f) DanceStartPos = currentPos;
            
            DancingProgress += Time.deltaTime;
            NotDancingProgress = 0f;

            NebulaGameManager.Instance!.AllPlayerInfo.Where(p => p != MyContainer && !p.IsDead && DanceStartPos.Distance(p.VanillaPlayer.transform.position) < DanceRange).Do(p => playerMask |= 1u << p.PlayerId);
            Helpers.AllDeadBodies().Where(p => p.ParentId != MyContainer.PlayerId && DanceStartPos.Distance(p.transform.position) < DanceRange).Do(p => corpseMask |= 1u << p.ParentId);


            if (DancingProgress > DanceDuration && !eventInvoked && MyContainer.AmOwner)
            {
                eventInvoked = true;
                RpcDance.Invoke((MyContainer, DanceStartPos, playerMask, corpseMask));
                if((Dancer.MyRole as ISpawnable).IsSpawnable || NebulaGameManager.Instance.AllPlayerInfo.Any(p => p.Role.Role == Dancer.MyRole))
                {
                    AmongUsUtil.PlayQuickFlash(Dancer.MyRole.UnityColor.RGBMultiplied(0.5f));
                }
                ResetState();
            }
        }
        else
        { 
            eventInvoked = false;
            NotDancingProgress += Time.deltaTime;
            if (NotDancingProgress > 0.5f) ResetState();
        }
    }

    //ダンスの進行状態を強制的にリセットします。
    public void ResetState()
    {
        playerMask = 0;
        corpseMask = 0;
        DancingProgress = 0f;
        eventInvoked = false;
        NotDancingProgress = 0f;
    }
}

[NebulaRPCHolder]
public class Dancer : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.dancer", new(243, 152, 0), TeamRevealType.OnlyMe);

    static private IntegerConfiguration NumOfSuccessfulForecastToWinOption = NebulaAPI.Configurations.Configuration("options.role.dancer.numOfSuccessfulForecastToWin", (1, 10), 4);
    static private FloatConfiguration DanceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceCoolDown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static public FloatConfiguration DanceDurationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceDuration", new([1f,2f,3f,4f,5f,7f,10f,15f,20f]), 3f, FloatConfigurationDecorator.Second);
    static public FloatConfiguration DanceRangeOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceRange", (1f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration ForecastDurationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.forecastDuration", (20f, 300f, 10f), 90f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration FinalDanceOption = NebulaAPI.Configurations.Configuration("options.role.dancer.finalDance", false);
    static private BoolConfiguration ShowDeahNotificationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.deathNotification", true);
    static private BoolConfiguration ForecastedFollowingSuicideOption = NebulaAPI.Configurations.Configuration("options.role.dancer.forecastedFollowingSuicide", true);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.dancer.vent", true);


    private Dancer() : base("dancer", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [NumOfSuccessfulForecastToWinOption, DanceCoolDownOption, DanceDurationOption, DanceRangeOption, ForecastDurationOption, FinalDanceOption, ShowDeahNotificationOption, ForecastedFollowingSuicideOption, VentConfiguration]/*, optionHolderPredicate: () => false*/) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }
    
    static public Dancer MyRole = new Dancer();
    static private GameStatsEntry StatsDance = NebulaAPI.CreateStatsEntry("stats.dancer.dance", GameStatsCategory.Roles, MyRole);
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    
    static public float DanceDuration => DanceDurationOption;

    static private RemoteProcess<GamePlayer[]> RpcLastDance = new("LastDance",
        (message, callByMe) =>
        {
            if (message.Any(p => p.AmOwner)) new StaticAchievementToken("dancer.common6");
        });
    static private RemoteProcess<(GamePlayer, int, GamePlayer[], GamePlayer[])> RpcShareDanceState = new("ShareDanceState",
    (message, callByMe) =>
    {
        if (!callByMe) (message.Item1.Role as Dancer.Instance)?.UpdateDanceState(message.Item2, message.Item3, message.Item4);
    });

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;
        bool RuntimeRole.IgnoreNoisemakerNotification => ShowDeahNotificationOption;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DanceButton.png", 100f);
        static private Image buttonKillSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DanceKillButton.png", 100f);
        public Instance(GamePlayer player) : base(player, VentConfiguration)
        {
        }

        GameTimer? danceCoolDownTimer = null;

        //ファイナルダンスモードで次のダンスがファイナルダンスになりうるとき、true
        bool nextIsFinalDance = false;

        //画面左下のホルダ
        PlayersIconHolder iconHolder;
        //ダンスボタン
        ModAbilityButton danceButton;
        //死の舞踏の使用可能回数
        int canKillLeft = 0;

        //使用済み死体
        EditableBitMask<GamePlayer> usedCorpses = BitMasks.AsPlayer();
        //ローカルのダンス進捗チェッカ(抜き出したもの)
        private DanceModule localDanceChecker;
        //Dancerのダンス進捗チェッカ(抜き出したもの)
        private DanceModule dancerDanceChecker;
        //Dancerのダンスの全目撃者(称号用)
        HashSet<GamePlayer> danceLooked = new HashSet<GamePlayer>();
        //死の預言が有効なプレイヤー
        List<GamePlayer> activeDanceLooked = new();
        //死の預言が完遂されたプレイヤー
        List<GamePlayer> completedDanceLooked = new();
        //Dancer本人だけが使用する。死の預言の有効時間
        float[] danceLookedTimer = new float[24];
        int danceShareVersion = 0;
        public void UpdateDanceState(int version, GamePlayer[] active, GamePlayer[] completed)
        {
            //古い情報は無視する
            if (version <= danceShareVersion) return;

            danceShareVersion = version;
            activeDanceLooked = new(active);
            completedDanceLooked = new(completed);
        }
        void UpdateButtonGraphic()
        {
            if(canKillLeft <= 0 || nextIsFinalDance)
            {
                canKillLeft = 0;
                danceButton.SetImage(buttonSprite);
                danceButton.HideUsesIcon();
            }
            else
            {
                danceButton.ShowUsesIcon(0, canKillLeft.ToString());
                danceButton.SetImage(buttonKillSprite);    
            }
        }

        //各種ボタン類のセットアップ
        public override void OnActivated()
        {
            if (AmOwner)
            {
                iconHolder = new PlayersIconHolder().Register(this);
                iconHolder.XInterval = 0.35f;

                danceCoolDownTimer = new TimerImpl(DanceCoolDownOption).Start(10f).Register(this);
                danceCoolDownTimer.ResetsAtTaskPhase();

                danceButton = NebulaAPI.Modules.AbilityButton(this);
                danceButton.SetImage(buttonSprite);
                danceButton.Availability = (button) => !danceCoolDownTimer.IsProgressing;
                danceButton.Visibility = (button) => !MyPlayer.IsDead;
                danceButton.CoolDownTimer = new ScriptVisualTimer(
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.Percentage : dancerDanceChecker.Percentage,
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.TimerText : dancerDanceChecker.DancingProgress > 0 ? Mathf.CeilToInt(DanceDuration - dancerDanceChecker.DancingProgress).ToString().Color(Color.cyan) : ""
                    );
                danceButton.SetLabel("dance");
                (danceButton as ModAbilityButtonImpl)?.SetCanUseByMouseClick(true, ButtonEffect.ActionIconType.NonclickAction, "danceAction", false);
            }

            localDanceChecker = GamePlayer.LocalPlayer!.GetModule<DanceModule>()!;
            dancerDanceChecker = MyPlayer.GetModule<DanceModule>()!;
        }

        EffectCircle? circleEffect = null;

        void RuntimeAssignable.OnInactivated()
        {
            if (circleEffect != null) circleEffect.Disappear();
        }

        //ダンスの進捗について、クールダウンを考慮する
        [Local]
        void OnHudUpdate(GameHudUpdateEvent ev)
        {
            if(AmOwner && (danceCoolDownTimer?.IsProgressing ?? false))
            {
                dancerDanceChecker.ResetState();
            }

            if(dancerDanceChecker.DancingProgress > 0f && circleEffect == null)
            {
                circleEffect = EffectCircle.SpawnEffectCircle(null, MyPlayer.Position.ToUnityVector(), Dancer.MyRole.UnityColor, DanceRangeOption, null, true);
            }
            if(!(dancerDanceChecker.DancingProgress > 0f) && circleEffect != null)
            {
                circleEffect.Disappear();
                circleEffect = null;
            }

            if (AmOwner)
            {
                //関係のないプレイヤーを削除
                foreach (var icon in iconHolder.AllIcons.ToArray())
                {
                    if (!activeDanceLooked.Contains(icon.Player) && !completedDanceLooked.Contains(icon.Player)) iconHolder.Remove(icon);
                }

                //完遂したプレイヤー
                foreach (var p in completedDanceLooked)
                {
                    var icon = iconHolder.AllIcons.FirstOrDefault(icon => icon.Player == p);
                    if (icon == null) icon = iconHolder.AddPlayer(p);
                    icon.SetAlpha(false);
                    icon.SetText(null);
                }

                //有効な死の宣告を受けたプレイヤー
                foreach (var p in activeDanceLooked)
                {
                    var icon = iconHolder.AllIcons.FirstOrDefault(icon => icon.Player == p);
                    if (icon == null) icon = iconHolder.AddPlayer(p);
                    icon.SetAlpha(true);
                    icon.SetText(Mathf.CeilToInt(danceLookedTimer[p.PlayerId]).ToString());
                }
            }
        }

        //一緒にダンスする称号用
        private float pairDanceProgress = 0f;
        private AchievementToken<bool> acTokenCommon7 = new("dancer.common7", false, (val, _) => val);


        void OnDance(PlayerDanceEvent ev)
        {
            var allPlayers = NebulaGameManager.Instance!.AllPlayerInfo;
            var players = allPlayers.Where(ev.Players.Test);
            var corpses = allPlayers.Where(ev.Corpses.Test);

            Debug.Log("Dance:" + string.Join(",", players.Select(p => p.Name)));

            //ダンスをしたのがDancer、自分自身がダンスの目撃者である
            if (!AmOwner && ev.Player == MyPlayer && players.Any(p => p.AmOwner)) new StaticAchievementToken("dancer.common3");

            //ダンスをしたのがDancer
            if(ev.Player == MyPlayer)
            {
                players.Do(p => danceLooked.Add(p));
            }

            //Dancerのみ実行
            if (AmOwner)
            {
                //Dancer自身のダンス
                if (ev.Player.AmOwner)
                {
                    //ラストダンス
                    if (nextIsFinalDance && AmOwner && players.Any())
                    {
                        RpcLastDance.Invoke(players.ToArray());
                        NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.DancerWin, (int)WinnersMask.AsRawPattern);
                    }
                    else
                    {
                        //通常のダンス
                        if (ev.Players.AsRawPattern != 0) new StaticAchievementToken("dancer.common1");
                        if (ev.Corpses.AsRawPattern != 0) new StaticAchievementToken("dancer.common2");
                        int playerCount = players.Count();
                        if (playerCount >= 2) new StaticAchievementToken("dancer.another2");
                        for (int i = 0; i < playerCount; i++) new StaticAchievementToken("dancer.common8");
                        //死の舞踏によるダンサーキル
                        using (RPCRouter.CreateSection("Dance"))
                        {
                            foreach (var p in players)
                            {
                                if (canKillLeft <= 0) break;

                                if (!p.IsDead)
                                {
                                    MyPlayer.MurderPlayer(p, PlayerState.Frenzied, EventDetail.Dance, KillParameter.RemoteKill);
                                    completedDanceLooked.Add(p);
                                    canKillLeft--;
                                }
                                //自身がキルした死体は使用できないようにする。
                                usedCorpses.Add(p);
                            }
                        }
                        //死の舞踏のチャージ
                        corpses.Do(p => { if (!usedCorpses.Test(p)) canKillLeft++; usedCorpses.Add(p); });

                        //死の預言
                        foreach (var p in players)
                        {
                            //復活等の理由で完遂したプレイヤーに死の預言をかけてしまったら無視する
                            if (completedDanceLooked.Contains(p)) continue;

                            if(!activeDanceLooked.Contains(p))activeDanceLooked.Add(p);
                            danceLookedTimer[p.PlayerId] = ForecastDurationOption;
                        }
                        RpcShareDanceState.Invoke((MyPlayer, ++danceShareVersion, activeDanceLooked.ToArray(), completedDanceLooked.ToArray()));
                    }

                    StatsDance.Progress();

                    //クールダウンリセット
                    danceCoolDownTimer?.Start();
                    UpdateButtonGraphic();
                }
                else
                {
                    //ダンサー以外のダンス
                    if (activeDanceLooked.Contains(ev.Player) && players.Any(p => !p.IsDead))
                    {
                        activeDanceLooked.Remove(ev.Player);
                        var cand = players.Where(p => !p.IsDead && !p.AmOwner && !completedDanceLooked.Contains(p)).ToArray();
                        if (cand.Length > 0)
                        {
                            //ダンサー自身を除く生きた目撃者に擦る
                            var selected = cand.Random();
                            if (!activeDanceLooked.Contains(selected)) activeDanceLooked.Add(selected);
                            danceLookedTimer[selected.PlayerId] = ForecastDurationOption;
                        }
                        RpcShareDanceState.Invoke((MyPlayer, ++danceShareVersion, activeDanceLooked.ToArray(), completedDanceLooked.ToArray()));
                    }
                }
            }
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            //2人で踊る称号 互いに生存していて、近くでダンスを踊っている場合に進捗の進行度が進む
            if (!AmOwner && !MyPlayer.IsDead && !GamePlayer.LocalPlayer.IsDead && dancerDanceChecker.IsDancing && localDanceChecker.IsDancing &&
                GamePlayer.LocalPlayer.VanillaPlayer.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 2f)
            {
                pairDanceProgress += Time.deltaTime;
                if (pairDanceProgress > 0.8f) acTokenCommon7.Value = true;
            }
            else
            {
                pairDanceProgress = 0f;
            }

            //死の預言の有効時間を減少させる
            if (AmOwner && !MeetingHud.Instance && !ExileController.Instance)
            {
                for (int i = 0; i < danceLookedTimer.Length; i++) danceLookedTimer[i] -= Time.deltaTime;
                if (activeDanceLooked.RemoveAll(p => !(danceLookedTimer[p.PlayerId] > 0f)) > 0)
                    RpcShareDanceState.Invoke((MyPlayer, ++danceShareVersion, activeDanceLooked.ToArray(), completedDanceLooked.ToArray()));
            }
        }

        void OnPlayerDead(PlayerDieEvent ev)
        {
            if(AmongUsClient.Instance.AmHost && ev.Player == MyPlayer && ForecastedFollowingSuicideOption)
            {
                //ダンサー本人が死亡した場合
                if(ev is PlayerExiledEvent)
                    activeDanceLooked.Where(p => !p.IsDead).Do(p => p.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide));
                else if(MeetingHud.Instance || ExileController.Instance)
                    activeDanceLooked.Where(p => !p.IsDead).Do(p => p.Suicide(PlayerState.Suicide, null, KillParameter.MeetingKill));
                else
                    activeDanceLooked.Where(p => !p.IsDead).Do(p => p.Suicide(PlayerState.Suicide, null, KillParameter.RemoteKill));
            }

            if (AmOwner)
            {
                if (ev is PlayerMurderedEvent pme && pme.Player.PlayerState == PlayerState.Guessed && danceLooked.Contains(pme.Murderer))
                    new StaticAchievementToken("dancer.another1");
                if (ev is PlayerExiledEvent pee && danceLooked.Any(p => MeetingHudExtension.LastVotedForMap.TryGetValue(p.PlayerId, out var voteFor) && voteFor == MyPlayer.PlayerId))
                    new StaticAchievementToken("dancer.another1");
            }

            //死の預言の成功をチェックする。
            if(activeDanceLooked.Contains(ev.Player))
            {
                activeDanceLooked.Remove(ev.Player);
                completedDanceLooked.Add(ev.Player);
                if (AmOwner) RpcShareDanceState.Invoke((MyPlayer, ++danceShareVersion, activeDanceLooked.ToArray(), completedDanceLooked.ToArray()));
            }

            //自身は生存している必要がある
            if (!MyPlayer.IsDead && NebulaGameManager.Instance!.AllPlayerInfo.Count(p => (p.PlayerState == PlayerState.Frenzied && p.MyKiller == MyPlayer) || completedDanceLooked.Contains(p)) >= NumOfSuccessfulForecastToWinOption)
            {
                if (FinalDanceOption)
                {
                    nextIsFinalDance = true;
                    UpdateButtonGraphic();
                }
                else if (NebulaAPI.CurrentGame?.LocalPlayer.AmHost ?? false)
                    NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.DancerWin, (int)WinnersMask.AsRawPattern);
            }
            
            //会議以外、自分以外のキルをDancerに通知する
            if(AmOwner && ShowDeahNotificationOption && !ev.Player.AmOwner && !MeetingHud.Instance && !ExileController.Instance && !(ev.Player.MyKiller?.AmOwner ?? false))
            {
                AmongUsUtil.InstantiateNoisemakerArrow(ev.Player.VanillaPlayer.transform.position, false, 314f);
            }
        }

        void BlockWinning(PlayerBlockWinEvent ev)
        {
            ev.SetBlockedIf(!MyPlayer.IsDead && activeDanceLooked.Contains(ev.Player));
        }

        void ExtraWinning(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.DancerPhase) return;

            //タイムラグで拾い漏れがあるので、少し緩めに取る(有効な預言をうけ且つ死亡している and ダンサーの狂乱で死亡)
            if (ev.GameEnd == NebulaGameEnd.DancerWin && (completedDanceLooked.Contains(ev.Player) || (activeDanceLooked.Contains(ev.Player) && ev.Player.IsDead) || (ev.WinnersMask.Test(ev.Player.MyKiller) && ev.Player.PlayerState == PlayerState.Frenzied))) ev.IsExtraWin = true;
        }

        BitMask<GamePlayer> WinnersMask { get
            {
                var mask = BitMasks.AsPlayer();
                mask.Add(MyPlayer);
                completedDanceLooked.Do(p => mask.Add(p));
                return mask;
            } }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            bool isCompleted = completedDanceLooked.Count >= (int)NumOfSuccessfulForecastToWinOption;
            var text = Language.Translate("role.dancer.taskDanceText");
            if(!isCompleted) text += $" ({completedDanceLooked.Count}/{(int)NumOfSuccessfulForecastToWinOption})";
            text = text.Color(isCompleted ? Color.green : completedDanceLooked.Count > 0 ? Color.yellow : Color.white);

            ev.AppendText(text);

            if (FinalDanceOption)
            {
                var finalText = Language.Translate("role.dancer.taskFinalText");
                if (nextIsFinalDance) finalText = finalText.Color(Color.yellow);
            }
        }

        void OnGameEnd(GameEndEvent ev)
        {
            var localPlayer = GamePlayer.LocalPlayer;
            if (AmOwner && ev.EndState.EndCondition == NebulaGameEnd.DancerWin && ev.EndState.Winners.Test(MyPlayer) && completedDanceLooked.Any(p => p.IsImpostor || p.Role == Jackal.MyRole)) new StaticAchievementToken("dancer.challenge");
            if (danceLooked.Contains(localPlayer) && !localPlayer.IsDead && ev.EndState.Winners.Test(localPlayer)) new StaticAchievementToken("dancer.common4");
            if (completedDanceLooked.Contains(localPlayer) && localPlayer.IsDead && ev.EndState.EndCondition == NebulaGameEnd.CrewmateWin && ev.EndState.Winners.Test(localPlayer)) new StaticAchievementToken("dancer.common5");
        }
    }
}