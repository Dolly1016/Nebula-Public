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
    static public RoleTeam MyTeam = new Team("teams.impostor", new(Palette.ImpostorRed), TeamRevealType.Teams);
    
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
            if (AmOwner)
            {
                if (GeneralConfigurations.ImpostorsRadioOption) {
                    VoiceChatRadio impostorRadio = new((p) => p.Role.Role.Category == RoleCategory.ImpostorRole, Language.Translate("voiceChat.info.impostorRadio"), Palette.ImpostorRed);
                    Bind(new NebulaGameScript() {
                        OnActivatedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(impostorRadio) ,
                        OnReleasedEvent = ()=> NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(impostorRadio)
                    });
                }
            }
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
        if (ev.Player.IsImpostor && (NebulaGameManager.Instance?.LocalPlayerInfo.IsImpostor ?? false)) ev.Color = new(Palette.ImpostorRed);
    }
}