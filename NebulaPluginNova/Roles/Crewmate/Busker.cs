using Nebula.Map;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;


public class Busker : ConfigurableStandardRole
{
    static public Busker MyRole = new Busker();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "busker";
    public override Color RoleColor => new Color(255f / 255f, 172f / 255f, 117f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration PseudocideCoolDownOption = null!;
    private NebulaConfiguration PseudocideDurationOption = null!;
    private NebulaConfiguration HidePseudocideFromVitalsOption = null!;

    public override bool CanLoadDefault(IntroAssignableModifier modifier) => modifier is not Lover;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny);

        PseudocideCoolDownOption = new(RoleConfig, "pseudocideCoolDown", null, 5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        PseudocideDurationOption = new(RoleConfig, "pseudocideDuration", null, 5f, 60f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        HidePseudocideFromVitalsOption = new(RoleConfig, "hidePseudocideFromVitals", null, false, false);
    }

    public class Instance : Crewmate.Instance, IGameEntity
    {
        static private ISpriteLoader pseudocideButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskPseudocideButton.png", 115f);
        static private ISpriteLoader reviveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskReviveButton.png", 115f);

        AchievementToken<(bool isCleared,float lastRevive)>? acTokenChallenge;

        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        protected override void OnInactivated()
        {
            if(AmOwner) PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var pseudocideButton = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));
                var reviveButon = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));

                pseudocideButton.SetSprite(pseudocideButtonSprite.GetSprite());
                pseudocideButton.Availability = (button) => MyPlayer.MyControl.CanMove;
                pseudocideButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                pseudocideButton.CoolDownTimer = Bind(new Timer(0f, MyRole.PseudocideCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                pseudocideButton.OnClick = (button) => {
                    NebulaManager.Instance.ScheduleDelayAction(() => {
                        using (RPCRouter.CreateSection("BuskerPseudocide"))
                        {
                            if(MyRole.HidePseudocideFromVitalsOption) PlayerModInfo.RpcAttrModulator.Invoke((MyPlayer.PlayerId, new AttributeModulator(PlayerAttributes.BuskerEffect, 10000f, false, 0), true));
                            MyPlayer.MyControl.ModKill(MyPlayer.MyControl, false, PlayerState.Pseudocide, null, false, false);
                        }
                        reviveButon.ActivateEffect();
                    });
                };
                pseudocideButton.SetLabel("pseudocide");

                StaticAchievementToken? acTokenCommon1 = null;

                reviveButon.SetSprite(reviveButtonSprite.GetSprite());
                reviveButon.Availability = (button) => MyPlayer.MyControl.CanMove && MapData.GetCurrentMapData().CheckMapArea(PlayerControl.LocalPlayer.transform.position);
                reviveButon.Visibility = (button) => button.EffectActive && Helpers.AllDeadBodies().Any(deadBody => deadBody.ParentId == MyPlayer.PlayerId);
                reviveButon.EffectTimer = Bind(new Timer(0f, MyRole.PseudocideDurationOption.GetFloat()));
                reviveButon.OnClick = (button) => {
                    using (RPCRouter.CreateSection("ReviveBusker"))
                    {
                        PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
                        MyPlayer.MyControl.ModRevive(MyPlayer.MyControl.transform.position, true, false);
                        MyPlayer.MyControl.ModDive(false);
                    }
                    reviveButon.InactivateEffect();
                    pseudocideButton.StartCoolDown();
                    acTokenCommon1 ??= new("busker.common1");
                    acTokenChallenge ??= new("busker.challenge", (false, 0f), (val, _) => val.isCleared);
                    acTokenChallenge.Value.lastRevive = NebulaGameManager.Instance!.CurrentTime;
                };
                reviveButon.OnEffectEnd = (button) =>
                {
                    if (MyPlayer.IsDead)
                    {
                        PlayerModInfo.RpcRemoveAttr.Invoke((MyPlayer.PlayerId, PlayerAttributes.BuskerEffect.Id));
                        NebulaGameManager.Instance!.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Accident, null, 1 << MyPlayer.PlayerId);
                        new StaticAchievementToken("busker.another1");
                        NebulaGameManager.RpcTryAssignGhostRole.Invoke(MyPlayer);
                    }
                };
                reviveButon.SetLabel("revive");

            }
        }

        private void CheckChallengeAchievement(PlayerModInfo reporter)
        {
            if (AmOwner)
            {
                if (acTokenChallenge != null && !reporter.AmOwner) acTokenChallenge.Value.isCleared |= NebulaGameManager.Instance!.CurrentTime - acTokenChallenge.Value.lastRevive < 2f;
            }
        }

        void IGameEntity.OnReported(GamePlayer reporter, GamePlayer reported) => CheckChallengeAchievement(reporter.Unbox());
        void IGameEntity.OnEmergencyMeeting(GamePlayer reporter) => CheckChallengeAchievement(reporter.Unbox());
        
    }
}
