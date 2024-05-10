using Virial.Assignable;
using Virial.Events.Player;

namespace Nebula.Roles.Crewmate;

public class Crewmate : ConfigurableStandardRole
{
    static public Crewmate MyRole = new Crewmate();
    static public Team MyTeam = new("teams.crewmate", Palette.CrewmateBlue, TeamRevealType.Everyone);

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "crewmate";
    public override Color RoleColor => Palette.CrewmateBlue;
    public override bool IsDefaultRole => true;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RoleInstance
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.CrewmateWin);
    }
}
