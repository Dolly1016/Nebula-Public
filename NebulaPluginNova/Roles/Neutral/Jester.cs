using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Jester : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public Team MyTeam = new("teams.jester", new(253,84,167), TeamRevealType.OnlyMe);
    static public Jester MyRole = new Jester();

    private Jester() : base("jester", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [CanDragDeadBodyOption, CanFixLightOption, CanFixCommsOption, VentConfiguration]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration CanDragDeadBodyOption = NebulaAPI.Configurations.Configuration("role.jester.canDragDeadBody", true);
    static private BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("role.jester.canDragDeadBody", false);
    static private BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("role.jester.canDragDeadBody", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.jester.vent", true);

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
        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;


        public Instance(GamePlayer player) : base(player)
        {
            if (CanDragDeadBodyOption) draggable = Bind(new Scripts.Draggable());
        }


        void RuntimeAssignable.OnActivated()
        {
            NebulaGameManager.Instance?.CriteriaManager.AddCriteria(JesterCriteria);

            draggable?.OnActivated(this);
            
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => draggable?.OnDead(this);
        

        void RuntimeAssignable.OnInactivated() => draggable?.OnInactivated(this);
        

        bool RuntimeAssignable.CanFixComm => CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => CanFixLightOption;


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

