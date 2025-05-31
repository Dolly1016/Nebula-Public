using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Morphing : DefinedSingleAbilityRoleTemplate<Morphing.Ability>, HasCitation, DefinedRole
{
    private Morphing() : base("morphing", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, MorphCoolDownOption, MorphDurationOption, LoseSampleOnMeetingOption]) { }
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.morphing.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.morphing.morphCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphDurationOption = NebulaAPI.Configurations.Configuration("options.role.morphing.morphDuration", (5f, 120f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.morphing.loseSampleOnMeeting", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public Morphing MyRole = new Morphing();
    static private GameStatsEntry StatsSample = NebulaAPI.CreateStatsEntry("stats.morphing.sample", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsMorph = NebulaAPI.CreateStatsEntry("stats.morphing.morph", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? sampleButton = null;
        private ModAbilityButtonImpl? morphButton = null;

        static public Image SampleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SampleButton.png", 115f);
        static public Image MorphButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MorphButton.png", 115f);
        

        StaticAchievementToken? acTokenCommon = null;
        StaticAchievementToken? acTokenAnother1 = null;
        StaticAchievementToken? acTokenAnother2 = null;
        AchievementToken<(bool kill,bool exile)>? acTokenChallenge = null;

        OutfitDefinition? sample = null;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped): base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenChallenge = new("morphing.challenge", (false, false), (val, _) => val.kill && val.exile);

                PoolablePlayer? sampleIcon = null;
                var sampleTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate).Register(this);

                ModAbilityButton morphButton = null!;
                var sampleButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "illusioner.sample",
                    SampleCoolDownOption, "sample", SampleButtonSprite,
                    _ => sampleTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker!.CurrentTarget!.GetOutfit(75);

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample.outfit, (morphButton as ModAbilityButtonImpl)!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                    StatsSample.Progress();
                };

                morphButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph",
                    MorphCoolDownOption, MorphDurationOption, "morph", MorphButtonSprite,
                    _ => sample != null, isToggleEffect: true).SetAsUsurpableButton(this);
                morphButton.OnEffectStart = (button) =>
                {
                    PlayerModInfo.RpcAddOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, new(sample!, "Morphing", 50, true)));
                    acTokenCommon ??= new("morphing.common1");
                    StatsMorph.Progress();
                };
                morphButton.OnEffectEnd = (button) =>
                {
                    PlayerModInfo.RpcRemoveOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, "Morphing"));
                    morphButton.CoolDownTimer?.Start();
                };
                morphButton.OnUpdate = (button) =>
                {
                    //すれ違いチェック
                    if (button.IsInEffect && acTokenAnother2 == null)
                    {
                        int colorId = MyPlayer.GetOutfit(75).outfit.ColorId;
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                        {
                            if (p.AmOwner) continue;
                            if (p.Unbox().CurrentOutfit.Outfit.outfit.ColorId != colorId) continue;
                            if (p.VanillaPlayer.GetTruePosition().Distance(MyPlayer.VanillaPlayer.GetTruePosition()) < 0.8f)
                            {
                                acTokenAnother2 ??= new("morphing.another2");
                                break;
                            }
                        }
                    }
                };
                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
                    morphButton.InterruptEffect();

                    if (LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                    }
                }, this);
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
            if (acTokenChallenge != null && ev.Player.Unbox()!.DefaultOutfit.Outfit.outfit.ColorId == (sample?.outfit.ColorId ?? -1))
                acTokenChallenge.Value.exile = true;
        }

        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            var targetId = ev.Dead.Unbox()?.GetOutfit(75).Outfit.outfit.ColorId;
            var sampleId = sample?.outfit.ColorId;
            if (targetId.HasValue && sampleId.HasValue && targetId.Value == sampleId.Value)
                acTokenAnother1 ??= new("morphing.another1");

            if (morphButton != null && acTokenChallenge != null && morphButton.EffectActive)
                acTokenChallenge.Value.kill = true;
        }
    }
}
