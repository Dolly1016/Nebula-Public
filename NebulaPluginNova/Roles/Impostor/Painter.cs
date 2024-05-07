using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class Painter : ConfigurableStandardRole
{
    static public Painter MyRole = new Painter();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "painter";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration SampleCoolDownOption = null!;
    private NebulaConfiguration PaintCoolDownOption = null!;
    private NebulaConfiguration LoseSampleOnMeetingOption = null!;
    private NebulaConfiguration TransformAfterMeetingOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        SampleCoolDownOption = new NebulaConfiguration(RoleConfig, "sampleCoolDown", null, 0f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        PaintCoolDownOption = new NebulaConfiguration(RoleConfig, "paintCoolDown", null, 0f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        LoseSampleOnMeetingOption = new NebulaConfiguration(RoleConfig, "loseSampleOnMeeting", null, false, false);
        TransformAfterMeetingOption = new NebulaConfiguration(RoleConfig, "transformAfterMeeting", null, false, false);
    }

    public class Instance : Impostor.Instance
    {
        private ModAbilityButton? sampleButton = null;
        private ModAbilityButton? paintButton = null;

        static public ISpriteLoader sampleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SampleButton.png", 115f);
        static public ISpriteLoader paintButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MorphButton.png", 115f);
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<int[]> acTokenChallenge = null;

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenChallenge = new("painter.challenge", new int[15], (val, _) => val.Count(v => v >= 2) >= 3);

                GameData.PlayerOutfit? sample = null;
                PoolablePlayer? sampleIcon = null;
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                sampleButton.SetSprite(sampleButtonSprite.GetSprite());
                sampleButton.Availability = (button) => MyPlayer.CanMove;
                sampleButton.Visibility = (button) => !MyPlayer.IsDead;
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker.CurrentTarget?.Unbox()?.GetOutfit(75) ?? null;

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample!, paintButton!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                sampleButton.CoolDownTimer = Bind(new Timer(MyRole.SampleCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("sample");
                
                paintButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                paintButton.SetSprite(paintButtonSprite.GetSprite());
                paintButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove;
                paintButton.Visibility = (button) => !MyPlayer.IsDead;
                paintButton.OnClick = (button) => {
                    var outfit = sample ?? MyPlayer.Unbox().GetOutfit(75);

                    acTokenCommon ??= new("painter.common1");
                    if (sampleTracker.CurrentTarget!.Unbox()!.GetOutfit(75).ColorId != outfit.ColorId)
                        acTokenChallenge.Value[sampleTracker.CurrentTarget!.PlayerId]++;

                    var invoker = PlayerModInfo.RpcAddOutfit.GetInvoker(new(sampleTracker.CurrentTarget!.PlayerId, new("Paint", 40, false, outfit)));
                    if (MyRole.TransformAfterMeetingOption)
                        NebulaGameManager.Instance?.Scheduler.Schedule(RPCScheduler.RPCTrigger.AfterMeeting, invoker);
                    else
                        invoker.InvokeSingle();
                    button.StartCoolDown();
                };
                paintButton.OnMeeting = (button) =>
                {
                    if (MyRole.LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                };
                paintButton.CoolDownTimer = Bind(new Timer(MyRole.PaintCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                paintButton.SetLabel("paint");
            }
        }
    }
}

