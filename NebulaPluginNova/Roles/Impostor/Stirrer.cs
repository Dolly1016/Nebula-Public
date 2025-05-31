using Nebula.Map;
using TMPro;
using Unity.Services.Core.Internal;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
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
    static private GameStatsEntry StatsStir = NebulaAPI.CreateStatsEntry("stats.stirrer.stir", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsSabotage = NebulaAPI.CreateStatsEntry("stats.stirrer.sabo", GameStatsCategory.Roles, MyRole);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        
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
                var sampleTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.KillablePredicate(MyPlayer)).Register(this);

                var stirButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,"stirrer.stir",
                    StirCoolDownOption, "stir", StirButtonSprite,
                    _ => sampleTracker.CurrentTarget != null && (!sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget.PlayerId, out int charge) || charge < SabotageMaxChargeOption));
                stirButton.OnClick = (button) => {
                    int charge = 0;
                    if (sabotageChargeMap.TryGetValue(sampleTracker.CurrentTarget!.PlayerId, out var v)) charge = v;
                    sabotageChargeMap[sampleTracker.CurrentTarget!.PlayerId] = Mathf.Min(SabotageMaxChargeOption, charge + SabotageChargeOption);

                    stirButton.StartCoolDown();
                    StatsStir.Progress();
                };

                var sabotageButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility,"stirrer.fakeSabo",
                    Mathf.Max(SabotageIntervalOption, SabotageCoolDownOption), "fakeSabotage", SabotageButtonSprite,
                    _ => sabotageChargeMap.Any(entry => entry.Value > 0) && PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)(task => task.TryCast<SabotageTask>() != null)) == null
                    );
                sabotageButton.CoolDownTimer!.Start(SabotageCoolDownOption);
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
                    StatsSabotage.Progress();

                };
                sabotageButton.SetLabel("fakeSabotage");
                GameOperatorManager.Instance?.Subscribe<TaskPhaseStartEvent>(ev => {
                    sabotageButton.CoolDownTimer!.Start(SabotageCoolDownOption);
                }, this, 99);
                //ModAbilityButtonのクールダウンリセットより後(優先度100未満)のタイミングで正しくリセットする。
            }

            GameOperatorManager.Instance?.Subscribe<PlayerDieEvent>(ev =>
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
