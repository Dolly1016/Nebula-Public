﻿using Epic.OnlineServices.Stats;
using Nebula.Configuration;
using System;
using Virial.Assignable;

namespace Nebula.Roles.Crewmate;

public class Sheriff : ConfigurableStandardRole
{
    static public Sheriff MyRole = new Sheriff();

    public override RoleCategory RoleCategory => RoleCategory.CrewmateRole;

    public override string LocalizedName => "sheriff";
    public override Color RoleColor => new Color(240f / 255f, 191f / 255f, 0f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player,arguments);

    private KillCoolDownConfiguration KillCoolDownOption = null!;
    private NebulaConfiguration NumOfShotsOption = null!;
    private NebulaConfiguration CanKillMadmateOption = null!;
    private NebulaConfiguration CanKillHidingPlayerOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        KillCoolDownOption = new(RoleConfig, "killCoolDown", KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        NumOfShotsOption = new(RoleConfig, "numOfShots", null, 1, 15, 3, 3);
        CanKillMadmateOption = new(RoleConfig, "canKillMadmate", null, false, false);
        CanKillHidingPlayerOption = new(RoleConfig, "canKillHidingPlayer", null, false, false);
    }

    public class Instance : Crewmate.Instance
    {
        private ModAbilityButton? killButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SheriffKillButton.png", 100f);
        public override AbstractRole Role => MyRole;
        private int leftShots = MyRole.NumOfShotsOption;
        public Instance(PlayerModInfo player, int[] arguments) : base(player)
        {
            if(arguments.Length >= 1) leftShots = arguments[0];
        }
        public override int[]? GetRoleArgument() => new int[] { leftShots };

        private bool CanKill(PlayerControl target)
        {
            var info = target.GetModInfo();
            if (info == null) return true;
            if (info.Role.Role == Madmate.MyRole) return Sheriff.MyRole.CanKillMadmateOption;
            if (info.Role.Role.RoleCategory == RoleCategory.CrewmateRole) return false;
            return true;
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead, MyRole.CanKillHidingPlayerOption));
                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);

                var leftText = killButton.ShowUsesIcon(3);
                leftText.text = leftShots.ToString();

                killButton.SetSprite(buttonSprite.GetSprite());
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                killButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && leftShots > 0;
                killButton.OnClick = (button) => {
                    if (CanKill(killTracker.CurrentTarget!)) 
                        MyPlayer.MyControl.ModKill(killTracker.CurrentTarget!, true, PlayerState.Dead, EventDetail.Kill); 
                    else
                    {
                        MyPlayer.MyControl.ModSuicide(false, PlayerState.Misfired, null);
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Misfire, MyPlayer.MyControl, killTracker.CurrentTarget!);
                    }
                    button.StartCoolDown();

                    leftText.text = (--leftShots).ToString();
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabel("kill");
            }
        }

    }
}

