using Nebula.Game.Statistics;
using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

internal class Vanity : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.vanity", new(170, 141, 34), TeamRevealType.OnlyMe);

    private Vanity() : base("vanity", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [CanExtraWinEvenIfVanityDied, IndependentTeamOption,
        new GroupConfiguration("options.role.vanity.group.task", [BecomeAwareOfWhenKillCrewmateOption, CanBecomeAwareOfOption, TaskProgressOption], GroupConfigurationColor.ToDarkenColor(MyTeam.Color.ToUnityColor()))
        ])
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Sheriff.MyRole.ConfigurationHolder!]);
    }


    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, Sheriff.NumOfShotsOption), arguments.GetAsBool(1, false), arguments.GetAsBool(2, false));

    static internal BoolConfiguration IndependentTeamOption = NebulaAPI.Configurations.Configuration("options.role.vanity.independentTeam", false);
    static private BoolConfiguration BecomeAwareOfWhenKillCrewmateOption = NebulaAPI.Configurations.Configuration("options.role.vanity.becomeAwareOfVanityWhenKillCrewmate", false);
    static private BoolConfiguration CanBecomeAwareOfOption = NebulaAPI.Configurations.Configuration("options.role.vanity.canBecomeAwareOfVanity", false);
    static private FloatConfiguration TaskProgressOption = NebulaAPI.Configurations.Configuration("options.role.vanity.taskProgressRequiredForSelfAdmission", (10, 100, 10), 80, FloatConfigurationDecorator.Percentage, () => CanBecomeAwareOfOption);
    static internal BoolConfiguration CanExtraWinEvenIfVanityDied = NebulaAPI.Configurations.Configuration("options.role.vanity.canExtraWinEvenIfVanityDied", true);

    static public Vanity MyRole = new();
    static private readonly GameStatsEntry StatsKill = NebulaAPI.CreateStatsEntry("stats.vanity.kill", GameStatsCategory.Roles, MyRole);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(Sheriff.Ability.KillButtonSprite, "role.vanity.ability.kill");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%WIN%", Language.Translate(IndependentTeamOption ? "role.vanity.winCond.independent" : "role.vanity.winCond.extra"));
    }

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private readonly RemoteProcess<(GamePlayer player, int leftShot, bool killCrewmate)> RpcUpdateLeftShot = new("vanityLeftShot", (message, _) =>
        {
            if (message.player.Role is Instance vanity)
            {
                vanity.leftShots = message.leftShot;
                vanity.killedCrewmate |= message.killCrewmate;
                if (BecomeAwareOfWhenKillCrewmateOption && vanity.killedCrewmate) vanity.Cognize();
            }
        });

        public Instance(GamePlayer player, int leftShots, bool aware, bool killedCrewmate) : base(player)
        {
            this.leftShots = leftShots;
            this.amAware = aware;
            this.killedCrewmate = killedCrewmate;
        }

        int[]? RuntimeAssignable.RoleArguments => [leftShots, amAware ? 1 : 0, killedCrewmate ? 1 : 0];

        private int leftShots;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                GameObject? lockSprite = null;
                bool killedLastCrewmate = false;
                ModAbilityButton killButton = NebulaAPI.Modules.PlayerlikeKillButton(this, MyPlayer, true,
                    Virial.Compat.VirtualKeyInput.Kill, null, Sheriff.KillCoolDownOption.GetCooldown(MyPlayer.TeamKillCooldown), "kill", ModAbilityButton.LabelType.Standard,
                    Sheriff.Ability.KillButtonSprite, (target, button) =>
                    {
                        using (RPCRouter.CreateSection("vanity"))
                        {
                            if (this.leftShots > 1)
                            {
                                this.leftShots--;
                                button.UpdateUsesIcon(this.leftShots.ToString());
                            }
                            else
                            {
                                button.HideUsesIcon();
                            }

                            RpcUpdateLeftShot.Invoke((MyPlayer, this.leftShots, target.RealPlayer.IsTrueCrewmate));
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill, result =>
                            {
                                if(result == KillResult.Kill)
                                {
                                    StatsKill.Progress();
                                    if(target.RealPlayer.IsTrueCrewmate && GamePlayer.AllPlayers.All(p => p.IsDead || !p.FeelBeTrueCrewmate || p.AmOwner))
                                    {
                                        killedLastCrewmate = true;
                                    }
                                }
                            });
                            button.StartCoolDown();
                        }

                    }, availability: _ => !MyPlayer.WillDie && lockSprite == null, canTrackInVentPlayer: Sheriff.CanKillHidingPlayerOption);
                killButton.ShowUsesIcon(this.leftShots.ToString(), MyRole.RoleColor);
                if (Sheriff.SealAbilityUntilReportingDeadBodiesOption && !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => p.IsDead)) lockSprite = killButton.AddLockedOverlay();

                GameOperatorManager.Instance?.SubscribeAchievement<GameEndEvent>("vanity.challenge", ev => killedLastCrewmate && ev.EndState.Winners.Test(MyPlayer), this);

                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
                    if (lockSprite && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => p.IsDead))
                    {
                        GameObject.Destroy(lockSprite);
                        lockSprite = null;
                    }
                }, killButton);
            }
            MyPlayer.FeelBeTrueCrewmate = !amAware;
        }

        [Local]
        void OnShowIntro(GameShowIntroLocalEvent ev)
        {
            ev.SetTeam(Crewmate.Crewmate.MyTeam);
            ev.SetRole(Crewmate.Sheriff.MyRole);
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            ev.SetWinIf(killedCrewmate && IndependentTeamOption && ev.GameEnd == NebulaGameEnd.VanityWin && !MyPlayer.IsDead);
        }

        [OnlyMyPlayer]
        void OnCheckExtraWin(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.VanityPhase) return;
            if (!CanExtraWinEvenIfVanityDied && MyPlayer.IsDead) return;

            if(
                (killedCrewmate && !IndependentTeamOption && ev.GameEnd != NebulaGameEnd.CrewmateWin) ||
                (!killedCrewmate && ev.GameEnd == NebulaGameEnd.CrewmateWin)
                )
            {
                ev.SetWin(true);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraVanityWin);
            }
        }

        void ClaimJackalRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == MyTeam) return;
            if (!IndependentTeamOption) return;
            if (!killedCrewmate) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        [OnlyMyPlayer]
        void OnTaskUpdate(PlayerTaskUpdateEvent ev)
        {
            if (!CanBecomeAwareOfOption) return;
            if (MyPlayer.Tasks.CurrentTasks == 0) return;
            if (!(((float)MyPlayer.Tasks.CurrentCompleted / (float)MyPlayer.Tasks.CurrentTasks) < TaskProgressOption / 100f))
            {
                Cognize();
            }
        }

        private void Cognize()
        {
            if (!amAware)
            {
                amAware = true;
                MyPlayer.FeelBeTrueCrewmate = false;
                if (MyPlayer.AmOwner || NebulaGameManager.Instance!.CanSeeAllInfo) AnimationEffects.CoPlayRoleNameEffect(GamePlayer.LocalPlayer!).StartOnScene();
                if (MyPlayer.AmOwner) new StaticAchievementToken("vanity.common1");
            }
        }

        [OnlyMyPlayer, Local]
        private void OnDied(PlayerDieEvent ev)
        {
            if (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled) return;
            if (!killedCrewmate) return;
            new StaticAchievementToken("vanity.another1");
        }

        [OnlyHost]
        void CheckKillWin(GameUpdateEvent ev)
        {
            if (!IndependentTeamOption) return;
            if (MyPlayer.IsDead) return;
            if (!(ev.Game.GameMode?.AllowSpecialGameEnd ?? false)) return;

            //他陣営が生き残っていれば勝利しない
            if (GameOperatorManager.Instance?.Run(new KillerTeamCallback(MyTeam)).RemainingOtherTeam ?? false) return;
            
            int vanities = 0;
            int totalAlive = 0;

            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead) continue;

                totalAlive++;
                if (p.Role.Role == MyRole) vanities++;
            }

            if (totalAlive <= 2 && vanities == 1) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.VanityWin, GameEndReason.Situation, BitMasks.AsPlayer().Add(MyPlayer));
        }



        bool killedCrewmate = false;
        bool amAware = false;
        bool ShouldShowVanityRole => amAware;
        bool ShouldShowAnnotatedRole => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;

        string RuntimeAssignable.DisplayName =>
            ShouldShowVanityRole ? (MyRole as DefinedRole).DisplayName :
            ShouldShowAnnotatedRole ? (Sheriff.MyRole as DefinedRole).DisplayName + " (" + (MyRole as DefinedRole).DisplayName + ")" :
            (Sheriff.MyRole as DefinedRole).DisplayName;
        string RuntimeAssignable.DisplayColoredName =>
            ShouldShowVanityRole ? (MyRole as DefinedRole).DisplayColoredName :
            ShouldShowAnnotatedRole ? (Sheriff.MyRole as DefinedRole).DisplayColoredName + " (" + (MyRole as DefinedRole).DisplayColoredName + ")" :
            (Sheriff.MyRole as DefinedRole).DisplayColoredName;

        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => ShouldShowVanityRole ? [MyRole] : [Sheriff.MyRole];
        (Image, Virial.Color?, Virial.Color?)? RuntimeAssignable.OverriddenRoleIcon => (Sheriff.MyRole.GetRoleIcon()!, (Sheriff.MyRole as DefinedRole).Color, null);
        RoleTaskType RuntimeRole.TaskType => amAware ? RoleTaskType.NoTask : RoleTaskType.CrewmateTask;
        bool RuntimeAssignable.MyCrewmateTaskIsIgnored => true;
    }
}
