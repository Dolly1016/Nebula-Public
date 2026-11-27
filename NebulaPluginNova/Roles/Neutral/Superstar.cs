using Nebula.Behavior;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Roles.Neutral;

[NebulaRPCHolder]
public class Superstar : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.superstar", new(240, 61, 190), TeamRevealType.OnlyMe);

    static private FloatConfiguration DanceDurationOption = NebulaAPI.Configurations.Configuration("options.role.superstar.danceDuration", (1f, 30f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DanceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.superstar.danceCoolDown", (1f, 30f, 1f), 10f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration NumOfDancesToWinOption = NebulaAPI.Configurations.Configuration("options.role.superstar.numOfDancesToWin", (4, 20), 10);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("options.role.superstar.vent", true);
    static private FloatConfiguration SpeedUpDurationOption = EffectDurationOption("speedUp");
    static private FloatConfiguration SpeedUpRatioOption = NebulaAPI.Configurations.Configuration("options.role.superstar.speedUp.ratio", (1.125f, 3f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration SpeedDownDurationOption = EffectDurationOption("speedDown");
    static private FloatConfiguration SpeedDownRatioOption = NebulaAPI.Configurations.Configuration("options.role.superstar.speedDown.ratio", (0.125f, 0.875f, 0.125f), 0.25f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration VisionDurationOption = EffectDurationOption("vision");
    static private FloatConfiguration VisionRatioOption = NebulaAPI.Configurations.Configuration("options.role.superstar.vision.ratio", (0.125f, 0.875f, 0.125f), 0.125f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration FlipDurationOption = EffectDurationOption("flip");
    static private FloatConfiguration MorphDurationOption = EffectDurationOption("morph");
    static private FloatConfiguration ScreenDurationOption = EffectDurationOption("screen");
    static private FloatConfiguration ScreenRatioOption = NebulaAPI.Configurations.Configuration("options.role.superstar.screen.ratio", (1.125f, 3f, 0.125f), 1.75f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration MosaicDurationOption = EffectDurationOption("mosaic");
    static private IntegerConfiguration MosaicStrengthOption = NebulaAPI.Configurations.Configuration("options.role.superstar.mosaic.strength", (1, 10), 5);
    static private FloatConfiguration EffectDurationOption(string effect) => NebulaAPI.Configurations.Configuration("options.role.superstar." + effect + ".duration", (1f, 30f, 1f), 10f, FloatConfigurationDecorator.Second);
    static private GroupConfiguration EffectGroup(string effect, params IConfiguration[] configurations) => new("options.role.superstar.group." + effect, configurations, GroupConfigurationColor.ToDarkenColor(MyTeam.Color));

    private Superstar() : base("superstar", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [DanceDurationOption, DanceCoolDownOption, NumOfDancesToWinOption, VentConfiguration,
        EffectGroup("speedUp", SpeedUpDurationOption, SpeedUpRatioOption),
        EffectGroup("speedDown", SpeedDownDurationOption, SpeedDownRatioOption),
        EffectGroup("vision", VisionDurationOption, VisionRatioOption),
        EffectGroup("flip", FlipDurationOption),
        EffectGroup("morph", MorphDurationOption),
        EffectGroup("screen", ScreenDurationOption, ScreenRatioOption),
        EffectGroup("mosaic", MosaicDurationOption, MosaicStrengthOption)
        ])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }

    static public Superstar MyRole = new Superstar();
    static private GameStatsEntry StatsDance = NebulaAPI.CreateStatsEntry("stats.superstar.dance", GameStatsCategory.Roles, MyRole, annotation: GameStatsAnnotation.From2611);
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonSprite, "role.superstar.ability.dance");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%TIME%", DanceDurationOption.GetValue().DecimalToString("1"));
        yield return new("%NUM%", NumOfDancesToWinOption.GetValue().ToString());
    }

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SuperstarButton.png", 100f);

    private enum Effect
    {
        SpeedUp,
        SpeedDown,
        Vision,
        Flip,
        Morph,
        Screen,
        Mosaic,
    }

    private const int NumOfEffects = 7;
    private const float DanceGraceDuration = 0.25f;
    private const string EffectTag = "superstar.effect";
    private const string MorphTag = "superstar.morph";
    private const float WitnessDistance = 3f;

    static private void GainEffectLocal(Effect effect)
    {
        var player = GamePlayer.LocalPlayer;
        if (player == null || player.IsDead) return;

        switch (effect)
        {
            case Effect.SpeedUp:
                player.GainSpeedAttribute(SpeedUpRatioOption, SpeedUpDurationOption, false, 100, EffectTag);
                break;
            case Effect.SpeedDown:
                player.GainSpeedAttribute(SpeedDownRatioOption, SpeedDownDurationOption, false, 100, EffectTag);
                break;
            case Effect.Vision:
                player.GainAttribute(PlayerAttributes.Eyesight, VisionDurationOption, VisionRatioOption, false, 100, EffectTag);
                break;
            case Effect.Flip:
                player.GainAttribute(PlayerAttributes.FlipX, FlipDurationOption, false, 100, EffectTag);
                break;
            case Effect.Screen:
                player.GainAttribute(PlayerAttributes.ScreenSize, ScreenDurationOption, ScreenRatioOption, false, 100, EffectTag);
                break;
            case Effect.Mosaic:
                player.GainAttribute(PlayerAttributes.Roughening, MosaicDurationOption, 4 + MosaicStrengthOption * 4, false, 100, EffectTag);
                break;
            default:
                player.RemoveAttributeByTag(EffectTag);
                break;
        }
    }

    private class SuperstarMorph : FlexibleLifespan, IGameOperator
    {
        private readonly GamePlayer[] players;
        private float timer;

        public SuperstarMorph(GamePlayer[] players, GamePlayer[] targets, float duration)
        {
            this.players = players;
            this.timer = duration;

            for (int i = 0; i < players.Length && i < targets.Length; i++)
                players[i].Unbox().AddOutfit(new OutfitCandidate(targets[i].DefaultOutfit, MorphTag, OutfitPriority.Morph, true));

            this.Register(NebulaAPI.CurrentGame!);
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            timer -= ev.DeltaTime;
            if (timer <= 0f) Release();
        }

        void OnMeetingStart(MeetingStartEvent ev) => Release();

        void IGameOperator.OnReleased()
        {
            foreach (var p in players) p.Unbox().RemoveOutfit(MorphTag);
        }
    }

    static private SuperstarMorph? currentMorph = null;

    static private readonly RemoteProcess<(GamePlayer superstar, int effect, GamePlayer[] morphPlayers, GamePlayer[] morphTargets)> RpcPerform = new("SuperstarPerform", (message, _) =>
    {
        var effect = (Effect)message.effect;

        if (!(currentMorph?.IsDeadObject ?? true)) currentMorph.Release();
        currentMorph = effect == Effect.Morph ? new SuperstarMorph(message.morphPlayers, message.morphTargets, MorphDurationOption) : null;

        GainEffectLocal(effect);

        var localPlayer = GamePlayer.LocalPlayer;
        var game = NebulaAPI.CurrentGame;
        if (localPlayer != null && !localPlayer.IsDead && game != null && audience?.Game != game) audience = new AudienceDance(game);
    });

    //スーパースターのダンスによる効果を受けたプレイヤーのダンスを監視する。
    private class AudienceDance : IGameOperator
    {
        public readonly Virial.Game.Game Game;
        private DanceModule? danceChecker = null;
        private float progress = 0f;
        private float notDancingProgress = 0f;
        private bool waitForStop = false;

        public AudienceDance(Virial.Game.Game game)
        {
            Game = game;
            this.Register(game);
        }

        private void Reset()
        {
            progress = 0f;
            notDancingProgress = 0f;
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            var localPlayer = GamePlayer.LocalPlayer;
            if (localPlayer == null || localPlayer.IsDead || localPlayer.Role is Instance || MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast())
            {
                Reset();
                return;
            }

            danceChecker ??= localPlayer.GetModule<DanceModule>();
            if (danceChecker == null) return;

            if (danceChecker.IsDancing && localPlayer.CanMove)
            {
                notDancingProgress = 0f;
                if (waitForStop) return;

                progress += ev.DeltaTime;
                if (progress >= DanceDurationOption)
                {
                    Reset();
                    waitForStop = true;
                    AmongUsUtil.PlayQuickFlash(MyRole.Color.AlphaMultiplied(0.5f));
                    RpcAudienceDance.Invoke(localPlayer);
                }
            }
            else
            {
                waitForStop = false;
                if (progress > 0f)
                {
                    notDancingProgress += ev.DeltaTime;
                    if (notDancingProgress >= DanceGraceDuration) Reset();
                }
            }
        }
    }

    static private AudienceDance? audience = null;

    static private readonly RemoteProcess<GamePlayer> RpcAudienceDance = new("SuperstarAudienceDance", (message, _) =>
    {
        foreach (var p in NebulaGameManager.Instance?.AllPlayerInfo ?? []) if (p.AmOwner && p.Role is Instance superstar) superstar.OnAudienceDanced(message);
    });

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private int numOfDances = 0;
        private int lastEffect = -1;
        private float danceProgress = 0f;
        private float notDancingProgress = 0f;
        private DanceModule danceChecker = null!;
        private GameTimer danceCoolDownTimer = null!;
        private ModAbilityButton danceButton = null!;

        private int dancesInTaskPhase = 0;
        private int performedEffectsMask = 0;
        private readonly HashSet<GamePlayer> audienceDancers = [];
        private readonly HashSet<GamePlayer> witnesses = [];
        private AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother1 = null;

        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
            if (arguments.Length >= 1) numOfDances = arguments[0];
        }

        int[]? RuntimeAssignable.RoleArguments => [numOfDances];

        private int LeftDances => Math.Max(0, NumOfDancesToWinOption - numOfDances);
        private void UpdateTaskProgress() => MyPlayer.Tasks.Custom(Math.Min(numOfDances, NumOfDancesToWinOption), NumOfDancesToWinOption, false);

        RoleTaskType RuntimeRole.TaskType => RoleTaskType.SpecialRoleTask;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                UpdateTaskProgress();
                acTokenAnother1 = AbstractAchievement.GenerateSimpleTriggerToken("superstar.another1");
                danceChecker = MyPlayer.GetModule<DanceModule>()!;

                danceCoolDownTimer = NebulaAPI.Modules.Timer(this, DanceCoolDownOption).SetAsAbilityTimer().ResetsAtTaskPhase();
                danceCoolDownTimer.Start();

                danceButton = NebulaAPI.Modules.AbilityButton(this);
                danceButton.SetImage(buttonSprite);
                danceButton.Availability = (button) => !danceCoolDownTimer.IsProgressing;
                danceButton.Visibility = (button) => !MyPlayer.IsDead;
                danceButton.CoolDownTimer = new ScriptVisualTimer(
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.Percentage : danceProgress / DanceDurationOption,
                    () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.TimerText : danceProgress > 0f ? Mathn.CeilToInt(DanceDurationOption - danceProgress).ToString().Color(VColor.Cyan) : ""
                    );
                danceButton.SetLabel("dance");
                danceButton.ShowUsesIcon(4, LeftDances.ToString());
                (danceButton as ModAbilityButtonImpl)?.SetCanUseByMouseClick(true, ButtonEffect.ActionIconType.NonclickAction, "danceAction", false);
                danceButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "superstar.pause");
            }
        }

        private void ResetDanceProgress()
        {
            danceProgress = 0f;
            notDancingProgress = 0f;
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            //エイドアクションを押している間は踊ってもダンスが進行しない。
            if (MyPlayer.IsDead || MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast() || danceCoolDownTimer.IsProgressing || NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.AidAction).KeyForAction)
            {
                ResetDanceProgress();
                return;
            }

            if (danceChecker.IsDancing && MyPlayer.CanMove)
            {
                notDancingProgress = 0f;
                danceProgress += ev.DeltaTime;
                if (danceProgress >= DanceDurationOption) CompleteDance();
            }
            else if (danceProgress > 0f)
            {
                notDancingProgress += ev.DeltaTime;
                if (notDancingProgress >= DanceGraceDuration) ResetDanceProgress();
            }
        }

        private void CompleteDance()
        {
            ResetDanceProgress();
            danceCoolDownTimer.Start();

            numOfDances++;
            danceButton.UpdateUsesIcon(LeftDances.ToString());
            StatsDance.Progress();
            UpdateTaskProgress();

            if (++dancesInTaskPhase >= 3) new StaticAchievementToken("superstar.common1");
            if (numOfDances == 1 && acTokenAnother1 != null) acTokenAnother1.Value.triggered = true;
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.AmOwner || p.IsDead || p.IsDisconnected) continue;
                if (p.Position.Distance(MyPlayer.Position) > WitnessDistance) continue;
                if (NebulaPhysicsHelpers.AnyShadowBetween(MyPlayer.Position.ToUnityVector(), p.Position.ToUnityVector(), out _)) continue;
                witnesses.Add(p);
            }

            PerformRandomEffect();
            AmongUsUtil.PlayQuickFlash(MyRole.Color.AlphaMultiplied(0.5f));

            ModSingleton<IWinningOpportunity>.Instance?.RpcSetOpportunity(MyTeam, Math.Min(1f, numOfDances / (float)NumOfDancesToWinOption));
            if (LeftDances <= 0) NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.SuperstarWin, 1 << MyPlayer.PlayerId, SuperstarDetails.Win);
        }

        private void PerformRandomEffect()
        {
            int selected;
            if (lastEffect == -1)
            {
                selected = System.Random.Shared.Next(NumOfEffects);
            }
            else
            {
                selected = System.Random.Shared.Next(NumOfEffects - 1);
                if (selected >= lastEffect) selected++;
            }
            lastEffect = selected;
            var effect = (Effect)selected;

            performedEffectsMask |= 1 << selected;
            if (performedEffectsMask == (1 << NumOfEffects) - 1) new StaticAchievementToken("superstar.common2");

            GamePlayer[] morphPlayers = [];
            GamePlayer[] morphTargets = [];
            if (effect == Effect.Morph)
            {
                var alive = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToArray();
                if (alive.Length >= 2)
                {
                    morphPlayers = alive;
                    morphTargets = alive.Select(p => alive.Where(target => target != p).ToArray().Random()).ToArray();
                }
            }

            RpcPerform.Invoke((MyPlayer, (int)effect, morphPlayers, morphTargets));
        }

        internal void OnAudienceDanced(GamePlayer player)
        {
            if (player != MyPlayer) audienceDancers.Add(player);
        }

        [Local]
        void OnGameStart(GameStartEvent ev) => UpdateTaskProgress();

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => dancesInTaskPhase = 0;

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother1 != null) acTokenAnother1.Value.triggered = false;
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (acTokenAnother1 != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother1.Value.isCleared |= acTokenAnother1.Value.triggered;
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.EndCondition == NebulaGameEnd.SuperstarWin && ev.CheckWin(MyPlayer) && audienceDancers.Count >= 5 && witnesses.Count >= 5) new StaticAchievementToken("superstar.challenge");
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            bool isCompleted = LeftDances <= 0;
            var text = Language.Translate("role.superstar.taskText");
            if (!isCompleted) text += $" ({numOfDances}/{(int)NumOfDancesToWinOption})";
            ev.AppendText(text.Color(isCompleted ? VColor.Green : numOfDances > 0 ? VColor.Yellow : VColor.White));
        }
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
file static class SuperstarDetails
{
    static internal CommunicableTextTag Win = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Win = preprocessor.RegisterCommunicableText("end.detail.superstar.win");
    }
}
