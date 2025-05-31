using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Bloody : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Bloody(): base("bloody", "BLD", new(180, 0, 0), [CurseDurationOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    static private FloatConfiguration CurseDurationOption = NebulaAPI.Configurations.Configuration("options.role.bloody.curseDuration", (2.5f,30f,2.5f),10f, FloatConfigurationDecorator.Second);

    static public Bloody MyRole = new Bloody();
    static internal GameStatsEntry StatsBloody = NebulaAPI.CreateStatsEntry("stats.bloody.bloody", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsKiller = NebulaAPI.CreateStatsEntry("stats.bloody.killer", GameStatsCategory.Roles, MyRole);
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " †".Color(MyRole.UnityColor);
        }

        [OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer != MyPlayer)
            {
                if (AmOwner)
                {
                    ev.Murderer.GainAttribute(PlayerAttributes.CurseOfBloody, CurseDurationOption, false, 1);
                    new StaticAchievementToken("bloody.common1");
                    StatsBloody.Progress();
                    acTokenChallenge = new("bloody.challenge", (false, true), (val, _) => val.cleared);
                }else if (ev.Murderer.AmOwner)
                {
                    StatsKiller.Progress();
                }
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge?.Value.triggered ?? false)
                acTokenChallenge.Value.triggered = false;
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (acTokenChallenge?.Value.triggered ?? false)
                acTokenChallenge.Value.cleared = ev.Player.PlayerId == (MyPlayer.MyKiller?.PlayerId ?? 255);
        }


    }
}

