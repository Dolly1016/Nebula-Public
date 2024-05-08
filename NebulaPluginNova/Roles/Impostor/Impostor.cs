using Nebula.VoiceChat;
using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class Impostor : ConfigurableStandardRole
{
    static public Impostor MyRole = new Impostor();
    static public Team MyTeam = new("teams.impostor", Palette.ImpostorRed,TeamRevealType.Teams);
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "impostor";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;
    public override bool IsDefaultRole => true;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public NebulaConfiguration CanKillHidingPlayerOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanKillHidingPlayerOption = new(RoleConfig, "canKillHidingPlayer", null, false, false);
    }

    public class Instance : RoleInstance
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void OnActivated()
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

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.ImpostorWin;

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if ((PlayerControl.LocalPlayer.GetModInfo() as GamePlayer)?.IsImpostor ?? false) color = Palette.ImpostorRed;
        }
    }
}
