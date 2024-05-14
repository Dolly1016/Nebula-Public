using Virial.Assignable;
using Virial.Events.Player;

namespace Nebula.Roles.Crewmate;

public class Crewmate : DefinedRoleTemplate, DefinedRole
{
    static public Crewmate MyRole = new Crewmate();
    static public Team MyTeam = new("teams.crewmate", Palette.CrewmateBlue, TeamRevealType.Everyone);

    public Crewmate() : base("crewmate", new(Palette.CrewmateBlue), RoleCategory.CrewmateRole, MyTeam) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) {}

        void RuntimeAssignable.OnActivated() {}

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.CrewmateWin);
    }
}
