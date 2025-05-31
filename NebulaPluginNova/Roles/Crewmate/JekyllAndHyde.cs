using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using System.ComponentModel;
using Nebula.Game.Statistics;
using Virial.Text;
using Virial.Events.Game;

namespace Nebula.Roles.Crewmate;

internal class JekyllAndHyde : DefinedRoleTemplate, DefinedRole
{
    private JekyllAndHyde() : base("jekyllAndHyde", Virial.Color.White, RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfKillOption, KillCoolDownOption, HasImpostorVisionOption, CanUseVentsOption, CanMoveInVentsOption, TaskConfiguration.AsGroup(new(GroupConfigurationColor.Gray))])
    {
    }

    static private UnityEngine.Color GetRoleColor(bool isJekyll) => isJekyll ? Palette.CrewmateBlue : Palette.ImpostorRed;
    static private string JekyllDisplayName => Language.Translate("role.jekyllAndHyde.name.jekyll");
    static private string HydeDisplayName => Language.Translate("role.jekyllAndHyde.name.hyde");
    static private string JekyllDisplayColoredName => Language.Translate("role.jekyllAndHyde.name.jekyll").Color(Palette.CrewmateBlue);
    static private string HydeDisplayColoredName => Language.Translate("role.jekyllAndHyde.name.hyde").Color(Palette.ImpostorRed);
    static private string JekyllDisplayShort => Language.Translate("role.jekyllAndHyde.short.jekyll");
    static private string HydeDisplayShort => Language.Translate("role.jekyllAndHyde.short.hyde");
    static private string JAndHCombinationName => Language.Translate("role.jekyllAndHyde.name.combination");
    static private string JAndHCombinationShort => Language.Translate("role.jekyllAndHyde.short.combination");
    string DefinedRole.DisplayIntroBlurb => Language.Translate("role.jekyllAndHyde.blurb").Replace("%J%", Palette.CrewmateBlue.ToTextColor()).Replace("%H%", Palette.ImpostorRed.ToTextColor()).Replace("%R%", "</color>");
    string DefinedAssignable.DisplayName => JAndHCombinationName.Replace("%J%", JekyllDisplayName).Replace("%H%", HydeDisplayName);
    string DefinedAssignable.DisplayColoredName => JAndHCombinationName.Replace("%J%", JekyllDisplayName.Color(Palette.CrewmateBlue)).Replace("%H%", HydeDisplayName.Color(Palette.ImpostorRed));
    string DefinedCategorizedAssignable.DisplayShort => JAndHCombinationShort.Replace("%J%", JekyllDisplayShort).Replace("%H%", HydeDisplayShort);
    string DefinedCategorizedAssignable.DisplayColoredShort => JAndHCombinationShort.Replace("%J%", JekyllDisplayShort.Color(Palette.CrewmateBlue)).Replace("%H%", HydeDisplayShort.Color(Palette.ImpostorRed));
    static private readonly IntegerConfiguration NumOfKillOption = NebulaAPI.Configurations.Configuration("options.role.jekyllAndHyde.numOfKill", (0, 5), 1);
    static private readonly IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.jekyllAndHyde.killCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 30f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f, () => NumOfKillOption > 0);
    static private readonly BoolConfiguration HasImpostorVisionOption = NebulaAPI.Configurations.Configuration("options.role.jekyllAndHyde.hasImpostorVision", false);
    static private readonly BoolConfiguration CanUseVentsOption = NebulaAPI.Configurations.Configuration("options.role.jekyllAndHyde.canUseVents", false);
    static private readonly BoolConfiguration CanMoveInVentsOption = NebulaAPI.Configurations.Configuration("options.role.jekyllAndHyde.canMoveInVents", false);
    static private readonly ITaskConfiguration TaskConfiguration = NebulaAPI.Configurations.TaskConfiguration("options.role.jekyllAndHyde.task", true, true);

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    bool DefinedRole.IsMadmate => true;
    static public readonly JekyllAndHyde MyRole = new();
    static private readonly GameStatsEntry StatsMedicine = NebulaAPI.CreateStatsEntry("stats.jekyllAndHyde.medicine", GameStatsCategory.Roles, MyRole);
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;


        public Instance(GamePlayer player, int[] argument) : base(player)
        {
            LeftKill = argument.Get(0, NumOfKillOption);
            UsedDrug = argument.Get(1, 0) == 1;
        }

        int[] RuntimeAssignable.RoleArguments => [LeftKill, UsedDrug ? 1 : 0];

        int LeftKill = 0;
        bool UsedDrug = false;
        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.JekyllDrugButton.png", 115f);
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                //var predicate = ObjectTrackers.KillablePredicate(MyPlayer);
                //var killTracker = NebulaAPI.Modules.KillTracker(this, MyPlayer, _ => !AmJekyll);

                var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true,
                    Virial.Compat.VirtualKeyInput.Kill, KillCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "kill", Virial.Components.ModAbilityButton.LabelType.Impostor,
                    null, (player, button) =>
                    {
                        using (RPCRouter.CreateSection("HydeKill"))
                        {
                            RpcDetermineState.Invoke((MyPlayer, false));

                            var target = player;
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill, result =>
                            {
                                if (target.IsCrewmate) GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                                {
                                    if (ev.EndState.EndCondition == NebulaGameEnd.CrewmateWin && AmJekyll && ev.EndState.Winners.Test(MyPlayer)) new StaticAchievementToken("jekyllAndHyde.challenge");
                                }, this);
                            });
                            NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                        }
                        RpcUpdateLeftKill.Invoke(MyPlayer);
                        button.UpdateUsesIcon(LeftKill.ToString());
                        button.StartCoolDown();
                    }, _ => !AmJekyll, _ => LeftKill > 0, _ => !AmJekyll
                    );
                killButton.ShowUsesIcon(0, LeftKill.ToString());
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());

                var medicineButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    5f, "medicine", buttonSprite,
                    _ => MyPlayer.Tasks.IsCompletedCurrentTasks, _ => AmJekyll && !UsedDrug);
                medicineButton.OnClick = (button) =>
                {
                    StatsMedicine.Progress();
                    RpcDetermineState.Invoke((MyPlayer, true));
                };
                var lockSprite = medicineButton.AddLockedOverlay();
                GameOperatorManager.Instance?.Subscribe<PlayerTaskCompleteLocalEvent>(ev =>
                {
                    if (MyPlayer.Tasks.IsCompletedCurrentTasks && lockSprite) GameObject.Destroy(lockSprite);
                }, this);
            }

        }

        [Local]
        void OnGameStart(GameStartEvent ev)
        {
            using (RPCRouter.CreateSection("JAHTask"))
            {
                TaskConfiguration.GetTasks(out var s, out var l, out var c);
                MyPlayer.Tasks.Unbox().ReplaceTasksAndRecompute(s, l, c);
                MyPlayer.Tasks.Unbox().BecomeToOutsider();

                if (Helpers.Prob(0.5f)) RpcDetermineState.Invoke((MyPlayer, false));
            }
        }


        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
        {
            ev.SetWinIf((ev.GameEnd == (AmJekyll ? NebulaGameEnd.CrewmateWin : NebulaGameEnd.ImpostorWin)) && (!AmJekyll || MyPlayer.Tasks.IsCompletedCurrentTasks));
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
        {
            ev.SetBlockedIf(ev.GameEnd == (AmJekyll ? NebulaGameEnd.ImpostorWin : NebulaGameEnd.CrewmateWin));
            ev.SetBlockedIf(ev.GameEnd == NebulaGameEnd.CrewmateWin && !MyPlayer.Tasks.IsCompletedCurrentTasks);
        }

        [OnlyHost, OnlyMyPlayer]
        void OnMurderImpostor(PlayerKillPlayerEvent ev)
        {
            if (ev.Dead.IsImpostor && !AmJekyll)
            {
                MyPlayer.Unbox().RpcInvokerSetRole(Impostor.HydeImpostor.MyRole, null).InvokeSingle();
                NebulaAchievementManager.RpcClearAchievement.Invoke(("jekyllAndHyde.common2", MyPlayer));
            }
            if(ev.Dead.IsCrewmate && AmJekyll)
            {
                NebulaAchievementManager.RpcClearAchievement.Invoke(("jekyllAndHyde.common3", MyPlayer));
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (MyPlayer.IsDead && !ev.EndState.Winners.Test(MyPlayer) && !AmJekyll) new StaticAchievementToken("jekyllAndHyde.another1");
        } 

        public bool AmJekyll { get; private set; } = true;
        void OnMeeting(MeetingEndEvent ev)
        {
            if (MyPlayer.IsDead) return;

            AmJekyll = !AmJekyll;
        }

        [Local]
        void OnMeetingEnd(FixExileTextEvent ev)
        {
            if (MyPlayer.IsDead || ev.Exiled.Any(p => p.AmOwner)) return;
            var nextIsJekyll = !AmJekyll;
            ev.AddText(Language.Translate("role.jekyllAndHyde.changedToText").Replace("%R%", nextIsJekyll ? JekyllDisplayColoredName : HydeDisplayColoredName));
        }

        string RuntimeAssignable.DisplayName => AmJekyll ? JekyllDisplayName : HydeDisplayName;
        string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color(GetRoleColor(AmJekyll));

        static private RemoteProcess<GamePlayer> RpcUpdateLeftKill = new("UpdateJAHLeftKill", (player, _) =>
        {
            if(player.Role is JekyllAndHyde.Instance jah) jah.LeftKill--;
        });
        static private RemoteProcess<(GamePlayer player, bool byDrug)> RpcDetermineState = new("DetermineJAHState", (message, _) =>
        {
            if (message.player.Role is JekyllAndHyde.Instance jah)
            {
                jah.AmJekyll = false;
                if (message.byDrug) jah.UsedDrug = true;
            }
        });

        bool RuntimeRole.CanMoveInVent => !AmJekyll && CanMoveInVentsOption;
        bool RuntimeRole.CanUseVent => !AmJekyll && CanUseVentsOption;
        bool RuntimeRole.HasImpostorVision => !AmJekyll && HasImpostorVisionOption;
        bool RuntimeRole.IgnoreBlackout => !AmJekyll && HasImpostorVisionOption;
        RoleTaskType RuntimeRole.TaskType => RoleTaskType.RoleTask;
    }
}