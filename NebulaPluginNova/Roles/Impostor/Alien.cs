using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Alien : DefinedSingleAbilityRoleTemplate<Alien.Ability>, HasCitation, DefinedRole
{
    private Alien(): base("alien", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [EMICoolDownOption, EMIDurationOption, InvalidateCoolDownOption, NumOfInvalidationsOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Alien.png");
    }
    
    Citation? HasCitation.Citation => Citations.SuperNewRoles;


    static private readonly FloatConfiguration EMICoolDownOption = NebulaAPI.Configurations.Configuration("options.role.alien.emiCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EMIDurationOption = NebulaAPI.Configurations.Configuration("options.role.alien.emiDuration", (5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration InvalidateCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.alien.invalidateCoolDown", (5f,60f,2.5f),10f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfInvalidationsOption = NebulaAPI.Configurations.Configuration("options.role.alien.numOfInvalidations", (1, 10), 1);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Alien MyRole = new();
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EMIButton.png", 115f);
        static private readonly Image invalidateButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AlienButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                AchievementToken<(int killTotal, int killOnlyMe)>  achChallengeToken = new("alien.challenge", (0, 0), (val, _) => val.killTotal >= 5 && val.killOnlyMe >= 3);

                var emiButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    EMICoolDownOption, EMIDurationOption, "emi", buttonSprite);
                emiButton.OnEffectStart = (button) =>
                {
                    using (RPCRouter.CreateSection("EMI"))
                    {
                        FakeInformation.RpcFakeAdmin.Invoke((FakeInformation.Instance!.CurrentAdmin.Players, EMIDurationOption));
                        FakeInformation.RpcFakeVitals.Invoke((FakeInformation.Instance!.CurrentVitals.Players, EMIDurationOption));
                    }

                    IEnumerator CoNoise()
                    {
                        MyPlayer.GainAttribute(PlayerAttributes.Roughening, 0.21f, 4f, false, 0, "nebula::alien");
                        yield return Effects.Wait(0.45f);
                        MyPlayer.GainAttribute(PlayerAttributes.Roughening, 0.5f, 20f, false, 0, "nebula::alien");
                        yield return Effects.Wait(0.55f);
                    }
                    NebulaManager.Instance.StartCoroutine(CoNoise().WrapToIl2Cpp());

                };
                emiButton.OnEffectEnd = (button) =>
                {
                    button.StartCoolDown();
                };
                emiButton.SetAsUsurpableButton(this);

                AchievementToken<(int playerMask, bool clear)> achCommon4Token = new("alien.common4", (0, false), (val, _) => val.clear);
                if (NumOfInvalidationsOption > 0)
                {
                    int left = NumOfInvalidationsOption;

                    var invalidateTracker = ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p)).Register(this);
                    var invalidateButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility,
                        InvalidateCoolDownOption, "invalidate", invalidateButtonSprite,
                        _ => invalidateTracker.CurrentTarget != null, _ => left > 0);
                    invalidateButton.ShowUsesIcon(0, left.ToString());
                    invalidateButton.OnClick = (button) =>
                    {
                        PlayerModInfo.RpcAttrModulator.Invoke((invalidateTracker.CurrentTarget!.PlayerId, new AttributeModulator(PlayerAttributes.Isolation, 100000f, true, 0, canBeAware: invalidateTracker.CurrentTarget.IsImpostor), true));
                        left--;
                        button.UpdateUsesIcon(left.ToString());

                        new StaticAchievementToken("alien.common2");
                        achCommon4Token.Value.playerMask |= 1 << invalidateTracker.CurrentTarget.PlayerId;

                        invalidateButton.StartCoolDown();
                    };
                    invalidateButton.SetAsUsurpableButton(this);
                }

                GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
                {
                    if (ev.Player.AmOwner && emiButton.IsInEffect) new StaticAchievementToken("alien.common1");
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev =>
                {
                    if (emiButton.IsInEffect)
                    {
                        achChallengeToken.Value.killTotal++;
                        if (ev.Murderer.AmOwner) achChallengeToken.Value.killOnlyMe++;
                    }

                    if (!ev.Murderer.AmOwner && ev.Murderer.IsImpostor)
                    {
                        if (emiButton.IsInEffect)
                            new StaticAchievementToken("alien.common3");
                        if ((achCommon4Token.Value.playerMask & (1 << ev.Dead.PlayerId)) != 0)
                            achCommon4Token.Value.clear = true;
                    }
                }, this);
            }
        }
    }
}

