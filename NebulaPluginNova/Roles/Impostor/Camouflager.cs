using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Camouflager : ConfigurableStandardRole, HasCitation
{
    static public Camouflager MyRole = new Camouflager();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "camouflager";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CamoCoolDownOption = null!;
    private NebulaConfiguration CamoDurationOption = null!;
    private NebulaConfiguration CanInvokeCamoAfterDeathOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        CamoCoolDownOption = new NebulaConfiguration(RoleConfig, "camoCoolDown", null, 5f, 60f, 5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        CamoDurationOption = new NebulaConfiguration(RoleConfig, "camoDuration", null, 5f, 60f, 5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        CanInvokeCamoAfterDeathOption = new NebulaConfiguration(RoleConfig, "canInvokeCamoAfterDeath", null, false, false);
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? camouflageButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CamoButton.png", 115f);
        public override AbstractRole Role => MyRole;

        private AchievementToken<bool>? acTokenCommon;
        private AchievementToken<(bool cleared, int killed)>? acTokenChallenge;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenCommon = new("camouflager.common1", false, (val, _) => val);
                acTokenChallenge = new("camouflager.challenge", (false, 0), (val, _) => val.cleared);

                camouflageButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                camouflageButton.SetSprite(buttonSprite.GetSprite());
                camouflageButton.Availability = (button) =>MyPlayer.MyControl.CanMove;
                camouflageButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead || MyRole.CanInvokeCamoAfterDeathOption;
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
                camouflageButton.CoolDownTimer = Bind(new Timer(MyRole.CamoCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                camouflageButton.EffectTimer = Bind(new Timer(MyRole.CamoDurationOption.GetFloat()));
                camouflageButton.SetLabel("camo");
            }
        }

        void IGameEntity.OnPlayerMurdered(GamePlayer dead, GamePlayer murderer)
        {
            if (AmOwner && acTokenChallenge != null) acTokenChallenge.Value.killed++;
        }

    }

    private static GameData.PlayerOutfit CamouflagerOutfit = new() { PlayerName = "", ColorId = 16, HatId = "hat_NoHat", SkinId = "skin_None", VisorId = "visor_EmptyVisor", PetId= "pet_EmptyPet" };

    public static RemoteProcess<(byte camouflagerId, bool on)> RpcCamouflage = new(
        "Camouflage",
        (message, _) =>
        {
            OutfitCandidate outfit = new("Camo" + message.camouflagerId, 100, true, CamouflagerOutfit);
            foreach(var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (message.on)
                    p.AddOutfit(outfit);
                else
                    p.RemoveOutfit(outfit.Tag);
            }
        }
        );
}
