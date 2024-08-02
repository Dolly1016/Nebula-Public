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


public class Busker : DefinedRoleTemplate, DefinedRole
{
    private Busker() : base("busker", new(255, 172, 117), RoleCategory.CrewmateRole, Crewmate.MyTeam, [PseudocideCoolDownOption, PseudocideDurationOption, HidePseudocideFromVitalsOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Busker.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration PseudocideCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.busker.pseudocideCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration PseudocideDurationOption = NebulaAPI.Configurations.Configuration("options.role.busker.pseudocideDuration", (5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration HidePseudocideFromVitalsOption = NebulaAPI.Configurations.Configuration("options.role.busker.hidePseudocideFromVitals", false);

    static public Busker MyRole = new Busker();
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable is not Lover;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        static private Image pseudocideButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskPseudocideButton.png", 115f);
        static private Image reviveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskReviveButton.png", 115f);

        AchievementToken<(bool isCleared,float lastRevive)>? acTokenChallenge;

        

        void RuntimeAssignable.OnInactivated()
        {
            if(AmOwner) PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var pseudocideButton = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));
                var reviveButton = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));

                pseudocideButton.SetSprite(pseudocideButtonSprite.GetSprite());
                pseudocideButton.Availability = (button) => MyPlayer.CanMove;
                pseudocideButton.Visibility = (button) => !MyPlayer.IsDead;
                pseudocideButton.CoolDownTimer = Bind(new Timer(0f, PseudocideCoolDownOption).SetAsAbilityCoolDown().Start());
                pseudocideButton.OnClick = (button) => {
                    NebulaManager.Instance.ScheduleDelayAction(() => {
                        using (RPCRouter.CreateSection("BuskerPseudocide"))
                        {
                            if(HidePseudocideFromVitalsOption) PlayerModInfo.RpcAttrModulator.Invoke((MyPlayer.PlayerId, new AttributeModulator(PlayerAttributes.BuskerEffect, 10000f, false, 0), true));
                            MyPlayer.Suicide(PlayerState.Pseudocide, null, KillParameter.WithDeadBody);
                        }
                        reviveButton.ActivateEffect();
                    });
                };
                pseudocideButton.SetLabel("pseudocide");

                StaticAchievementToken? acTokenCommon1 = null;

                reviveButton.SetSprite(reviveButtonSprite.GetSprite());
                reviveButton.Availability = (button) => MyPlayer.CanMove && MapData.GetCurrentMapData().CheckMapArea(PlayerControl.LocalPlayer.GetTruePosition());
                reviveButton.Visibility = (button) => button.EffectActive && Helpers.AllDeadBodies().Any(deadBody => deadBody.ParentId == MyPlayer.PlayerId);
                reviveButton.EffectTimer = Bind(new Timer(0f, PseudocideDurationOption));
                reviveButton.PlayFlash = () => reviveButton.EffectActive;
                reviveButton.OnClick = (button) => {
                    using (RPCRouter.CreateSection("ReviveBusker"))
                    {
                        PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
                        MyPlayer.Revive(null, MyPlayer.Position, true, false);
                        MyPlayer.VanillaPlayer.ModDive(false);
                    }
                    reviveButton.InactivateEffect();
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
                        NebulaGameManager.RpcTryAssignGhostRole.Invoke(MyPlayer.Unbox());
                    }
                };
                reviveButton.SetLabel("revive");

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
