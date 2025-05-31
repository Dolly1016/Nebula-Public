using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Bait : DefinedSingleAbilityRoleTemplate<Bait.Ability>, HasCitation, DefinedRole
{

    private Bait(): base("bait", new(0, 247, 255), RoleCategory.CrewmateRole, Crewmate.MyTeam, [ShowKillFlashOption, ReportDelayOption, ReportDelayDispersionOption, CanSeeVentFlashOption]) {
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0, false));

    static private readonly BoolConfiguration ShowKillFlashOption = NebulaAPI.Configurations.Configuration("options.role.bait.showKillFlash", false);
    static private readonly FloatConfiguration ReportDelayOption = NebulaAPI.Configurations.Configuration("options.role.bait.reportDelay", (0f, 5f, 0.5f), 0f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration ReportDelayDispersionOption = NebulaAPI.Configurations.Configuration("options.role.bait.reportDelayDispersion", (0f, 10f, 0.25f), 0.5f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration CanSeeVentFlashOption = NebulaAPI.Configurations.Configuration("options.role.bait.canSeeVentFlash", false);

    static public readonly Bait MyRole = new();
    static private readonly GameStatsEntry StatsBait = NebulaAPI.CreateStatsEntry("stats.bait.bait", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsKiller = NebulaAPI.CreateStatsEntry("stats.bait.killer", GameStatsCategory.Roles, MyRole);
    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped){}
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        private IEnumerator CoReport(PlayerControl murderer)
        {
            if(ShowKillFlashOption) AmongUsUtil.PlayQuickFlash(MyRole.UnityColor);

            float t = Mathf.Max(0.1f, ReportDelayOption) + ReportDelayDispersionOption * (float)System.Random.Shared.NextDouble();
            yield return new WaitForSeconds(t);
            if (MeetingHud.Instance) yield break;
            
            murderer.CmdReportDeadBody(MyPlayer.VanillaPlayer.Data);
            StatsKiller.Progress();
        }

        [Local]
        void OnReported(ReportDeadBodyEvent ev)
        {
            if((ev.Reported?.AmOwner ?? false) && ev.Reporter == MyPlayer.MyKiller && !ev.Reporter.AmOwner)
            {
                StatsBait.Progress();
                new StaticAchievementToken("bait.common1");
                acTokenChallenge ??= new("bait.challenge", (false, true), (val, _) => val.cleared);
            }
        }

        [Local,OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            new StaticAchievementToken("bait.another1");
        }

        [OnlyMyPlayer]
        void BaitReportOnMurdered(PlayerMurderedEvent ev)
        { 
            if (ev.Murderer.AmOwner && !MyPlayer.AmOwner && !IsUsurped) NebulaManager.Instance.StartCoroutine(CoReport(ev.Murderer.VanillaPlayer).WrapToIl2Cpp());
        }

        [Local]
        void OnEnterVent(PlayerVentEnterEvent ev)
        {
            if (CanSeeVentFlashOption && !IsUsurped) AmongUsUtil.PlayQuickFlash(MyRole.UnityColor.AlphaMultiplied(0.3f));
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.triggered = false;
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if ((acTokenChallenge?.Value.triggered ?? false) && ev.Player.PlayerId == (MyPlayer.MyKiller?.PlayerId ?? 255))
                acTokenChallenge.Value.cleared = true;
        }
    }
}

