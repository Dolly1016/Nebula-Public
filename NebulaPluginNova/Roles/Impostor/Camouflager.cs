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

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    static private FloatConfiguration CamoCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoCoolDown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration CamoDurationOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.camoDuration", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanInvokeCamoAfterDeathOption = NebulaAPI.Configurations.Configuration("options.role.camouflager.canInvokeCamoAfterDeath", false);

    static public Camouflager MyRole = new Camouflager();
    static private GameStatsEntry StatsCamo = NebulaAPI.CreateStatsEntry("stats.camouflager.camo", GameStatsCategory.Roles, MyRole);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    bool DefinedRole.IsJackalizable => true;


    public class Ability : AbstractPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CamoButton.png", 115f);

        private AchievementToken<(bool cleared, int killed)>? acTokenChallenge;
        public Ability(GamePlayer player) : base(player) {
            if (AmOwner)
            {
                acTokenChallenge = new("camouflager.challenge", (false, 0), (val, _) => val.cleared);

                var camouflageButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                camouflageButton.SetSprite(buttonSprite.GetSprite());
                camouflageButton.Availability = (button) => MyPlayer.CanMove;
                camouflageButton.Visibility = (button) => !MyPlayer.IsDead || CanInvokeCamoAfterDeathOption;
                camouflageButton.OnClick = (button) =>
                {
                    button.ActivateEffect();
                };
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

                    if (acTokenChallenge!.Value.killed >= 4)
                    {
                        acTokenChallenge!.Value.cleared = true;
                    }
                };
                camouflageButton.OnMeeting = button => button.StartCoolDown();
                camouflageButton.CoolDownTimer = Bind(new Timer(CamoCoolDownOption).SetAsAbilityCoolDown().Start());
                camouflageButton.EffectTimer = Bind(new Timer(CamoDurationOption));
                camouflageButton.SetLabel("camo");
                camouflageButton.OnReleased = button =>
                {
                    if (button.EffectActive) RpcCamouflage.Invoke(new(MyPlayer.PlayerId, false));
                };
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (AmOwner && acTokenChallenge != null) acTokenChallenge.Value.killed++;
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
