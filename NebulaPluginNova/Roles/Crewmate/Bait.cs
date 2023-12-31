﻿using Nebula.Configuration;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Crewmate;

public class Bait : ConfigurableStandardRole
{
    static public Bait MyRole = new Bait();

    public override RoleCategory RoleCategory => RoleCategory.CrewmateRole;

    public override string LocalizedName => "bait";
    public override Color RoleColor => new Color(0f / 255f, 247f / 255f, 255f / 255f);
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

    public class Instance : Crewmate.Instance
    {
        public override AbstractRole Role => MyRole;
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
        public override void OnMurdered(PlayerControl murderer)
        {
            if (murderer.PlayerId == MyPlayer.PlayerId) return;

            if (PlayerControl.LocalPlayer.PlayerId == murderer.PlayerId) NebulaManager.Instance.StartCoroutine(CoReport(murderer).WrapToIl2Cpp());
        }

        public override void OnEnterVent(PlayerControl player,Vent vent)
        {
            if (AmOwner && MyRole.CanSeeVentFlashOption) AmongUsUtil.PlayQuickFlash(Role.RoleColor.AlphaMultiplied(0.3f));
        }
    }
}

