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
    static public Impostor MyRole = new Impostor();
    static public Team MyTeam = new("teams.impostor", Palette.ImpostorRed,TeamRevealType.Teams);
    
    private Impostor():base("impostor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, MyTeam, [CanKillHidingPlayerOption]) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("role.impostor.canKillHidingPlayer", false);

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


        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if ((PlayerControl.LocalPlayer.GetModInfo() as GamePlayer)?.IsImpostor ?? false) color = Palette.ImpostorRed;
        }
    }
}

public class ImpostorGameRule : AbstractModule<IGameModeStandard>
{
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.Player.Role.Role.Category == RoleCategory.ImpostorRole && ev.GameEnd == NebulaGameEnd.ImpostorWin);
}