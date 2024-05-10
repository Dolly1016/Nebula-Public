using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Bait : ConfigurableStandardRole, HasCitation
{
    static public Bait MyRole = new Bait();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "bait";
    public override Color RoleColor => new Color(0f / 255f, 247f / 255f, 255f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration ShowKillFlashOption = null!;
    private NebulaConfiguration ReportDelayOption = null!;
    private NebulaConfiguration ReportDelayDispersionOption = null!;
    private NebulaConfiguration CanSeeVentFlashOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        ShowKillFlashOption = new(RoleConfig, "showKillFlash", null, false, false);
        ReportDelayOption = new(RoleConfig, "reportDelay", null, 0f, 5f, 0.5f, 0f, 0f) { Decorator = NebulaConfiguration.SecDecorator };
        ReportDelayDispersionOption = new(RoleConfig, "reportDelayDispersion", null, 0f, 10f, 0.25f, 0.5f, 0.5f) { Decorator = NebulaConfiguration.SecDecorator };
        CanSeeVentFlashOption = new(RoleConfig, "canSeeVentFlash", null, false, false);

    }

    [NebulaRPCHolder]
    public class Instance : Crewmate.Instance, IBindPlayer
    {
        public override AbstractRole Role => MyRole;
        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        public Instance(GamePlayer player) : base(player)
        {
        }

        private IEnumerator CoReport(PlayerControl murderer)
        {
            if(Bait.MyRole.ShowKillFlashOption) AmongUsUtil.PlayQuickFlash(Role.RoleColor);

            float t = Mathf.Max(0.1f, MyRole.ReportDelayOption.GetFloat()) + MyRole.ReportDelayDispersionOption.GetFloat() * (float)System.Random.Shared.NextDouble();
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

        public override void OnEnterVent(PlayerControl player,Vent vent)
        {
            if (AmOwner && MyRole.CanSeeVentFlashOption) AmongUsUtil.PlayQuickFlash(Role.RoleColor.AlphaMultiplied(0.3f));
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

