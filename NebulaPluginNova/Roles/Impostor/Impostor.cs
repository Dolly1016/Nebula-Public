using Nebula.VoiceChat;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Impostor : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.impostor", new(Palette.ImpostorRed), TeamRevealType.Teams);
    
    private Impostor():base("impostor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, MyTeam, [CanKillHidingPlayerOption]) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("options.role.impostor.canKillHidingPlayer", false);

    static public Impostor MyRole = new Impostor();
    bool ISpawnable.IsSpawnable => true;
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
        }
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class ImpostorGameRule : AbstractModule<IGameModeStandard>, IGameOperator
{
    static ImpostorGameRule() => DIManager.Instance.RegisterModule(() => new ImpostorGameRule());

    public ImpostorGameRule() => this.Register(NebulaAPI.CurrentGame!);
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.Player.IsImpostor && ev.GameEnd == NebulaGameEnd.ImpostorWin);  
    
    void DecoratePlayerColor(PlayerDecorateNameEvent ev)
    {
        if (ev.Player.IsImpostor && (GamePlayer.LocalPlayer?.IsImpostor ?? false)) ev.Color = new(Palette.ImpostorRed);
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class ImpostorRadioOperator : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static ImpostorRadioOperator() => DIManager.Instance.RegisterModule(() => new ImpostorRadioOperator());

    public ImpostorRadioOperator() => this.Register(NebulaAPI.CurrentGame!);
    [OnlyMyPlayer]
    void OnSetRole(PlayerRoleSetEvent ev)
    {
        if (GeneralConfigurations.ImpostorsRadioOption && ev.Role.Role.Category == RoleCategory.ImpostorRole)
        {
            VoiceChatManager.RegisterRadio(ev.Role, (p) => p.Role.Role.Category == RoleCategory.ImpostorRole, "voiceChat.info.impostorRadio", Palette.ImpostorRed);
        }
    }

}