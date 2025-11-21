using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Crewmate : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.crewmate", new(Palette.CrewmateBlue), TeamRevealType.Everyone);

    private Crewmate() : base("crewmate", new(Palette.CrewmateBlue), RoleCategory.CrewmateRole, MyTeam) {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Crewmate.png");
    }

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
    public CrewmateGameRule() => this.RegisterPermanently();
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.Player.Role.Role.Category == RoleCategory.CrewmateRole && ev.GameEnd == NebulaGameEnd.CrewmateWin);

    [OnlyHost]
    void CheckExileWin(GameUpdateEvent ev)
    {
        //キル人外が生存している間は勝利できない。
        if (GameOperatorManager.Instance?.Run(new KillerTeamCallback(NebulaTeams.CrewmateTeam)).RemainingOtherTeam ?? true) return;

        NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.CrewmateWin, GameEndReason.Situation);
    }

    [OnlyHost]
    void CheckTaskWin(PlayerTaskUpdateEvent ev)
    {
        int quota = 0;
        int completed = 0;
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            if (p.IsDisconnected) continue;

            if (!p.Tasks.IsCrewmateTask) continue;
            quota += p.Tasks.Quota;
            completed += p.Tasks.TotalCompleted;
        }
        if (quota > 0) ModSingleton<IWinningOpportunity>.Instance?.RpcSetOpportunity(NebulaTeams.CrewmateTeam, (completed * 0.93f) / (float)quota);
        if (quota > 0 && quota <= completed) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.CrewmateWin, GameEndReason.Task);
    }
}