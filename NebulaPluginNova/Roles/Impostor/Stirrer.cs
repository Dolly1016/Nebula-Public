using Nebula.Map;
using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class Stirrer : ConfigurableStandardRole
{
    static public Stirrer MyRole = new Stirrer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "stirrer";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[]? arguments) => new Instance(player);

    private NebulaConfiguration StirCoolDownOption = null!;
    private NebulaConfiguration SabotageChargeOption = null!;
    private NebulaConfiguration SabotageMaxChargeOption = null!;
    private NebulaConfiguration SabotageCoolDownOption = null!;
    private NebulaConfiguration SabotageIntervalOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny);

        StirCoolDownOption = new NebulaConfiguration(RoleConfig, "stirCoolDown", null, 0f, 60f, 5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        SabotageChargeOption = new NebulaConfiguration(RoleConfig, "sabotageCharge", null, 1, 10, 3, 3);
        SabotageMaxChargeOption = new NebulaConfiguration(RoleConfig, "sabotageMaxCharge", null, 1, 20, 5, 5);
        SabotageCoolDownOption = new NebulaConfiguration(RoleConfig, "sabotageCoolDown", null, 0f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        SabotageIntervalOption = new NebulaConfiguration(RoleConfig, "sabotageInterval", null, 30f, 120f, 5f, 60f, 60f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : Impostor.Instance
    {
        private ModAbilityButton? stirButton = null;
        private ModAbilityButton? sabotageButton = null;

        static public ISpriteLoader StirButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.StirButton.png", 115f);
        static public ISpriteLoader SabotageButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FakeSaboButton.png", 115f);
        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        Dictionary<byte, int> sabotageChargeMap = new();

        StaticAchievementToken? acTokenCommon = null, acTokenChallenge = null;
        
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead && !p.Data.Role.IsImpostor));

                stirButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                stirButton.SetSprite(StirButtonSprite.GetSprite());
                stirButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove && (!sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget.PlayerId,out int charge) || charge < MyRole.SabotageMaxChargeOption.GetMappedInt());
                stirButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                stirButton.OnClick = (button) => {
                    int charge = 0;
                    if (sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget!.PlayerId, out var v)) charge = v;
                    sabotageChargeMap[sampleTracker.CurrentTarget!.PlayerId] = Mathf.Min(MyRole.SabotageMaxChargeOption.GetMappedInt(), charge + MyRole.SabotageChargeOption.GetMappedInt());

                    stirButton.StartCoolDown();
                };
                stirButton.CoolDownTimer = Bind(new Timer(MyRole.StirCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                stirButton.SetLabel("stir");

                sabotageButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                sabotageButton.SetSprite(SabotageButtonSprite.GetSprite());
                sabotageButton.Availability = (button) => MyPlayer.MyControl.CanMove && sabotageChargeMap.Any(entry => entry.Value > 0) && PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(task => task.TryCast<SabotageTask>() != null)) == null;
                sabotageButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                sabotageButton.OnClick = (button) => {
                    int count = 0;
                    foreach (var entry in sabotageChargeMap)
                    {
                        if (entry.Value > 0)
                        {
                            var p = NebulaGameManager.Instance!.GetModPlayerInfo(entry.Key);
                            if (p?.Role.Role.Category == RoleCategory.ImpostorRole) continue;

                            if (!(p?.IsDead ?? false))
                            {
                                FakeSabotageStatus.RpcPushFakeSabotage(p!, MapData.GetCurrentMapData().GetSabotageSystemTypes().Random());
                                count++;
                            }
                        }
                        sabotageChargeMap[entry.Key] = entry.Value - 1;
                    }
                    button.CoolDownTimer?.Start(MyRole.SabotageIntervalOption.GetFloat());

                    acTokenCommon ??= new("stirrer.common1");
                    if(count >= 7 && MyRole.SabotageChargeOption <= 3 && !(MyRole.StirCoolDownOption.GetFloat() > 10f)) acTokenChallenge ??= new("stirrer.challenge");

                };
                sabotageButton.CoolDownTimer = Bind(new Timer(Mathf.Max(MyRole.SabotageIntervalOption.GetFloat(), MyRole.SabotageCoolDownOption.GetFloat())).SetAsAbilityCoolDown().Start(MyRole.SabotageCoolDownOption.GetFloat()));
                sabotageButton.SetLabel("fakeSabotage");
                sabotageButton.UseCoolDownSupport = false;
                sabotageButton.OnStartTaskPhase = (button) => button.CoolDownTimer?.Start(MyRole.SabotageCoolDownOption.GetFloat());
            }
        }

        public override void DecorateOtherPlayerName(PlayerModInfo player, ref string text, ref Color color)
        {
            if(sabotageChargeMap.TryGetValue(player.PlayerId,out int val))
            {
                if (player.Role.Role.Category == RoleCategory.ImpostorRole) return;
                if (val <= 0) return;
                text += StringExtensions.Color(" (" + val + ")", Color.gray);
            }
        }
    }
}
