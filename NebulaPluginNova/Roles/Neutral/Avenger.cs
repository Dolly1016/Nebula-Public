using Nebula.Behaviour;
using Nebula.Roles.Abilities;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Avenger : ConfigurableRole
{
    static public Avenger MyRole = new Avenger();
    static public Team MyTeam = new("teams.avenger", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;


    public override string LocalizedName => "avenger";
    public override Color RoleColor => new Color(141f / 255f, 111f / 255f, 131f / 255f);
    public override RoleTeam Team => MyTeam;

    public override int RoleCount => 0;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, (byte)arguments.Get(0, -1));

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

    public class Instance : RoleInstance, IGamePlayerEntity
    {
        private Timer ventCoolDown = new Timer(MyRole.VentOption.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentOption.Duration);
        private bool canUseVent = MyRole.VentOption.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;

        public override AbstractRole Role => MyRole;

        private PlayerModInfo? target;
        public PlayerModInfo? AvengerTarget => target;
        public Instance(PlayerModInfo player,byte targetId) : base(player)
        {
            target = NebulaGameManager.Instance?.GetModPlayerInfo(targetId);
        }

        public override int[]? GetRoleArgument() => [target?.PlayerId ?? 255];
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, ObjectTrackers.StandardPredicate));

                var killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                killButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                killButton.OnClick = (button) => {
                    MyPlayer.MyControl.ModKill(killTracker.CurrentTarget!, true, PlayerState.Dead, EventDetail.Kill);
                    killButton.StartCoolDown();
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());

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
        (GameEnd end, GameEndReason reason)? IGameEntity.OnCheckGameEnd(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason gameEndReason)
        {
            //復讐対象が自身の手によって死亡、かつ自身は生存
            if (CheckKillCondition) return (NebulaGameEnd.AvengerWin, GameEndReason.Special);
            return null;
        }

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong extraWinMask)
        {
            return endCondition == NebulaGameEnd.AvengerWin && CheckKillCondition;
        }

        void IGamePlayerEntity.OnMurdered(Virial.Game.Player murder)
        {
            if (murder == target)
            {
                if (murder.AmOwner) new StaticAchievementToken("avenger.common2");
                if (AmOwner) new StaticAchievementToken("avenger.another1");
            }
        }

        void IGamePlayerEntity.OnDead()
        {
            if(AmOwner && CheckKillCondition) new StaticAchievementToken("avenger.another2");
        }

        void IGamePlayerEntity.OnKillPlayer(Virial.Game.Player target)
        {
            if(AmOwner && target == this.target) new StaticAchievementToken("avenger.common1");
        }

        void IGameEntity.OnPlayerExiled(Virial.Game.Player exiled)
        {
            if (AmOwner && target == exiled)
            {
                MyPlayer.MyControl.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
            }

        }
        public override void OnGameEnd(NebulaEndState endState)
        {
            if(endState.EndCondition == NebulaGameEnd.AvengerWin && endState.CheckWin(MyPlayer.PlayerId))
            {
                if(MyPlayer.GetModifiers<Lover.Instance>().Any(l => l.MyLover?.Role.Role is Avenger)) new StaticAchievementToken("avenger.challenge");
            }
        }

        void IGameEntity.OnPlayerDead(Virial.Game.Player dead)
        {
            if (AmOwner && !CheckKillCondition && dead == target)
            {
                MyPlayer.MyControl.ModSuicide(false, PlayerState.Suicide, EventDetail.Kill);
                new StaticAchievementToken("avenger.another1");
            }
        }
    }
}
