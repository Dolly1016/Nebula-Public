using Nebula.Behaviour;
using Nebula.Game.Statistics;
using Nebula.Modules.GUIWidget;
using System.Drawing;
using System.Linq;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Neutral;

[NebulaRPCHolder]
public class Dancer : DefinedRoleTemplate, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.dancer", new(243, 152, 0), TeamRevealType.OnlyMe);

    static private IntegerConfiguration NumOfSuccessfulForecastToWinOption = NebulaAPI.Configurations.Configuration("options.role.dancer.numOfSuccessfulForecastToWin", (1, 10), 4);
    static private FloatConfiguration DanceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceCoolDown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DanceDurationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceDuration", new([1f,2f,3f,4f,5f,7f,10f,15f,20f]), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DanceRangeOption = NebulaAPI.Configurations.Configuration("options.role.dancer.danceRange", (1f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration ForecastDurationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.forecastDuration", (20f, 300f, 10f), 90f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration FinalDanceOption = NebulaAPI.Configurations.Configuration("options.role.dancer.finalDance", false);
    static private BoolConfiguration ShowDeahNotificationOption = NebulaAPI.Configurations.Configuration("options.role.dancer.deathNotification", true);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.dancer.vent", true);


    private Dancer() : base("dancer", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [NumOfSuccessfulForecastToWinOption, DanceCoolDownOption, DanceDurationOption, ForecastDurationOption, FinalDanceOption, ShowDeahNotificationOption, VentConfiguration]/*, optionHolderPredicate: () => false*/) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }
    /*
    bool DefinedAssignable.ShowOnHelpScreen => false;
    bool IGuessed.CanBeGuessDefault => false;
    bool AssignableFilterHolder.CanLoadDefault(Virial.Assignable.DefinedAssignable assignable) => false;
    AllocationParameters? DefinedSingleAssignable.AllocationParameters => null;
    */
    static public Dancer MyRole = new Dancer();

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    
    static public float DancePlayerRange => DanceRangeOption;
    static public float DanceCorpseRange => 1.5f;
    static public float DanceDuration => DanceDurationOption;

    private class DanceProgress
    {
        public Vector2 Position { get; private set; }
        public EditableBitMask<GamePlayer> Players = BitMasks.AsPlayer();
        public EditableBitMask<GamePlayer> Corpses = BitMasks.AsPlayer();
        public float Progress { get; private set; }
        public float Percentage => Progress / DanceDuration;
        public float LeftProgress => DanceDuration - Progress;
        private float failProgress = 0f;
        public bool IsFailed { get; private set; } = false;
        public bool IsCompleted { get; private set; } = false;
        public bool IsFinishedWithSomeReason => IsCompleted || IsFailed;
        private EffectCircle Effect;
        public void Update(bool isDancing)
        {
            if (IsFinishedWithSomeReason) return;

            if (isDancing)
            {
                NebulaAPI.CurrentGame?.GetAllPlayers().Where(p => !p.AmOwner && !p.IsDead && p.VanillaPlayer.transform.position.Distance(Position) < DancePlayerRange).Do(p => Players.Add(p));
                Helpers.AllDeadBodies().Where(p => p.transform.position.Distance(Position) < DanceCorpseRange).Do(p => Corpses.Add(NebulaGameManager.Instance!.GetPlayer(p.ParentId)!));
                failProgress = 0f;
                Progress += Time.deltaTime;
                if (Progress > DanceDuration)
                {
                    IsCompleted = true;
                    OnFinished();
                } 
            }
            else
            {
                failProgress += Time.deltaTime;
                if (failProgress > 0.5f)
                {
                    IsFailed = true;
                    OnFinished();
                }       
            }
        }

        private void OnFinished()
        {
            if (Effect) Effect.Disappear();
        }

        public void Destroy()
        {
            if (Effect) Effect.DestroyFast();
        }

        public DanceProgress(Vector2 pos)
        {
            Position = pos;
            Effect = EffectCircle.SpawnEffectCircle(null, pos, MyRole.UnityColor, DancePlayerRange, null, true);
        }
    }

    private class DancePlayerIcon
    {
        public PlayerIconInfo? info;
        public GamePlayer Player;
        public float LeftTime;
        public bool Success;
    }

    static private RemoteProcess<(GamePlayer dancer, GamePlayer[] addIcons)> RpcDance = new("Dance",
        (message, callByMe) =>
        {
            var dancer = message.dancer.Role as Instance;
            if (dancer == null) return;

            dancer.OnDance(message.addIcons);
        });

    static private RemoteProcess<GamePlayer[]> RpcLastDance = new("LastDance",
        (message, callByMe) =>
        {
            if (message.Any(p => p.AmOwner)) new StaticAchievementToken("dancer.common6");
        });

    public class DancerChecker
    {
        public DancerChecker(GamePlayer player)
        {
            this.Player = player;
        }
        public GamePlayer Player { get; private init; }
        private Vector2? lastPos = null;
        private Vector2 displacement = new();
        private float distance = 0f;
        private float danceGuage = 0f;
        public bool IsDancing => danceGuage > 0.7f;
        public void Update() {
            Vector2 currentPos = Player.VanillaPlayer.transform.position;
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
        }
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;


        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DanceButton.png", 100f);
        static private Image buttonKillSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DanceKillButton.png", 100f);
        public Instance(GamePlayer player) : base(player)
        {
            DancerChecker = new(player);
            LocalDancerChecker = new(NebulaGameManager.Instance!.LocalPlayerInfo);
        }

        GameTimer? danceCoolDownTimer = null;
        List<DancePlayerIcon> iconList = new();
        PlayersIconHolder iconHolder = null!;

        //ファイナルダンスモードで次のダンスがファイナルダンスになりうるとき、true
        bool nextIsFinalDance = false;

        public void OnDance(IEnumerable<GamePlayer> players)
        {
            if (nextIsFinalDance && AmOwner && players.Any()) {
                NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.DancerWin, (int)WinnersMask.AsRawPattern);
                return;
            }

            if(AmOwner && players.Count() >= 2) new StaticAchievementToken("dancer.another2");

            foreach (var p in players)
            {
                if (p.AmOwner) new StaticAchievementToken("dancer.commmon3");
                var existed = iconList.FirstOrDefault(i => i.Player.PlayerId == p.PlayerId);
                if (existed == null)
                {
                    var info = AmOwner ? iconHolder.AddPlayer(p) : null;
                    info?.SetAlpha(true);

                    var icon = new DancePlayerIcon() { info = info, Player = p, LeftTime = ForecastDurationOption, Success = false };
                    iconList.Add(icon);

                    new StaticAchievementToken("dancer.commmon8");
                }
                else if (!existed.Success)
                {
                    existed.LeftTime = ForecastDurationOption;
                }
            }
        }

        Modules.ScriptComponents.ModAbilityButton danceButton;
        int canKillLeft = 0;
        void UpdateButton()
        {
            if(canKillLeft <= 0 || nextIsFinalDance)
            {
                canKillLeft = 0;
                danceButton.SetSprite(buttonSprite.GetSprite());
                if(danceButton.UsesIcon) GameObject.Destroy(danceButton.UsesIcon);
            }
            else
            {
                danceButton.ShowUsesIcon(0).text = canKillLeft.ToString();
                danceButton.SetSprite(buttonKillSprite.GetSprite());    
            }
        }
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                iconHolder = Bind(new PlayersIconHolder());
                iconHolder.XInterval = 0.35f;

                danceCoolDownTimer = Bind(new Timer(DanceCoolDownOption)).Start(10f);
                danceCoolDownTimer.ResetsAtTaskPhase();

                danceButton = Bind(new Modules.ScriptComponents.ModAbilityButton());
                danceButton.SetSprite(buttonSprite.GetSprite());
                danceButton.Availability = (button) => !danceCoolDownTimer.IsProgressing;
                danceButton.Visibility = (button) => !MyPlayer.IsDead;
                danceButton.CoolDownTimer = new ScriptVisualTimer(
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.Percentage : (currentDance?.Percentage ?? 0f),
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.TimerText : currentDance != null ? Mathf.CeilToInt(currentDance.LeftProgress).ToString().Color(Color.cyan) : null
                    );
                danceButton.SetLabel("dance");
                danceButton.SetCanUseByMouseClick(true, ButtonEffect.ActionIconType.NonclickAction, "danceAction", false);
            }
        }


        void RuntimeAssignable.OnInactivated()
        {
            currentDance?.Destroy();
        }
        

        DanceProgress? currentDance = null;

        [Local]
        void OnHudUpdate(GameHudUpdateEvent ev)
        {
            if(currentDance != null)
            {
                NebulaGameManager.Instance?.AllPlayerInfo().Where(currentDance.Players.Test).Do(p => HighlightHelpers.SetHighlight(p, MyRole.UnityColor));
                NebulaGameManager.Instance?.AllPlayerInfo().Where(currentDance.Corpses.Test).Do(p => HighlightHelpers.SetHighlight(p.RelatedDeadBody, MyRole.UnityColor));
            }
        }

        EditableBitMask<GamePlayer> usedCorpses = BitMasks.AsPlayer();

        DancerChecker DancerChecker = null!;
        DancerChecker LocalDancerChecker = null!;
        private float pairDanceProgress = 0f;
        private AchievementToken<bool> acTokenCommon7 = new("dancer.common7", false, (val, _) => val);
        void OnUpdate(GameUpdateEvent ev)
        {
            DancerChecker.Update();
            LocalDancerChecker.Update();

            //互いに生存していて、近くでダンスを踊っている場合に進捗の進行度が進む
            if (!AmOwner && !DancerChecker.Player.IsDead && !LocalDancerChecker.Player.IsDead && DancerChecker.IsDancing && LocalDancerChecker.IsDancing &&
                LocalDancerChecker.Player.VanillaPlayer.transform.position.Distance(MyPlayer.VanillaPlayer.transform.position) < 2f)
            {
                pairDanceProgress += Time.deltaTime;
                if (pairDanceProgress > 0.8f) acTokenCommon7.Value = true;
            }
            else
            {
                pairDanceProgress = 0f;
            }

            if (AmOwner)
            {
                if (currentDance != null)
                {
                    if (currentDance.IsCompleted)
                    {
                        if (currentDance.Players.AsRawPattern != 0) new StaticAchievementToken("dancer.commmon1");
                        if (currentDance.Corpses.AsRawPattern != 0) new StaticAchievementToken("dancer.commmon2");
                        using (RPCRouter.CreateSection("Dance"))
                        {
                            var players = NebulaGameManager.Instance!.AllPlayerInfo().Where(currentDance.Players.Test).ToArray();
                            RpcDance.Invoke((MyPlayer, players));
                            foreach (var p in players)
                            {
                                if (canKillLeft <= 0) break;

                                if (!p.IsDead)
                                {
                                    MyPlayer.MurderPlayer(p, PlayerState.Frenzied, EventDetail.Dance, KillParameter.RemoteKill);
                                    canKillLeft--;
                                }
                                //自身がキルした死体は使用できないようにする。
                                usedCorpses.Add(p);
                            }
                        }
                        NebulaGameManager.Instance!.AllPlayerInfo().Where(currentDance.Corpses.Test).Do(p => { if (!usedCorpses.Test(p)) canKillLeft++; usedCorpses.Add(p); });
                        UpdateButton();
                        currentDance = null;
                        danceCoolDownTimer?.Start();
                        AmongUsUtil.PlayCustomFlash(MyRole.UnityColor, 0f, 0.8f,0.2f);
                    }
                    else if (currentDance.IsFailed)
                        currentDance = null;
                }

                if (IsDancing && !danceCoolDownTimer!.IsProgressing) currentDance ??= new DanceProgress(MyPlayer.Position);
                currentDance?.Update(IsDancing);
            }

            if (!MeetingHud.Instance && !ExileController.Instance)
            {
                foreach (var icon in iconList)
                {
                    icon.LeftTime -= Time.deltaTime;
                    icon.info?.SetText((!icon.Success && icon.LeftTime > 0f) ? Mathf.CeilToInt(icon.LeftTime).ToString() : null);
                }
            }
        }

        bool IsDancing => DancerChecker.IsDancing && !MyPlayer.IsDead;
        //成功した預言数のキャッシュ
        int successCache = 0;
        void OnPlayerDead(PlayerDieEvent ev)
        {
            if (AmOwner)
            {
                if (ev is PlayerMurderedEvent pme && pme.Player.PlayerState == PlayerState.Guessed && iconList.Any(i => i.Player == pme.Murderer))
                    new StaticAchievementToken("dancer.another1");
                if (ev is PlayerExiledEvent pee && iconList.Any(i => MeetingHudExtension.LastVotedForMap.TryGetValue(i.Player.PlayerId, out var voteFor) && voteFor == MyPlayer.PlayerId))
                    new StaticAchievementToken("dancer.another1");
            }

            //Dancerがキルされたとき、ホストがキルをトリガーする
            if (AmongUsClient.Instance.AmHost && ev.Player == MyPlayer)
            {
                using (RPCRouter.CreateSection("DancerDead"))
                {
                    var targets = iconList.Where(icon => icon.LeftTime > 0f && !icon.Player.IsDead);
                    if (ev is PlayerExiledEvent pee)
                        targets.Do(icon => icon.Player.VanillaPlayer.ModMarkAsExtraVictim(MyPlayer.VanillaPlayer, PlayerState.Suicide, PlayerState.Suicide));
                    else
                        targets.Do(icon => icon.Player.Suicide(PlayerState.Suicide, PlayerState.Suicide, Virial.Game.KillParameter.RemoteKill));
                }
            }

            var icon = iconList.FirstOrDefault(i => i.Player == ev.Player && i.LeftTime > 0f);
            if (icon != null) {
                icon.Success = true;
                icon.info?.SetText(null);
                icon.info?.SetAlpha(false);
            }

            //自身は生存している必要がある
            successCache = iconList.Count(icon => icon.Success);
            if (!MyPlayer.IsDead && successCache >= NumOfSuccessfulForecastToWinOption)
            {
                if (FinalDanceOption)
                {
                    nextIsFinalDance = true;
                    UpdateButton();
                }
                else if (NebulaAPI.CurrentGame?.LocalPlayer.AmHost ?? false)
                    NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.DancerWin, (int)WinnersMask.AsRawPattern);
            }
            
            //会議以外、自分以外のキルをDancerに通知する
            if(AmOwner && ShowDeahNotificationOption && !ev.Player.AmOwner && !MeetingHud.Instance && !ExileController.Instance && !(ev.Player.MyKiller?.AmOwner ?? false))
            {
                var noisemaker = AmongUsUtil.GetRolePrefab<NoisemakerRole>();
                if (noisemaker != null)
                {
                    GameObject gameObject = GameObject.Instantiate<GameObject>(noisemaker.deathArrowPrefab, Vector3.zero, Quaternion.identity);
                    var deathArrow = gameObject.GetComponent<NoisemakerArrow>();
                    deathArrow.SetDuration(3f);
                    deathArrow.gameObject.SetActive(true);
                    deathArrow.target = ev.Player.VanillaPlayer.transform.position;

                    deathArrow.GetComponentsInChildren<SpriteRenderer>().Do(renderer =>
                    {
                        renderer.material = new Material(NebulaAsset.HSVShader);
                        renderer.sharedMaterial.SetFloat("_Hue", 314);
                    });
                }
            }
        }

        void BlockWinning(PlayerBlockWinEvent ev)
        {
            ev.SetBlockedIf(iconList.Any(i => i.Player == ev.Player && i.LeftTime > 0f));
        }

        void ExtraWinning(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.LoversPhase) return;

            if (ev.GameEnd == NebulaGameEnd.DancerWin)
            {
                if (iconList.Any(i => i.Player == ev.Player && i.Success)) ev.IsExtraWin = true;
            }
        }

        BitMask<GamePlayer> WinnersMask { get
            {
                var mask = BitMasks.AsPlayer();
                mask.Add(MyPlayer);
                iconList.Where(i => i.Success).Do(i => mask.Add(i.Player));
                return mask;
            } }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            bool isCompleted = successCache >= (int)NumOfSuccessfulForecastToWinOption;
            var text = Language.Translate("role.dancer.taskDanceText");
            if(!isCompleted) text += $" ({successCache}/{(int)NumOfSuccessfulForecastToWinOption})";
            text = text.Color(isCompleted ? Color.green : successCache > 0 ? Color.yellow : Color.white);

            ev.AppendText(text);

            if (FinalDanceOption)
            {
                var finalText = Language.Translate("role.dancer.taskFinalText");
                if (nextIsFinalDance) finalText = finalText.Color(Color.yellow);
            }
        }

        void OnGameEnd(GameEndEvent ev)
        {
            if (AmOwner && ev.EndState.EndCondition == NebulaGameEnd.DancerWin && ev.EndState.Winners.Test(MyPlayer) && iconList.Any(i => (i.Player.IsImpostor || i.Player.Role == Jackal.MyRole) && i.Success)) new StaticAchievementToken("dancer.challenge");
            if (iconList.Any(i => i.Player.AmOwner && !i.Player.IsDead && ev.EndState.Winners.Test(i.Player))) new StaticAchievementToken("dancer.common4");
            if (iconList.Any(i => i.Player.AmOwner && i.Player.IsDead && i.Success && ev.EndState.EndCondition == NebulaGameEnd.CrewmateWin && ev.EndState.Winners.Test(i.Player))) new StaticAchievementToken("dancer.common5");
        }
    }
}