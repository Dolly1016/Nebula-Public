using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Morphing : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Morphing() : base("morphing", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, MorphCoolDownOption, MorphDurationOption, LoseSampleOnMeetingOption]) { }
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[]? arguments) => new Instance(player);

    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.morphing.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.morphing.morphCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphDurationOption = NebulaAPI.Configurations.Configuration("options.role.morphing.morphDuration", (5f, 120f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.morphing.loseSampleOnMeeting", false);

    static public Morphing MyRole = new Morphing();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? sampleButton = null;
        private ModAbilityButton? morphButton = null;

        static public Image SampleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SampleButton.png", 115f);
        static public Image MorphButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MorphButton.png", 115f);
        

        StaticAchievementToken? acTokenCommon = null;
        StaticAchievementToken? acTokenAnother1 = null;
        StaticAchievementToken? acTokenAnother2 = null;
        AchievementToken<(bool kill,bool exile)>? acTokenChallenge = null;

        public Instance(GamePlayer player) : base(player)
        {
        }

        Outfit? sample = null;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("morphing.challenge", (false, false), (val, _) => val.kill && val.exile);

                PoolablePlayer? sampleIcon = null;
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "illusioner.sample");
                sampleButton.SetSprite(SampleButtonSprite.GetSprite());
                sampleButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove;
                sampleButton.Visibility = (button) => !MyPlayer.IsDead;
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker!.CurrentTarget!.GetOutfit(75);

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample.outfit, morphButton!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                sampleButton.CoolDownTimer = Bind(new Timer(SampleCoolDownOption).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("sample");

                morphButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph");
                morphButton.SetSprite(MorphButtonSprite.GetSprite());
                morphButton.Availability = (button) => MyPlayer.CanMove && sample != null;
                morphButton.Visibility = (button) => !MyPlayer.IsDead;
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
                        int colorId = MyPlayer.GetOutfit(75).outfit.ColorId;
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                        {
                            if (p.AmOwner) continue;
                            if (p.Unbox().CurrentOutfit.ColorId != colorId) continue;
                            if (p.VanillaPlayer.GetTruePosition().Distance(MyPlayer.VanillaPlayer.GetTruePosition()) < 0.8f)
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

                    if (LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                    }
                };
                morphButton.CoolDownTimer = Bind(new Timer(MorphCoolDownOption).SetAsAbilityCoolDown().Start());
                morphButton.EffectTimer = Bind(new Timer(MorphDurationOption));
                morphButton.SetLabel("morph");
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (LoseSampleOnMeetingOption) sample = null;
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (acTokenChallenge != null && ev.Player.Unbox()!.DefaultOutfit.ColorId == (sample?.outfit.ColorId ?? -1))
                acTokenChallenge.Value.exile = true;
        }

        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            var targetId = ev.Dead.Unbox()?.GetOutfit(75).ColorId;
            var sampleId = sample?.outfit.ColorId;
            if (targetId.HasValue && sampleId.HasValue && targetId.Value == sampleId.Value)
                acTokenAnother1 ??= new("morphing.another1");

            if (morphButton != null && acTokenChallenge != null && morphButton.EffectActive)
                acTokenChallenge.Value.kill = true;
        }
    }
}
