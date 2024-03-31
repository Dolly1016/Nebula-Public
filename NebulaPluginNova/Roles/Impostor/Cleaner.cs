using AmongUs.GameOptions;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;
using static Rewired.UnknownControllerHat;

namespace Nebula.Roles.Impostor;

public class Cleaner : ConfigurableStandardRole, HasCitation
{
    static public Cleaner MyRole = new Cleaner();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "cleaner";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CleanCoolDownOption = null!;
    private NebulaConfiguration SyncKillAndCleanCoolDownOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        CleanCoolDownOption = new NebulaConfiguration(RoleConfig, "cleanCoolDown", null, 5f, 60f, 2.5f, 30f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        SyncKillAndCleanCoolDownOption = new NebulaConfiguration(RoleConfig, "syncKillAndCleanCoolDown", null, true, true);
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? cleanButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CleanButton.png", 115f);
        public override AbstractRole Role => MyRole;

        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, int removed)>? acTokenChallenge = null;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
        {
            if (AmOwner)
            {
                cleanButton?.CoolDownTimer?.Start(MyRole.SyncKillAndCleanCoolDownOption ? null : 5f);
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenChallenge = new("cleaner.challenge",(false,0),(val,_)=>val.cleared);

                var cleanTracker = Bind(ObjectTrackers.ForDeadBody(null, MyPlayer.MyControl, (d) => true));

                cleanButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                cleanButton.SetSprite(buttonSprite.GetSprite());
                cleanButton.Availability = (button) => cleanTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                cleanButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                cleanButton.OnClick = (button) => {
                    AmongUsUtil.RpcCleanDeadBody(cleanTracker.CurrentTarget!.ParentId,MyPlayer.PlayerId,EventDetail.Clean);
                    if (MyRole.SyncKillAndCleanCoolDownOption) PlayerControl.LocalPlayer.killTimer = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
                    cleanButton.StartCoolDown();

                    acTokenCommon ??= new("cleaner.common1");
                    acTokenChallenge.Value.removed++;
                };
                cleanButton.CoolDownTimer = Bind(new Timer(MyRole.CleanCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                cleanButton.SetLabel("clean");
            }
        }


        void IGameEntity.OnEmergencyMeeting(GamePlayer reporter)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.cleared = acTokenChallenge.Value.removed >= 2;
        }

        void IGameEntity.OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.removed = 0;
        }
    }
}
