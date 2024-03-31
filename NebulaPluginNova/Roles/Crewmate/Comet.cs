using Il2CppSystem.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;


public class Comet : ConfigurableStandardRole
{
    static public Comet MyRole = new Comet();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "comet";
    public override Color RoleColor => new Color(121f / 255f, 175f / 255f, 206f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration BlazeCoolDownOption = null!;
    private NebulaConfiguration BlazeSpeedOption = null!;
    private NebulaConfiguration BlazeDurationOption = null!;
    private NebulaConfiguration BlazeVisionOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        BlazeCoolDownOption = new(RoleConfig, "blazeCoolDown", null, 5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        BlazeSpeedOption = new(RoleConfig, "blazeSpeed", null, 0.5f, 3f, 0.125f, 1.5f, 1.5f) { Decorator = NebulaConfiguration.OddsDecorator };
        BlazeDurationOption = new(RoleConfig, "blazeDuration", null, 5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        BlazeVisionOption = new(RoleConfig, "blazeVisionRate", null, 1f, 3f, 0.125f, 1.5f, 1.5f) { Decorator = NebulaConfiguration.OddsDecorator };
    }

    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player) { }

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BoostButton.png", 115f);
        private ModAbilityButton? boostButton = null;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                AchievementToken<bool> acTokenCommon = new((AmongUsUtil.CurrentMapId is 0 or 4) ? "comet.common1" : "comet.common2", false, (val, _) => val);
                AchievementToken<(Vector2 pos, bool cleared)>? acTokenCommon2 = null;

                if(MyRole.BlazeDurationOption.GetFloat() <= 15f && MyRole.BlazeSpeedOption.GetFloat() <= 2.5f)
                    acTokenCommon2 = new("comet.common3", (Vector2.zero, false), (val, _) => val.Item2);

                boostButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                boostButton.SetSprite(buttonSprite.GetSprite());
                boostButton.Availability = (button) => MyPlayer.MyControl.CanMove;
                boostButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                boostButton.OnClick = (button) => button.ActivateEffect();
                boostButton.OnEffectStart = (button) => {
                    using (RPCRouter.CreateSection("CometBlaze"))
                    {
                        PlayerModInfo.RpcSpeedModulator.Invoke(new(MyPlayer.PlayerId, new(MyRole.BlazeSpeedOption.GetFloat(), true, MyRole.BlazeDurationOption.GetFloat(), false, 100)));
                        PlayerModInfo.RpcAttrModulator.Invoke(new(MyPlayer.PlayerId, new(PlayerAttributes.Invisible, MyRole.BlazeDurationOption.GetFloat(), false, 100)));
                    }
                    acTokenCommon.Value = true;
                    if(acTokenCommon2 != null) acTokenCommon2.Value.pos = MyPlayer.MyControl.GetTruePosition();
                };
                boostButton.OnEffectEnd = (button) =>
                {
                    boostButton.StartCoolDown();

                    //緊急会議招集による移動は除外
                    if(!MeetingHud.Instance && acTokenCommon2 != null)
                        acTokenCommon2.Value.cleared |= MyPlayer.MyControl.GetTruePosition().Distance(acTokenCommon2.Value.pos) > 45f;
                };
                boostButton.CoolDownTimer = Bind(new Timer(0f, MyRole.BlazeCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                boostButton.EffectTimer = Bind(new Timer(0f, MyRole.BlazeDurationOption.GetFloat()));
                boostButton.SetLabel("blaze");
            }
        }

        public override bool IgnoreBlackout => true;

        public override void EditLightRange(ref float range)
        {
            if(boostButton?.EffectActive ?? false) range *= MyRole.BlazeVisionOption.GetFloat();
        }

        void IGameEntity.OnPlayerMurdered(GamePlayer dead, GamePlayer murderer)
        {
            if (AmOwner)
            {
                if (MyPlayer.HasAttribute(PlayerAttributes.Invisible))
                {
                    if (!Helpers.AnyNonTriggersBetween(MyPlayer.MyControl.GetTruePosition(), dead.VanillaPlayer.GetTruePosition(), out var vec) &&
                        vec.magnitude < MyRole.BlazeVisionOption.GetFloat() * 0.75f)
                        new StaticAchievementToken("comet.challenge");
                }
            }

        }
    }
}

