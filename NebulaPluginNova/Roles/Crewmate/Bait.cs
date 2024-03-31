using Nebula.Configuration;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
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

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

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
    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        private IEnumerator CoReport(PlayerControl murderer)
        {
            if(Bait.MyRole.ShowKillFlashOption) AmongUsUtil.PlayQuickFlash(Role.RoleColor);

            float t = Mathf.Max(0.1f, MyRole.ReportDelayOption.GetFloat()) + MyRole.ReportDelayDispersionOption.GetFloat() * (float)System.Random.Shared.NextDouble();
            yield return new WaitForSeconds(t);
            murderer.CmdReportDeadBody(MyPlayer.MyControl.Data);
        }
        void IGamePlayerEntity.OnMurdered(GamePlayer murderer)
        {
            if (murderer.PlayerId == MyPlayer.PlayerId) return;

            if (AmOwner)
            {
                new StaticAchievementToken("bait.common1");
                acTokenChallenge ??= new("bait.challenge", (false,true), (val,_) => val.cleared);
            }

            if (PlayerControl.LocalPlayer.PlayerId == murderer.PlayerId) NebulaManager.Instance.StartCoroutine(CoReport(murderer.VanillaPlayer).WrapToIl2Cpp());
        }

        public override void OnEnterVent(PlayerControl player,Vent vent)
        {
            if (AmOwner && MyRole.CanSeeVentFlashOption) AmongUsUtil.PlayQuickFlash(Role.RoleColor.AlphaMultiplied(0.3f));
        }

        void IGameEntity.OnMeetingEnd(GamePlayer[] exiled)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.triggered = false;
        }

        void IGameEntity.OnPlayerExiled(GamePlayer exiled)
        {
            if (AmOwner)
            {
                if ((acTokenChallenge?.Value.triggered ?? false) && exiled.PlayerId == (MyPlayer.MyKiller?.PlayerId ?? 255))
                    acTokenChallenge.Value.cleared = true;
            }
        }
    }
}

