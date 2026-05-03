using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

internal class NiceTracker : DefinedSingleAbilityRoleTemplate<NiceTracker.Ability>, DefinedRole, IAssignableDocument
{

    private NiceTracker() : base("niceTracker", new(98, 121, 207), RoleCategory.CrewmateRole, Crewmate.MyTeam, [TrackCooldownOption, TrackDurationOption, TrackAdditionalDurationOption, TrackerDurationOption, ResetOnMeetingOption, ShowWhereTrackingIsOption])
    {
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static private readonly FloatConfiguration TrackCooldownOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.trackCooldown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration TrackDurationOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.trackDuration", (0f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration TrackAdditionalDurationOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.trackAdditionalDuration", (0f, 10f, 0.5f), 1f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration TrackerDurationOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.trackerDuration", (float[])[5f, 10f, 15f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 90f, 100f, 120f, 140f, 180f, 220f, 260f, 300f], 40f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration ResetOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.resetOnMeeting", false);
    static private readonly BoolConfiguration ShowWhereTrackingIsOption = NebulaAPI.Configurations.Configuration("options.role.niceTracker.showWhereTrackingIs", false);

    static public NiceTracker MyRole = new();

    static private readonly GameStatsEntry StatsTrack = NebulaAPI.CreateStatsEntry("stats.niceTracker.track", GameStatsCategory.Roles, MyRole);    

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    Image? DefinedAssignable.IconImage => Impostor.EvilTracker.MyRole.GetRoleIcon();
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage, "role.niceTracker.ability.track");
    }

    static private readonly Image buttonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.TrackNiceButton.png", 115f);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private record Tracking(GamePlayer Player, PlayerIconInfo Icon, GameTimer Duration, FlexibleLifespan Lifespan);
        
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            
            if (AmOwner)
            {
                var iconHolder = new PlayersIconHolder(false).Register(this);
                iconHolder.XInterval = 0.35f;

                List<Tracking> trackings = [];

                if (ShowWhereTrackingIsOption)
                {
                    Helpers.TextHudContent("TrackingText", this, (tmPro) =>
                    {
                        tmPro.text = string.Join("<br>", trackings.Where(tuple => !tuple.Lifespan.IsDeadObject).Select(tuple =>
                        {
                            var target = tuple.Player;
                            return target.Name + ": " + AmongUsUtil.GetRoomName(target.TruePosition, true).Color(Color.Lerp(DynamicPalette.PlayerColors[target.PlayerId], Color.white, 0.25f));
                        }));
                    });
                }

                var trackTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p) && !trackings.Any(tuple => !tuple.Lifespan.IsDeadObject && tuple.Player == p));
                var trackButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, TrackCooldownOption, TrackDurationOption, "track", buttonImage,
                    _ => trackTracker.CurrentTarget != null);
                trackButton.OnClick = button =>
                {
                    (button.EffectTimer as GameTimer)?.SetRange(TrackDurationOption + trackings.Count(tuple => !tuple.Lifespan.IsDeadObject) * TrackAdditionalDurationOption);
                    trackButton.StartEffect();
                };
                trackButton.OnUpdate = button =>
                {
                    if (button.IsInEffect && trackTracker.CurrentTarget == null) trackButton.InterruptEffect();
                };
                trackButton.OnEffectStart = button =>
                {
                    trackTracker.KeepAsLongAsPossible = true;
                };
                trackButton.OnEffectEnd = (button) =>
                {
                    trackTracker.KeepAsLongAsPossible = false;

                    var target = trackTracker.CurrentTarget;
                    if (target == null) return;
                    if (MeetingHud.Instance) return;

                    if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, target, new(RealPlayerOnly: true))).IsCanceled ?? false))
                    {
                        var icon = iconHolder.AddPlayer(target.RealPlayer);
                        var lifespan = new FlexibleLifespan(this);
                        var timer = NebulaAPI.Modules.Timer(lifespan, TrackerDurationOption);
                        var arrow = new TrackingArrowAbility(target.RealPlayer, 0f, Color.white).Register(lifespan);
                        timer.SetCondition(() => !MeetingHud.Instance && !ExileController.Instance);
                        timer.Start();
                        trackings.Add(new(target.RealPlayer, icon, timer, lifespan));
                        StatsTrack.Progress();
                        trackButton.StartCoolDown();
                    }
                };
                trackButton.SetAsUsurpableButton(this);
                
                (trackButton.EffectTimer as GameTimer)?.SetCondition(() => MeetingHud.Instance == null && ExileController.Instance == null);

                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => {
                    trackings.RemoveAll(tuple =>
                    {
                        if (!tuple.Duration.IsProgressing)
                        {
                            tuple.Lifespan.Release();
                            iconHolder.Remove(tuple.Icon);
                            return true;
                        }
                        tuple.Icon.SetText(tuple.Duration.TimerText ?? "");
                        return false;
                    });
                }, this);
                if (ResetOnMeetingOption)
                {
                    GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                    {
                        foreach (var tuple in trackings)
                        {
                            tuple.Lifespan.Release();
                            iconHolder.Remove(tuple.Icon);
                        }
                        trackings.Clear();
                    }, this);
                }
            }
        }


    }
}
