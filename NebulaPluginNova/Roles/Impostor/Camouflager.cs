using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Camouflager : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Camouflager():base("camouflager", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CamoCoolDownOption, CamoDurationOption, CanInvokeCamoAfterDeathOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration CamoCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoCoolDown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration CamoDurationOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoDuration", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanInvokeCamoAfterDeathOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.canInvokeCamoAfterDeath", false);

    static public Camouflager MyRole = new Camouflager();
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? camouflageButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CamoButton.png", 115f);

        private AchievementToken<bool>? acTokenCommon;
        private AchievementToken<(bool cleared, int killed)>? acTokenChallenge;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenCommon = new("camouflager.common1", false, (val, _) => val);
                acTokenChallenge = new("camouflager.challenge", (false, 0), (val, _) => val.cleared);

                camouflageButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                camouflageButton.SetSprite(buttonSprite.GetSprite());
                camouflageButton.Availability = (button) =>MyPlayer.CanMove;
                camouflageButton.Visibility = (button) => !MyPlayer.IsDead || CanInvokeCamoAfterDeathOption;
                camouflageButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                camouflageButton.OnEffectStart = (button) =>
                {
                    RpcCamouflage.Invoke(new(MyPlayer.PlayerId,true));

                    acTokenCommon!.Value = true;
                    acTokenChallenge!.Value.killed = 0;
                    
                };
                camouflageButton.OnEffectEnd = (button) =>
                {
                    RpcCamouflage.Invoke(new(MyPlayer.PlayerId,false));
                    button.StartCoolDown();

                    if(acTokenChallenge!.Value.killed >= 4)
                    {
                        acTokenChallenge!.Value.cleared = true;
                    }
                };
                camouflageButton.OnMeeting = button => button.StartCoolDown();
                camouflageButton.CoolDownTimer = Bind(new Timer(CamoCoolDownOption).SetAsAbilityCoolDown().Start());
                camouflageButton.EffectTimer = Bind(new Timer(CamoDurationOption));
                camouflageButton.SetLabel("camo");
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (AmOwner && acTokenChallenge != null) acTokenChallenge.Value.killed++;
        }

    }

    private static NetworkedPlayerInfo.PlayerOutfit CamouflagerOutfit = new() { PlayerName = "", ColorId = 16, HatId = "hat_NoHat", SkinId = "skin_None", VisorId = "visor_EmptyVisor", PetId= "pet_EmptyPet" };

    public static RemoteProcess<(byte camouflagerId, bool on)> RpcCamouflage = new(
        "Camouflage",
        (message, _) =>
        {
            OutfitCandidate outfit = new("Camo" + message.camouflagerId, 100, true, CamouflagerOutfit, []);
            foreach(var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (message.on)
                    p.Unbox().AddOutfit(outfit);
                else
                    p.Unbox().RemoveOutfit(outfit.Tag);
            }
        }
        );
}
