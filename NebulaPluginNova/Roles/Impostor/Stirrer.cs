using Nebula.Map;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Stirrer : DefinedRoleTemplate, DefinedRole
{
    private Stirrer() : base("stirrer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [StirCoolDownOption,SabotageChargeOption, SabotageMaxChargeOption,SabotageCoolDownOption,SabotageIntervalOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[]? arguments) => new Instance(player);

    static private FloatConfiguration StirCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.stirrer.stirCoolDown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration SabotageChargeOption = NebulaAPI.Configurations.Configuration("options.role.stirrer.sabotageCharge", (1,10),3);
    static private IntegerConfiguration SabotageMaxChargeOption = NebulaAPI.Configurations.Configuration("options.role.stirrer.sabotageMaxCharge", (1, 20), 5);
    static private FloatConfiguration SabotageCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.stirrer.sabotageCoolDown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration SabotageIntervalOption = NebulaAPI.Configurations.Configuration("options.role.stirrer.sabotageInterval", (10f, 120f, 5f), 60f, FloatConfigurationDecorator.Second);

    static public Stirrer MyRole = new Stirrer();
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        private ModAbilityButton? stirButton = null;
        private ModAbilityButton? sabotageButton = null;

        static public Image StirButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.StirButton.png", 115f);
        static public Image SabotageButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FakeSaboButton.png", 115f);
        
        public Instance(GamePlayer player) : base(player)
        {
        }

        Dictionary<byte, int> sabotageChargeMap = new();

        StaticAchievementToken? acTokenCommon = null, acTokenChallenge = null;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.ImpostorKillPredicate(p)));

                stirButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability,"stirrer.stir");
                stirButton.SetSprite(StirButtonSprite.GetSprite());
                stirButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove && (!sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget.PlayerId,out int charge) || charge < SabotageMaxChargeOption);
                stirButton.Visibility = (button) => !MyPlayer.IsDead;
                stirButton.OnClick = (button) => {
                    int charge = 0;
                    if (sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget!.PlayerId, out var v)) charge = v;
                    sabotageChargeMap[sampleTracker.CurrentTarget!.PlayerId] = Mathf.Min(SabotageMaxChargeOption, charge + SabotageChargeOption);

                    stirButton.StartCoolDown();
                };
                stirButton.CoolDownTimer = Bind(new Timer(StirCoolDownOption).SetAsAbilityCoolDown().Start());
                stirButton.SetLabel("stir");

                sabotageButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility,"stirrer.fakeSabo");
                sabotageButton.SetSprite(SabotageButtonSprite.GetSprite());
                sabotageButton.Availability = (button) => MyPlayer.CanMove && sabotageChargeMap.Any(entry => entry.Value > 0) && PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(task => task.TryCast<SabotageTask>() != null)) == null;
                sabotageButton.Visibility = (button) => !MyPlayer.IsDead;
                sabotageButton.OnClick = (button) => {
                    int count = 0;
                    foreach (var entry in sabotageChargeMap)
                    {
                        if (entry.Value > 0)
                        {
                            var p = NebulaGameManager.Instance!.GetPlayer(entry.Key);
                            if (p?.Role.Role.Category == RoleCategory.ImpostorRole) continue;

                            if (!(p?.IsDead ?? false))
                            {
                                FakeSabotageStatus.RpcPushFakeSabotage(p!, MapData.GetCurrentMapData().GetSabotageSystemTypes().Random());
                                count++;
                            }
                        }
                        sabotageChargeMap[entry.Key] = entry.Value - 1;
                    }
                    button.CoolDownTimer?.Start(SabotageIntervalOption);

                    acTokenCommon ??= new("stirrer.common1");
                    if(count >= 7 && SabotageChargeOption <= 3 && !(StirCoolDownOption > 10f)) acTokenChallenge ??= new("stirrer.challenge");

                };
                sabotageButton.CoolDownTimer = Bind(new Timer(Mathf.Max(SabotageIntervalOption, SabotageCoolDownOption)).SetAsAbilityCoolDown().Start(SabotageCoolDownOption));
                sabotageButton.SetLabel("fakeSabotage");
                sabotageButton.UseCoolDownSupport = false;
                sabotageButton.OnStartTaskPhase = (button) => button.CoolDownTimer?.Start(SabotageCoolDownOption);
            }

            GameOperatorManager.Instance?.Register<PlayerDieEvent>(ev =>
            {
                ev.Player.Unbox().FakeSabotage.RemoveFakeSabotage(SystemTypes.Electrical, SystemTypes.Comms);
            }, NebulaAPI.CurrentGame!);
        }

        [Local]
        void DecorateOtherPlayerName(PlayerDecorateNameEvent ev)
        {
            if(sabotageChargeMap.TryGetValue(ev.Player.PlayerId,out int val))
            {
                if (ev.Player.IsImpostor) return;
                if (val <= 0) return;
                ev.Name += StringExtensions.Color(" (" + val + ")", Color.gray);
            }
        }
    }
}
