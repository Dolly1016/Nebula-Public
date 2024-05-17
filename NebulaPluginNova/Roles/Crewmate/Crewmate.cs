using Virial.Assignable;
using Virial.DI;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Crewmate : DefinedRoleTemplate, DefinedRole
{
    static public Team MyTeam = new("teams.crewmate", new(Palette.CrewmateBlue), TeamRevealType.Everyone);
    static public Crewmate MyRole = new Crewmate();

    private Crewmate() : base("crewmate", new(Palette.CrewmateBlue), RoleCategory.CrewmateRole, MyTeam) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) {}

        void RuntimeAssignable.OnActivated() {}

        
    }
}

public class CrewmateGameRule : AbstractModule<IGameModeStandard>
{
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.Player.Role.Role.Category == RoleCategory.CrewmateRole && ev.GameEnd == NebulaGameEnd.CrewmateWin);
}