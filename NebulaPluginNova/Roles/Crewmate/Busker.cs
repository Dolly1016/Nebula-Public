using Nebula.Game.Statistics;
using Nebula.Map;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;


public class Busker : DefinedSingleAbilityRoleTemplate<Busker.Ability>, DefinedRole
{
    private Busker() : base("busker", new(255, 172, 117), RoleCategory.CrewmateRole, Crewmate.MyTeam, [PseudocideCoolDownOption, PseudocideDurationOption, HidePseudocideFromVitalsOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Busker.png");

        GameActionTypes.BuskerRevivingAction = new("busker.revive", this, isPhysicalAction: true);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    static private readonly FloatConfiguration PseudocideCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.busker.pseudocideCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration PseudocideDurationOption = NebulaAPI.Configurations.Configuration("options.role.busker.pseudocideDuration", (5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration HidePseudocideFromVitalsOption = NebulaAPI.Configurations.Configuration("options.role.busker.hidePseudocideFromVitals", false);

    static public readonly Busker MyRole = new();
    static private readonly GameStatsEntry StatsPseudocide = NebulaAPI.CreateStatsEntry("stats.busker.pseudocide", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsMissed = NebulaAPI.CreateStatsEntry("stats.busker.missed", GameStatsCategory.Roles, MyRole);
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable is not Lover;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private Image pseudocideButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskPseudocideButton.png", 115f);
        static private Image reviveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskReviveButton.png", 115f);

        AchievementToken<(bool isCleared,float lastRevive)>? acTokenChallenge;

        

        void IGameOperator.OnReleased()
        {
            if(AmOwner) PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
        }

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var pseudocideButton = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: !MyPlayer.IsImpostor)
                    .BindKey(Virial.Compat.VirtualKeyInput.Ability)
                    .SetImage(pseudocideButtonSprite)
                    .SetLabel("pseudocide")
                    .SetAsUsurpableButton(this);
                var reviveButton = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: !MyPlayer.IsImpostor)
                    .BindKey(Virial.Compat.VirtualKeyInput.Ability)
                    .SetImage(reviveButtonSprite)
                    .SetLabel("revive")
                    .SetAsUsurpableButton(this);

                pseudocideButton.Availability = (button) => MyPlayer.CanMove;
                pseudocideButton.Visibility = (button) => !MyPlayer.IsDead;
                pseudocideButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, PseudocideCoolDownOption)
                    .SetAsAbilityTimer()
                    .Start();
                pseudocideButton.OnClick = (button) => {
                    NebulaManager.Instance.ScheduleDelayAction(() => {
                        using (RPCRouter.CreateSection("BuskerPseudocide"))
                        {
                            if (HidePseudocideFromVitalsOption) MyPlayer.GainAttribute(PlayerAttributes.BuskerEffect, 10000f, false, 0);
                            MyPlayer.Suicide(PlayerState.Pseudocide, null, KillParameter.WithDeadBody);
                            StatsPseudocide.Progress();
                        }
                        reviveButton.StartEffect();
                    });
                };

                StaticAchievementToken? acTokenCommon1 = null;

                reviveButton.Availability = button => MyPlayer.CanMove && MapData.GetCurrentMapData().CheckMapArea(PlayerControl.LocalPlayer.GetTruePosition());
                reviveButton.Visibility = button => button.IsInEffect && Helpers.AllDeadBodies().Any(deadBody => deadBody.ParentId == MyPlayer.PlayerId);
                reviveButton.EffectTimer = NebulaAPI.Modules.Timer(this, PseudocideDurationOption);
                reviveButton.PlayFlashWhile = button => true;
                reviveButton.OnClick = (button) => {
                    using (RPCRouter.CreateSection("ReviveBusker"))
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.BuskerRevivingAction);
                        PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
                        MyPlayer.Revive(null, MyPlayer.Position, true, false);
                        MyPlayer.VanillaPlayer.ModDive(false);
                    }
                    reviveButton.InterruptEffect();
                    pseudocideButton.StartCoolDown();
                    acTokenCommon1 ??= new("busker.common1");
                    acTokenChallenge ??= new("busker.challenge", (false, 0f), (val, _) => val.isCleared);
                    acTokenChallenge.Value.lastRevive = NebulaGameManager.Instance!.CurrentTime;
                };
                reviveButton.OnEffectEnd = (button) =>
                {
                    if (MyPlayer.IsDead)
                    {
                        PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
                        NebulaGameManager.Instance!.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Accident, null, 1 << MyPlayer.PlayerId);
                        new StaticAchievementToken("busker.another1");
                        StatsMissed.Progress();
                        MyPlayer.GetModule<Impostor.BalloonHolder>()?.OnDeadFinally();
                        NebulaGameManager.RpcTryAssignGhostRole.Invoke(MyPlayer.Unbox());
                    }
                };

            }
        }

        private void CheckChallengeAchievement(GamePlayer reporter)
        {
            if (acTokenChallenge != null && !reporter.AmOwner) acTokenChallenge.Value.isCleared |= NebulaGameManager.Instance!.CurrentTime - acTokenChallenge.Value.lastRevive < 2f;
        }

        [Local]
        void OnReported(MeetingPreStartEvent ev) => CheckChallengeAchievement(ev.Reporter);
        
    }
}
