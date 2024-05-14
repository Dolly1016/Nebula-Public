using Nebula.VoiceChat;
using Virial.Assignable;
using Virial.Events.Player;

namespace Nebula.Roles.Impostor;

public class Impostor : ConfigurableStandardRole, DefinedRole
{
    static public Impostor MyRole = new Impostor();
    static public Team MyTeam = new("teams.impostor", Palette.ImpostorRed,TeamRevealType.Teams);
    RoleCategory DefinedSingleAssignable.Category => RoleCategory.ImpostorRole;

    string DefinedAssignable.LocalizedName => "impostor";
    Virial.Color DefinedAssignable.Color => new(Palette.ImpostorRed);
    public override bool IsDefaultRole => true;
    RoleTeam DefinedSingleAssignable.Team => Impostor.MyTeam;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public NebulaConfiguration CanKillHidingPlayerOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanKillHidingPlayerOption = new(RoleConfig, "canKillHidingPlayer", null, false, false);
    }

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

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) =>ev.SetWin(ev.GameEnd == NebulaGameEnd.ImpostorWin);

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if ((PlayerControl.LocalPlayer.GetModInfo() as GamePlayer)?.IsImpostor ?? false) color = Palette.ImpostorRed;
        }
    }
}
