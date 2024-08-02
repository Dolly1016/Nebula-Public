using Nebula.Compat;
using Nebula.Game.Statistics;
using Nebula.Roles.Abilities;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Avenger : DefinedRoleTemplate, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.avenger", new(141,111,131), TeamRevealType.OnlyMe);
    private Avenger() : base("avenger", MyTeam.Color, RoleCategory.NeutralRole, MyTeam,
        [KillCoolDownOption, VentOption, CanKnowExistanceOfAvengerOption, TargetCanKnowAvengerOption, AvengerFlashForMurdererOption, NotificationForMurdererIntervalOption, NotificationForAvengerIntervalOption],
        false, optionHolderPredicate: () => (Modifier.Lover.NumOfPairsOption > 0 && Modifier.Lover.AvengerModeOption))
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Modifier.Lover.MyRole.ConfigurationHolder!]);
    }

    AllocationParameters? DefinedSingleAssignable.AllocationParameters => null;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, (byte)arguments.Get(0, -1));

    static public ValueConfiguration<int> CanKnowExistanceOfAvengerOption = NebulaAPI.Configurations.Configuration("options.role.avenger.canKnowExistanceOfAvenger", ["options.role.avenger.canKnowExistanceOfAvenger.off", "options.role.avenger.canKnowExistanceOfAvenger.onlyTarget", "options.role.avenger.canKnowExistanceOfAvenger.on"], 0);
    static private BoolConfiguration TargetCanKnowAvengerOption = NebulaAPI.Configurations.Configuration("options.role.avenger.targetCanKnowAvenger", true);
    static private BoolConfiguration AvengerFlashForMurdererOption = NebulaAPI.Configurations.Configuration("options.role.avenger.showAvengerFlashForTarget", true);
    static private FloatConfiguration NotificationForAvengerIntervalOption = NebulaAPI.Configurations.Configuration("options.role.avenger.notificationForAvengerInterval", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration NotificationForMurdererIntervalOption = NebulaAPI.Configurations.Configuration("options.role.avenger.notificationForTargetInterval", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.avenger.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static private IVentConfiguration VentOption = NebulaAPI.Configurations.NeutralVentConfiguration("options.role.avenger.vent", false);

    static public Avenger MyRole = new Avenger();

    bool IGuessed.CanBeGuessDefault => false;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private GameTimer ventCoolDown = (new Timer(VentOption.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentOption.Duration);
        private bool canUseVent = VentOption.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;


        private GamePlayer? target;
        public GamePlayer? AvengerTarget => target;
        public Instance(GamePlayer player,byte targetId) : base(player)
        {
            target = NebulaGameManager.Instance?.GetPlayer(targetId);
        }

        int[]? RuntimeAssignable.RoleArguments => [target?.PlayerId ?? 255];
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText("You became AVENGER.", MyRole.RoleColor.ToUnityColor(), 5.5f, true);
                AmongUsUtil.PlayCustomFlash(MyRole.RoleColor.ToUnityColor(), 0f, 0.8f, 0.7f);

                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                var killButton = Bind(new Modules.ScriptComponents.ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) => {
                    MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    killButton.StartCoolDown();
                };
                killButton.CoolDownTimer = Bind(new Timer(KillCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                if (target != null) Bind(new TrackingArrowAbility(target, NotificationForAvengerIntervalOption, MyRole.RoleColor.ToUnityColor())).Register();
            }

            if (target?.AmOwner ?? false)
            {
                if (TargetCanKnowAvengerOption) Bind(new TrackingArrowAbility(MyPlayer, NotificationForMurdererIntervalOption, MyRole.RoleColor.ToUnityColor())).Register();
                if (AvengerFlashForMurdererOption) AmongUsUtil.PlayFlash(MyRole.RoleColor.ToUnityColor());
            }
        }

        private bool KillCondition => (target?.IsDead ?? false) && target?.MyKiller == MyPlayer;
        private bool CheckKillCondition => KillCondition && !MyPlayer.IsDead;
        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            if (CheckKillCondition) ev.TryOverwriteEnd(NebulaGameEnd.AvengerWin, GameEndReason.Special);
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.AvengerWin && CheckKillCondition);
        

        [OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer == target)
            {
                if (ev.Murderer.AmOwner) new StaticAchievementToken("avenger.common2");
                if (AmOwner) new StaticAchievementToken("avenger.another1");
            }
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if(CheckKillCondition) new StaticAchievementToken("avenger.another2");
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if(ev.Dead == this.target) new StaticAchievementToken("avenger.common1");
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (target == ev.Player && !MyPlayer.IsDead)
                MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndCondition == NebulaGameEnd.AvengerWin && ev.EndState.Winners.Test(MyPlayer))
            {
                if(MyPlayer.Unbox().GetModifiers<Lover.Instance>().Any(l => l.MyLover?.Role.Role is Avenger)) new StaticAchievementToken("avenger.challenge");
            }
        }

        [Local]
        void OnTargetDead(PlayerDieEvent ev)
        {
            if (!CheckKillCondition && ev.Player == target && !MyPlayer.IsDead && ev is PlayerMurderedEvent)
            {
                if (MeetingHud.Instance || ExileController.Instance)
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
                }
                else
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                }
                new StaticAchievementToken("avenger.another1");

            }
        }
    }
}
