using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Crewmate : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.crewmate", new(Palette.CrewmateBlue), TeamRevealType.Everyone);

    private Crewmate() : base("crewmate", new(Palette.CrewmateBlue), RoleCategory.CrewmateRole, MyTeam) { }

    static public readonly Crewmate MyRole = new();

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    bool ISpawnable.IsSpawnable => true;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) {}

        void RuntimeAssignable.OnActivated() {}

        
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class CrewmateGameRule : AbstractModule<IGameModeStandard>, IGameOperator
{
    static CrewmateGameRule() => DIManager.Instance.RegisterModule(() => new CrewmateGameRule());
    public CrewmateGameRule() => this.Register(NebulaAPI.CurrentGame!);
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.Player.Role.Role.Category == RoleCategory.CrewmateRole && ev.GameEnd == NebulaGameEnd.CrewmateWin);
    
}