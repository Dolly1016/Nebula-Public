using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Alien : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Alien(): base("alien", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [EMICoolDownOption, EMIDurationOption, InvalidateCoolDownOption, NumOfInvalidationsOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Alien.png");
    }
    
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration EMICoolDownOption = NebulaAPI.Configurations.Configuration("options.role.alien.emiCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EMIDurationOption = NebulaAPI.Configurations.Configuration("options.role.alien.emiDuration", (5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration InvalidateCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.alien.invalidateCoolDown", (5f,60f,2.5f),10f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration NumOfInvalidationsOption = NebulaAPI.Configurations.Configuration("options.role.alien.numOfInvalidations", (1, 10), 1);

    static public Alien MyRole = new Alien();
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EMIButton.png", 115f);
        static private Image invalidateButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AlienButton.png", 115f);
        

        public Instance(GamePlayer player) : base(player){}

        ModAbilityButton? emiButton = null;
        AchievementToken<(int playerMask,bool clear)>? achCommon4Token = null;
        AchievementToken<(int killTotal,int killOnlyMe)>? achChallengeToken = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                achChallengeToken = new("alien.challenge", (0, 0), (val, _) => val.killTotal >= 5 && val.killOnlyMe >= 3);

                emiButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                emiButton.SetSprite(buttonSprite.GetSprite());
                emiButton.Availability = (button) => MyPlayer.CanMove;
                emiButton.Visibility = (button) => !MyPlayer.IsDead;
                emiButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                emiButton.OnEffectStart = (button) =>
                {
                    using (RPCRouter.CreateSection("EMI"))
                    {
                        FakeInformation.RpcFakeAdmin.Invoke((FakeInformation.Instance!.CurrentAdmin.Players, EMIDurationOption));
                        FakeInformation.RpcFakeVitals.Invoke((FakeInformation.Instance!.CurrentVitals.Players, EMIDurationOption));
                    }

                    IEnumerator CoNoise()
                    {
                        PlayerModInfo.RpcAttrModulator.Invoke((MyPlayer.PlayerId, new FloatModulator(PlayerAttributes.Roughening, 4, 0.21f, false, 0, "nebula::alien", false),true));
                        yield return Effects.Wait(0.45f);
                        PlayerModInfo.RpcAttrModulator.Invoke((MyPlayer.PlayerId, new FloatModulator(PlayerAttributes.Roughening, 20, 0.5f, false, 0, "nebula::alien", false), true));
                        yield return Effects.Wait(0.55f);
                    }
                    NebulaManager.Instance.StartCoroutine(CoNoise().WrapToIl2Cpp());

                };
                emiButton.OnEffectEnd = (button) =>
                {
                    button.StartCoolDown();
                };
                emiButton.OnMeeting = button => button.StartCoolDown();
                emiButton.CoolDownTimer = Bind(new Timer(EMICoolDownOption).SetAsAbilityCoolDown().Start());
                emiButton.EffectTimer = Bind(new Timer(EMIDurationOption));
                emiButton.SetLabel("emi");

                if (NumOfInvalidationsOption > 0)
                {
                    achCommon4Token = new("alien.common4", (0,false),(val,_) => val.clear);

                    int left = NumOfInvalidationsOption;

                    var invalidateTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p)));
                    var invalidateButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                    invalidateButton.SetSprite(invalidateButtonSprite.GetSprite());
                    invalidateButton.Availability = (button) => MyPlayer.CanMove && invalidateTracker.CurrentTarget != null;
                    invalidateButton.Visibility = (button) => !MyPlayer.IsDead && left > 0;
                    var icon = invalidateButton.ShowUsesIcon(0);
                    icon.text = left.ToString();
                    invalidateButton.OnClick = (button) =>
                    {
                        PlayerModInfo.RpcAttrModulator.Invoke((invalidateTracker.CurrentTarget!.PlayerId, new AttributeModulator(PlayerAttributes.Isolation, 100000f, true, 0, canBeAware: invalidateTracker.CurrentTarget.IsImpostor), true));
                        left--;
                        icon.text = left.ToString();

                        new StaticAchievementToken("alien.common2");
                        achCommon4Token.Value.playerMask |= 1 << invalidateTracker.CurrentTarget.PlayerId;

                        invalidateButton.StartCoolDown();
                    };
                    invalidateButton.OnMeeting = button => button.StartCoolDown();
                    invalidateButton.CoolDownTimer = Bind(new Timer(InvalidateCoolDownOption).SetAsAbilityCoolDown().Start());
                    invalidateButton.SetLabel("invalidate");
                    
                }
            }
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (emiButton?.EffectActive ?? false) new StaticAchievementToken("alien.common1");
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (achChallengeToken != null && (emiButton?.EffectActive ?? false))
            {
                achChallengeToken.Value.killTotal++;
                if (ev.Murderer.AmOwner) achChallengeToken.Value.killOnlyMe++;
            }

            if (!ev.Murderer.AmOwner && ev.Murderer.IsImpostor)
            {
                if (emiButton?.EffectActive ?? false)
                    new StaticAchievementToken("alien.common3");
                if (((achCommon4Token?.Value.playerMask ?? 0) & (1 << ev.Dead.PlayerId)) != 0)
                    achCommon4Token!.Value.clear = true;
            }
        }
    }
}

