using AmongUs.GameOptions;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Components;
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

    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    static private readonly FloatConfiguration CleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.cleanCoolDown", (5f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration SyncKillAndCleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.syncKillAndCleanCoolDown", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Cleaner MyRole = new();
    static private readonly GameStatsEntry StatsClean = NebulaAPI.CreateStatsEntry("stats.cleaner.clean", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CleanButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                AchievementToken<(bool cleared, int removed)>? acTokenChallenge = new("cleaner.challenge", (false, 0), (val, _) => val.cleared);
                
                var cleanTracker = ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => true).Register(this);

                var cleanButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "cleaner.clean",
                    CleanCoolDownOption, "clean", buttonSprite,
                    _ => cleanTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                cleanButton.OnClick = (button) => {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.CleanCorpseAction);

                    if (cleanTracker.CurrentTarget?.MyKiller == MyPlayer) new StaticAchievementToken("cleaner.common2");
                    AmongUsUtil.RpcCleanDeadBody(cleanTracker.CurrentTarget!.PlayerId,MyPlayer.PlayerId,EventDetail.Clean);
                    if (SyncKillAndCleanCoolDownOption) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    cleanButton.StartCoolDown();

                    new StaticAchievementToken("cleaner.common1");
                    acTokenChallenge.Value.removed++;
                    StatsClean.Progress();
                };

                GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
                {
                    if (ev.Murderer.AmOwner)
                    {
                        if (SyncKillAndCleanCoolDownOption)
                            cleanButton.CoolDownTimer!.Start();
                        else if (cleanButton.CoolDownTimer!.CurrentTime < 5f)
                            cleanButton?.CoolDownTimer?.Start(5f);
                    }
                }, this);
                GameOperatorManager.Instance?.Subscribe<CalledEmergencyMeetingEvent>(ev => acTokenChallenge.Value.cleared = acTokenChallenge.Value.removed >= 2, this);
                GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev => acTokenChallenge.Value.removed = 0, this);
            }
        }
    }
}