using AmongUs.GameOptions;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Cleaner : ConfigurableStandardRole, HasCitation, DefinedRole
{
    static public Cleaner MyRole = new Cleaner();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    string DefinedAssignable.LocalizedName => "cleaner";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CleanCoolDownOption = null!;
    private NebulaConfiguration SyncKillAndCleanCoolDownOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        CleanCoolDownOption = new NebulaConfiguration(RoleConfig, "cleanCoolDown", null, 5f, 60f, 2.5f, 30f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        SyncKillAndCleanCoolDownOption = new NebulaConfiguration(RoleConfig, "syncKillAndCleanCoolDown", null, true, true);
    }

    public class Instance : Impostor.Instance, RuntimeRole
    {
        private ModAbilityButton? cleanButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CleanButton.png", 115f);
        public override AbstractRole Role => MyRole;

        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, int removed)>? acTokenChallenge = null;

        public Instance(GamePlayer player) : base(player)
        {
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            cleanButton?.CoolDownTimer?.Start(MyRole.SyncKillAndCleanCoolDownOption ? null : 5f);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("cleaner.challenge",(false,0),(val,_)=>val.cleared);

                var cleanTracker = Bind(ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => true));

                cleanButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                cleanButton.SetSprite(buttonSprite.GetSprite());
                cleanButton.Availability = (button) => cleanTracker.CurrentTarget != null && MyPlayer.VanillaPlayer.CanMove;
                cleanButton.Visibility = (button) => !MyPlayer.IsDead;
                cleanButton.OnClick = (button) => {
                    AmongUsUtil.RpcCleanDeadBody(cleanTracker.CurrentTarget!.PlayerId,MyPlayer.PlayerId,EventDetail.Clean);
                    if (MyRole.SyncKillAndCleanCoolDownOption) PlayerControl.LocalPlayer.killTimer = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
                    cleanButton.StartCoolDown();

                    acTokenCommon ??= new("cleaner.common1");
                    acTokenChallenge.Value.removed++;
                };
                cleanButton.CoolDownTimer = Bind(new Timer(MyRole.CleanCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                cleanButton.SetLabel("clean");
            }
        }


        [Local]
        void OnEmergencyMeeting(CalledEmergencyMeetingEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.cleared = acTokenChallenge.Value.removed >= 2;
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.removed = 0;
        }
    }
}
