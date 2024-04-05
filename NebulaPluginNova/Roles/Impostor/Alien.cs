using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Alien : ConfigurableStandardRole, HasCitation
{
    static public Alien MyRole = null;//new Alien();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "alien";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EMICoolDownOption = null!;
    private NebulaConfiguration EMIDurationOption = null!;
    private NebulaConfiguration InvalidateCoolDownOption = null!;
    private NebulaConfiguration NumOfInvalidationsOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagSNR);

        EMICoolDownOption = new NebulaConfiguration(RoleConfig, "emiCoolDown", null, 5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        EMIDurationOption = new NebulaConfiguration(RoleConfig, "emiDuration", null, 5f, 40f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        InvalidateCoolDownOption = new NebulaConfiguration(RoleConfig, "invalidateCoolDown", null, 5f, 60f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        NumOfInvalidationsOption = new NebulaConfiguration(RoleConfig, "numOfInvalidations", null, 10, 1, 1);
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EMIButton.png", 115f);
        static private ISpriteLoader invalidateButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AlienButton.png", 115f);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        ModAbilityButton? emiButton = null;
        AchievementToken<(int playerMask,bool clear)>? achCommon4Token = null;
        AchievementToken<(int killTotal,int killOnlyMe)>? achChallengeToken = null;
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                achChallengeToken = new("alien.challenge", (0, 0), (val, _) => val.killTotal >= 5 && val.killOnlyMe >= 3);

                emiButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                emiButton.SetSprite(buttonSprite.GetSprite());
                emiButton.Availability = (button) => MyPlayer.MyControl.CanMove;
                emiButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                emiButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                emiButton.OnEffectStart = (button) =>
                {
                    using (RPCRouter.CreateSection("EMI"))
                    {
                        FakeInformation.RpcFakeAdmin.Invoke((FakeInformation.Instance!.CurrentAdmin.Players, MyRole.EMIDurationOption.GetFloat()));
                        FakeInformation.RpcFakeVitals.Invoke((FakeInformation.Instance!.CurrentVitals.Players, MyRole.EMIDurationOption.GetFloat()));
                    }

                };
                emiButton.OnEffectEnd = (button) =>
                {
                    button.StartCoolDown();
                };
                emiButton.OnMeeting = button => button.StartCoolDown();
                emiButton.CoolDownTimer = Bind(new Timer(MyRole.EMICoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                emiButton.EffectTimer = Bind(new Timer(MyRole.EMIDurationOption.GetFloat()));
                emiButton.SetLabel("emi");

                if (MyRole.NumOfInvalidationsOption.GetMappedInt() > 0)
                {
                    achCommon4Token = new("alien.common4", (0,false),(val,_) => val.clear);

                    int left = MyRole.NumOfInvalidationsOption.GetMappedInt();

                    var invalidateTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, p => ObjectTrackers.StandardPredicate(p)));
                    var invalidateButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                    invalidateButton.SetSprite(invalidateButtonSprite.GetSprite());
                    invalidateButton.Availability = (button) => MyPlayer.MyControl.CanMove && invalidateTracker.CurrentTarget != null;
                    invalidateButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && left > 0;
                    var icon = invalidateButton.ShowUsesIcon(0);
                    icon.text = left.ToString();
                    invalidateButton.OnClick = (button) =>
                    {
                        PlayerModInfo.RpcAttrModulator.Invoke((invalidateTracker.CurrentTarget!.PlayerId, new AttributeModulator(PlayerAttributes.Isolation, 100000f, true, 0, canBeAware: invalidateTracker.CurrentTarget.Data.Role.IsImpostor)));
                        left--;
                        icon.text = left.ToString();

                        new StaticAchievementToken("alien.common2");
                        achCommon4Token.Value.playerMask |= 1 << invalidateTracker.CurrentTarget.PlayerId;

                        invalidateButton.StartCoolDown();
                    };
                    invalidateButton.OnMeeting = button => button.StartCoolDown();
                    invalidateButton.CoolDownTimer = Bind(new Timer(MyRole.InvalidateCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                    invalidateButton.SetLabel("invalidate");
                    
                }
            }
        }

        void IGamePlayerEntity.OnKillPlayer(Virial.Game.Player target)
        {
            if (AmOwner && (emiButton?.EffectActive ?? false))
                new StaticAchievementToken("alien.common1");
        }

        void IGameEntity.OnPlayerMurdered(Virial.Game.Player dead, Virial.Game.Player murderer)
        {
            if (AmOwner)
            {
                if (achChallengeToken != null && (emiButton?.EffectActive ?? false))
                {
                    achChallengeToken.Value.killTotal++;
                    if(murderer.AmOwner) achChallengeToken.Value.killOnlyMe++;
                }

                if (!murderer.AmOwner && murderer.Role.Role.Category == RoleCategory.ImpostorRole)
                {
                    if (emiButton?.EffectActive ?? false)
                        new StaticAchievementToken("alien.common3");
                    if (((achCommon4Token?.Value.playerMask ?? 0) & (1 << dead.PlayerId)) != 0)
                        achCommon4Token!.Value.clear = true;
                }
            }
        }
    }
}

