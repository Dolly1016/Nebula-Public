using Nebula.Configuration;
using Nebula.Modules;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jester : ConfigurableStandardRole, HasCitation
{
    static public Jester MyRole = new Jester();
    static public Team MyTeam = new("teams.jester", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    public override string LocalizedName => "jester";
    public override Color RoleColor => new Color(253f / 255f, 84f / 255f, 167f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CanDragDeadBodyOption = null!;
    private NebulaConfiguration CanFixLightOption = null!;
    private NebulaConfiguration CanFixCommsOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        CanDragDeadBodyOption = new NebulaConfiguration(RoleConfig, "canDragDeadBody", null, true, true);
        CanFixLightOption = new NebulaConfiguration(RoleConfig, "canFixLight", null, false, false);
        CanFixCommsOption = new NebulaConfiguration(RoleConfig, "canFixComms", null, false, false);

    }

    static public NebulaEndCriteria JesterCriteria = new(CustomGameMode.Standard)
    {
        OnExiled = (PlayerControl? p)=>
        {
            if(p== null) return null;

            if (p!.GetModInfo()!.Role.Role != MyRole) return null;
            return new(NebulaGameEnd.JesterWin, 1 << p.PlayerId);
        }
    };

    public class Instance : RoleInstance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        private Scripts.Draggable? draggable = null;
        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;


        public Instance(PlayerModInfo player) : base(player)
        {
            if (MyRole.CanDragDeadBodyOption) draggable = Bind(new Scripts.Draggable());
        }

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => false;


        public override void OnActivated()
        {
            NebulaGameManager.Instance?.CriteriaManager.AddCriteria(JesterCriteria);

            draggable?.OnActivated(this);
            
        }

        void IGamePlayerEntity.OnDead()
        {
            draggable?.OnDead(this);
        }

        protected override void OnInactivated()
        {
            draggable?.OnInactivated(this);
        }

        public override bool CanFixComm => MyRole.CanFixCommsOption;
        public override bool CanFixLight => MyRole.CanFixLightOption;


        StaticAchievementToken? acTokenCommon = null;
        void IGameEntity.OnVotedForMeLocal(PlayerControl[] voters)
        {
            if(voters.Length > 0 && voters.Any(v=>!v.AmOwner))
                acTokenCommon ??= new StaticAchievementToken("jester.common1");
            if (NebulaGameManager.Instance?.AllPlayerInfo().All(p => p.IsDead || voters.Any(v => v.PlayerId == p.PlayerId)) ?? false)
                new StaticAchievementToken("jester.challenge");
        }
    }
}

