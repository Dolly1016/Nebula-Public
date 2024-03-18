using Nebula.Configuration;
using Nebula.Roles.Impostor;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Il2CppSystem.Net.NetworkInformation;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Morphing : ConfigurableStandardRole, HasCitation
{
    static public Morphing MyRole = new Morphing();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "morphing";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[]? arguments) => new Instance(player);

    private NebulaConfiguration SampleCoolDownOption = null!;
    private NebulaConfiguration MorphCoolDownOption = null!;
    private NebulaConfiguration MorphDurationOption = null!;
    private NebulaConfiguration LoseSampleOnMeetingOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        SampleCoolDownOption = new NebulaConfiguration(RoleConfig, "sampleCoolDown", null, 0f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        MorphCoolDownOption = new NebulaConfiguration(RoleConfig, "morphCoolDown", null, 0f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        MorphDurationOption = new NebulaConfiguration(RoleConfig, "morphDuration", null, 5f, 120f, 2.5f, 25f, 25f) { Decorator = NebulaConfiguration.SecDecorator };
        LoseSampleOnMeetingOption = new NebulaConfiguration(RoleConfig, "loseSampleOnMeeting", null, false, false);
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? sampleButton = null;
        private ModAbilityButton? morphButton = null;

        static public ISpriteLoader SampleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SampleButton.png", 115f);
        static public ISpriteLoader MorphButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MorphButton.png", 115f);
        public override AbstractRole Role => MyRole;

        StaticAchievementToken? acTokenCommon = null;
        StaticAchievementToken? acTokenAnother1 = null;
        StaticAchievementToken? acTokenAnother2 = null;
        AchievementToken<(bool kill,bool exile)>? acTokenChallenge = null;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        GameData.PlayerOutfit? sample = null;

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenChallenge = new("morphing.challenge", (false, false), (val, _) => val.kill && val.exile);

                PoolablePlayer? sampleIcon = null;
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, ObjectTrackers.StandardPredicate));

                sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                sampleButton.SetSprite(SampleButtonSprite.GetSprite());
                sampleButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                sampleButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker!.CurrentTarget!.GetModInfo()?.GetOutfit(75);

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample, morphButton!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                sampleButton.CoolDownTimer = Bind(new Timer(MyRole.SampleCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("sample");

                morphButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                morphButton.SetSprite(MorphButtonSprite.GetSprite());
                morphButton.Availability = (button) => MyPlayer.MyControl.CanMove && sample != null;
                morphButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                morphButton.OnClick = (button) => {
                    button.ToggleEffect();
                };
                morphButton.OnEffectStart = (button) =>
                {
                    PlayerModInfo.RpcAddOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, new("Morphing", 50, true, sample!)));
                    acTokenCommon ??= new("morphing.common1");
                };
                morphButton.OnEffectEnd = (button) =>
                {
                    PlayerModInfo.RpcRemoveOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, "Morphing"));
                    morphButton.CoolDownTimer?.Start();
                };
                morphButton.OnUpdate = (button) =>
                {
                    //すれ違いチェック
                    if (button.EffectActive && acTokenAnother2 == null)
                    {
                        int colorId = MyPlayer.GetOutfit(75).ColorId;
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                        {
                            if (p.AmOwner) continue;
                            if (p.CurrentOutfit.ColorId != colorId) continue;
                            if (p.MyControl.GetTruePosition().Distance(MyPlayer.MyControl.GetTruePosition()) < 0.8f)
                            {
                                acTokenAnother2 ??= new("morphing.another2");
                                break;
                            }
                        }
                    }
                };
                morphButton.OnMeeting = (button) =>
                {
                    morphButton.InactivateEffect();

                    if (MyRole.LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                    }
                };
                morphButton.CoolDownTimer = Bind(new Timer(MyRole.MorphCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                morphButton.EffectTimer = Bind(new Timer(MyRole.MorphDurationOption.GetFloat()));
                morphButton.SetLabel("morph");
            }
        }

        void IGameEntity.OnMeetingEnd()
        {
            if (MyRole.LoseSampleOnMeetingOption) sample = null;
        }

        void IGameEntity.OnPlayerExiled(GamePlayer exiled)
        {
            if (AmOwner)
            {
                if (acTokenChallenge != null && exiled.Unbox()!.DefaultOutfit.ColorId == (sample?.ColorId ?? -1))
                    acTokenChallenge.Value.exile = true;
            }
        }

        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
        {
            var targetId = target.Unbox()?.GetOutfit(75).ColorId;
            var sampleId = sample?.ColorId;
            if (targetId.HasValue && sampleId.HasValue && targetId.Value == sampleId.Value)
                acTokenAnother1 ??= new("morphing.another1");

            if (morphButton != null && acTokenChallenge != null && morphButton.EffectActive)
                acTokenChallenge.Value.kill = true;
        }
    }
}
