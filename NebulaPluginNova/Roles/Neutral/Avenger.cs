using Nebula.Roles.Abilities;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Avenger : ConfigurableRole, DefinedRole
{
    static public Avenger MyRole = new Avenger();
    static public Team MyTeam = new("teams.avenger", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;


    string DefinedAssignable.LocalizedName => "avenger";
    public override Color RoleColor => new Color(141f / 255f, 111f / 255f, 131f / 255f);
    public override RoleTeam Team => MyTeam;

    public override int RoleCount => 0;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, (byte)arguments.Get(0, -1));

    public NebulaConfiguration CanKnowExistanceOfAvengerOption = null!;
    private NebulaConfiguration TargetCanKnowAvengerOption = null!;
    private NebulaConfiguration AvengerFlashForMurdererOption = null!;
    private NebulaConfiguration NotificationForAvengerIntervalOption = null!;
    private NebulaConfiguration NotificationForMurdererIntervalOption = null!;
    private KillCoolDownConfiguration KillCoolDownOption = null!;
    private VentConfiguration VentOption = null!;

    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return Lover.MyRole; }

    protected override void LoadOptions()
    {
        base.LoadOptions();

        KillCoolDownOption = new(RoleConfig, "killCoolDown", KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        CanKnowExistanceOfAvengerOption = new(RoleConfig, "canKnowExistanceOfAvenger", null, ["options.role.avenger.canKnowExistanceOfAvenger.off", "options.role.avenger.canKnowExistanceOfAvenger.onlyTarget", "options.role.avenger.canKnowExistanceOfAvenger.on"], 0, 0);
        TargetCanKnowAvengerOption = new(RoleConfig, "targetCanKnowAvenger", null, true, true);
        AvengerFlashForMurdererOption = new(RoleConfig, "showAvengerFlashForTarget", null, true, true);
        NotificationForAvengerIntervalOption = new(RoleConfig, "notificationForAvengerInterval", null, 2.5f, 60f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        NotificationForMurdererIntervalOption = new(RoleConfig, "notificationForTargetInterval", null, 2.5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator, Predicate = () => TargetCanKnowAvengerOption };
        VentOption = new(RoleConfig,null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);

        RoleConfig.SetPredicate(() => (Modifier.Lover.MyRole.RoleConfig.IsActivated?.Invoke() ?? false) && Modifier.Lover.MyRole.AvengerModeOption);
    }

    public override float GetRoleChance(int count) => 0f;

    public override bool CanBeGuessDefault => false;

    public class Instance : RoleInstance, RuntimeRole
    {
        private GameTimer ventCoolDown = (new Timer(MyRole.VentOption.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(MyRole.VentOption.Duration);
        private bool canUseVent = MyRole.VentOption.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;

        public override AbstractRole Role => MyRole;

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
                NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText("You became AVENGER.", MyRole.RoleColor, 5.5f, true);
                AmongUsUtil.PlayCustomFlash(MyRole.RoleColor, 0f, 0.8f, 0.7f);

                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                var killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead;
                killButton.OnClick = (button) => {
                    MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill);
                    killButton.StartCoolDown();
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
                killButton.SetLabel("kill");

                if (target != null) Bind(new TrackingArrowAbility(target, MyRole.NotificationForAvengerIntervalOption.GetFloat(), MyRole.RoleColor)).Register();
            }

            if (target?.AmOwner ?? false)
            {
                if (MyRole.TargetCanKnowAvengerOption) Bind(new TrackingArrowAbility(MyPlayer, MyRole.NotificationForMurdererIntervalOption.GetFloat(), MyRole.RoleColor)).Register();
                if (MyRole.AvengerFlashForMurdererOption) AmongUsUtil.PlayFlash(MyRole.RoleColor);
            }
        }

        private bool KillCondition => (target?.IsDead ?? false) && target?.MyKiller == MyPlayer;
        private bool CheckKillCondition => KillCondition && !MyPlayer.IsDead;
        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            if (CheckKillCondition) ev.TryOverwriteEnd(NebulaGameEnd.AvengerWin, GameEndReason.Special);
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.AvengerWin && CheckKillCondition);
        

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
            if (!CheckKillCondition && ev.Player == target && !MyPlayer.IsDead)
            {
                MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, false);
                new StaticAchievementToken("avenger.another1");
            }
        }
    }
}
