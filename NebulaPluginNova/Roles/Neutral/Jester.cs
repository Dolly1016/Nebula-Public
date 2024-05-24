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
    static public RoleTeam MyTeam = new Team("teams.jester", new(253,84,167), TeamRevealType.OnlyMe);

    private Jester() : base("jester", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [VentConfiguration, CanDragDeadBodyOption, CanFixLightOption, CanFixCommsOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration CanDragDeadBodyOption = NebulaAPI.Configurations.Configuration("options.role.jester.canDragDeadBody", true);
    static private BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("options.role.jester.canFixLight", false);
    static private BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("options.role.jester.canFixComms", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.jester.vent", true);

    static public Jester MyRole = new Jester();

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

