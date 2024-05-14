using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Bait : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public Bait MyRole = new Bait();

    public Bait(): base("bait", new(0, 247, 255), RoleCategory.CrewmateRole, Crewmate.MyTeam ) {
        ConfigurationHolder?.AppendConfigurations([ShowKillFlashOption, ReportDelayOption, ReportDelayDispersionOption, CanSeeVentFlashOption]);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private BoolConfiguration ShowKillFlashOption = new BoolConfigurationImpl("role.bait.showKillFlash", false);
    private FloatConfiguration ReportDelayOption = new FloatConfigurationImpl("role.bait.reportDelay", ArrayHelper.Selection(0f, 5f, 0.5f), 0f).DecorateAsSecConfiguration();
    private FloatConfiguration ReportDelayDispersionOption = new FloatConfigurationImpl("role.bait.reportDelayDispersion", ArrayHelper.Selection(0f, 10f, 0.25f), 0.5f).DecorateAsSecConfiguration();
    private BoolConfiguration CanSeeVentFlashOption = new BoolConfigurationImpl("role.bait.canSeeVentFlash", false);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        void RuntimeAssignable.OnActivated() { }


        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        private IEnumerator CoReport(PlayerControl murderer)
        {
            if(Bait.MyRole.ShowKillFlashOption) AmongUsUtil.PlayQuickFlash(MyRole.UnityColor);

            float t = Mathf.Max(0.1f, MyRole.ReportDelayOption) + MyRole.ReportDelayDispersionOption * (float)System.Random.Shared.NextDouble();
            yield return new WaitForSeconds(t);
            murderer.CmdReportDeadBody(MyPlayer.VanillaPlayer.Data);
        }

        [Local, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer.AmOwner) return; //自殺の場合は何もしない

            new StaticAchievementToken("bait.common1");
            acTokenChallenge ??= new("bait.challenge", (false, true), (val, _) => val.cleared);
        }

        [OnlyMyPlayer]
        void BaitReportOnMurdered(PlayerMurderedEvent ev)
        { 
            if (ev.Murderer.AmOwner && !MyPlayer.AmOwner) NebulaManager.Instance.StartCoroutine(CoReport(ev.Murderer.VanillaPlayer).WrapToIl2Cpp());
        }

        [Local]
        void OnEnterVent(PlayerVentEnterEvent ev)
        {
            if (MyRole.CanSeeVentFlashOption) AmongUsUtil.PlayQuickFlash(MyRole.UnityColor.AlphaMultiplied(0.3f));
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

