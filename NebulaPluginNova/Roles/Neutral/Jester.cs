using Virial.Assignable;
using Virial.Components;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jester : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public Jester MyRole = new Jester();
    static public Team MyTeam = new("teams.jester", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    string DefinedAssignable.LocalizedName => "jester";
    public override Color RoleColor => new Color(253f / 255f, 84f / 255f, 167f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => MyTeam;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CanDragDeadBodyOption = null!;
    private NebulaConfiguration CanFixLightOption = null!;
    private NebulaConfiguration CanFixCommsOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();
        
        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

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

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        private Scripts.Draggable? draggable = null;
        private GameTimer ventCoolDown = (new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;


        public Instance(GamePlayer player) : base(player)
        {
            if (MyRole.CanDragDeadBodyOption) draggable = Bind(new Scripts.Draggable());
        }


        void RuntimeAssignable.OnActivated()
        {
            NebulaGameManager.Instance?.CriteriaManager.AddCriteria(JesterCriteria);

            draggable?.OnActivated(this);
            
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => draggable?.OnDead(this);
        

        void RuntimeAssignable.OnInactivated() => draggable?.OnInactivated(this);
        

        bool RuntimeAssignable.CanFixComm => MyRole.CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => MyRole.CanFixLightOption;


        StaticAchievementToken? acTokenCommon = null;
        [OnlyMyPlayer]
        void OnVotedForMeLocal(PlayerVotedLocalEvent ev)
        {
            if (ev.Voters.Any(v => !(v.AmOwner)))
                acTokenCommon ??= new StaticAchievementToken("jester.common1");
            if (NebulaGameManager.Instance?.AllPlayerInfo().All(p => p.IsDead || ev.Voters.Any(v => v.PlayerId == p.PlayerId)) ?? false)
                new StaticAchievementToken("jester.challenge");

        }
    }
}

