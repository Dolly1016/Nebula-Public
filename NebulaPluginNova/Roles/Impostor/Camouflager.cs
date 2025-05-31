using Il2CppSystem.Runtime.CompilerServices;
using System.Runtime.CompilerServices;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Camouflager : DefinedSingleAbilityRoleTemplate<Camouflager.Ability>, HasCitation, DefinedRole
{
    private Camouflager():base("camouflager", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CamoCoolDownOption, CamoDurationOption, CanInvokeCamoAfterDeathOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    static private readonly FloatConfiguration CamoCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoCoolDown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CamoDurationOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoDuration", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration CanInvokeCamoAfterDeathOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.canInvokeCamoAfterDeath", false);

    static public readonly Camouflager MyRole = new();
    static private readonly GameStatsEntry StatsCamo = NebulaAPI.CreateStatsEntry("stats.camouflager.camo", GameStatsCategory.Roles, MyRole);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CamoButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped) {
            if (AmOwner)
            {
                AchievementToken<(bool cleared, int killed)>? acTokenChallenge = new("camouflager.challenge", (false, 0), (val, _) => val.cleared);

                var camouflageButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    CamoCoolDownOption, CamoDurationOption, "camo", buttonSprite).SetAsUsurpableButton(this);
                if(CanInvokeCamoAfterDeathOption) camouflageButton.Visibility = (button) => !MyPlayer.IsDead || CanInvokeCamoAfterDeathOption;
                camouflageButton.OnEffectStart = (button) =>
                {
                    RpcCamouflage.Invoke(new(MyPlayer.PlayerId, true));

                    new StaticAchievementToken("camouflager.common1");
                    acTokenChallenge!.Value.killed = 0;
                    StatsCamo.Progress();
                };
                camouflageButton.OnEffectEnd = (button) =>
                {
                    RpcCamouflage.Invoke(new(MyPlayer.PlayerId, false));
                    button.StartCoolDown();

                    if (acTokenChallenge.Value.killed >= 4) acTokenChallenge!.Value.cleared = true;
                };
                GameOperatorManager.Instance?.RegisterOnReleased(() =>
                {
                    if (camouflageButton.IsInEffect) RpcCamouflage.Invoke(new(MyPlayer.PlayerId, false));
                }, camouflageButton);
                GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev => acTokenChallenge.Value.killed++, this);
            }
        }
    }

    public static RemoteProcess<(byte camouflagerId, bool on)> RpcCamouflage = new(
        "Camouflage",
        (message, _) =>
        {
            string tag = "Camo" + message.camouflagerId;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (message.on)
                    p.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 100, true));
                else
                    p.Unbox().RemoveOutfit(tag);
            }
        }
        );
}
