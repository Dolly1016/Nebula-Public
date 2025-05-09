﻿using AmongUs.GameOptions;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Cleaner : DefinedSingleAbilityRoleTemplate<Cleaner.Ability>, HasCitation, DefinedRole
{
    private Cleaner() : base("cleaner", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CleanCoolDownOption, SyncKillAndCleanCoolDownOption]){
        GameActionTypes.CleanCorpseAction = new("cleaner.clean", this, isCleanDeadBodyAction: true);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    static private FloatConfiguration CleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.cleanCoolDown", (5f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration SyncKillAndCleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.syncKillAndCleanCoolDown", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    bool DefinedRole.IsJackalizable => true;
    static public Cleaner MyRole = new Cleaner();
    static private GameStatsEntry StatsClean = NebulaAPI.CreateStatsEntry("stats.cleaner.clean", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private ModAbilityButton? cleanButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CleanButton.png", 115f);


        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, int removed)>? acTokenChallenge = null;

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (SyncKillAndCleanCoolDownOption)
                cleanButton?.CoolDownTimer?.Start();
            else if (((cleanButton?.CurrentTimer as Timer)?.CurrentTime ?? 99f) < 5f)
                cleanButton?.CoolDownTimer?.Start(5f);
        }

        public Ability(GamePlayer player) : base(player)
        {
            if (AmOwner)
            {
                acTokenChallenge = new("cleaner.challenge",(false,0),(val,_)=>val.cleared);

                var cleanTracker = Bind(ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => true));

                cleanButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "cleaner.clean");
                cleanButton.SetSprite(buttonSprite.GetSprite());
                cleanButton.Availability = (button) => cleanTracker.CurrentTarget != null && MyPlayer.VanillaPlayer.CanMove;
                cleanButton.Visibility = (button) => !MyPlayer.IsDead;
                cleanButton.OnClick = (button) => {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.CleanCorpseAction);

                    if (cleanTracker.CurrentTarget?.MyKiller == MyPlayer) new StaticAchievementToken("cleaner.common2");
                    AmongUsUtil.RpcCleanDeadBody(cleanTracker.CurrentTarget!.PlayerId,MyPlayer.PlayerId,EventDetail.Clean);
                    if (SyncKillAndCleanCoolDownOption) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    cleanButton.StartCoolDown();

                    acTokenCommon ??= new("cleaner.common1");
                    acTokenChallenge.Value.removed++;
                    StatsClean.Progress();
                };
                cleanButton.CoolDownTimer = Bind(new Timer(CleanCoolDownOption).SetAsAbilityCoolDown().Start());
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