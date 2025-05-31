
using Nebula.Game.Statistics;
using Nebula.Roles.Abilities;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Avenger : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.avenger", new(141,111,131), TeamRevealType.OnlyMe);
    private Avenger() : base("avenger", MyTeam.Color, RoleCategory.NeutralRole, MyTeam,
        [KillCoolDownOption, VentOption, CanKnowExistanceOfAvengerOption, TargetCanKnowAvengerOption, AvengerFlashForMurdererOption, NotificationForMurdererIntervalOption, NotificationForAvengerIntervalOption],
        false, optionHolderPredicate: () => (Modifier.Lover.NumOfPairsOption > 0 && Modifier.Lover.AvengerModeOption))
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [Modifier.Lover.MyRole.ConfigurationHolder!]);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Avenger.png");
    }

    AllocationParameters? DefinedSingleAssignable.AllocationParameters => null;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, (byte)arguments.Get(0, -1));

    static public ValueConfiguration<int> CanKnowExistanceOfAvengerOption = NebulaAPI.Configurations.Configuration("options.role.avenger.canKnowExistanceOfAvenger", ["options.role.avenger.canKnowExistanceOfAvenger.off", "options.role.avenger.canKnowExistanceOfAvenger.onlyTarget", "options.role.avenger.canKnowExistanceOfAvenger.on"], 0);
    static private BoolConfiguration TargetCanKnowAvengerOption = NebulaAPI.Configurations.Configuration("options.role.avenger.targetCanKnowAvenger", true);
    static private BoolConfiguration AvengerFlashForMurdererOption = NebulaAPI.Configurations.Configuration("options.role.avenger.showAvengerFlashForTarget", true);
    static private FloatConfiguration NotificationForAvengerIntervalOption = NebulaAPI.Configurations.Configuration("options.role.avenger.notificationForAvengerInterval", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration NotificationForMurdererIntervalOption = NebulaAPI.Configurations.Configuration("options.role.avenger.notificationForTargetInterval", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.avenger.killCoolDown", CoolDownType.Relative, (2.5f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static private IVentConfiguration VentOption = NebulaAPI.Configurations.NeutralVentConfiguration("options.role.avenger.vent", false);

    static public Avenger MyRole = new Avenger();

    static private GameStatsEntry StatsKillTarget = NebulaAPI.CreateStatsEntry("stats.avenger.killTarget", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsFailed = NebulaAPI.CreateStatsEntry("stats.avenger.failed", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsDefendAsKiller = NebulaAPI.CreateStatsEntry("stats.avenger.defend", GameStatsCategory.Roles, MyRole);
    bool IGuessed.CanBeGuessDefault => false;

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private GamePlayer? target;
        public GamePlayer? AvengerTarget => target;
        public Instance(GamePlayer player,byte targetId) : base(player, VentOption)
        {
            target = NebulaGameManager.Instance?.GetPlayer(targetId);
        }

        int[]? RuntimeAssignable.RoleArguments => [target?.PlayerId ?? 255];
        public override void OnActivated()
        {
            if (AmOwner)
            {
                NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText("You became AVENGER.", MyRole.RoleColor.ToUnityColor(), 5.5f, true);
                AmongUsUtil.PlayCustomFlash(MyRole.RoleColor.ToUnityColor(), 0f, 0.8f, 0.7f);

                var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill,
                        KillCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "kill", ModAbilityButton.LabelType.Impostor, null,
                        (player, button) => {
                            MyPlayer.MurderPlayer(player, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                            button.StartCoolDown();
                        }
                    );
                
                if (target != null) new TrackingArrowAbility(target, NotificationForAvengerIntervalOption, MyRole.RoleColor.ToUnityColor(), false).Register(this);

                if((target?.TryGetModifier<Damned.Instance>(out var damned) ?? false) && MyPlayer.TryGetModifier<Lover.Instance>(out var lover))
                {
                    var myLover = lover.MyLover.Get();
                    var targetRole = target.Role.Role;
                    if (myLover != null) {
                        GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                        {
                            var winners = ev.EndState.Winners;
                            if (
                            target.IsDead && target.MyKiller == MyPlayer &&
                            MyPlayer.Role.Role == targetRole &&
                            winners.Test(MyPlayer) && !winners.Test(myLover))
                            {
                                new StaticAchievementToken("combination.2.damned.avenger.hard");
                            }
                        }, NebulaAPI.CurrentGame!);
                    }
                }
            }

            if (target?.AmOwner ?? false)
            {
                if (TargetCanKnowAvengerOption) new TrackingArrowAbility(MyPlayer, NotificationForMurdererIntervalOption, MyRole.RoleColor.ToUnityColor(), false).Register(this);
                if (AvengerFlashForMurdererOption) AmongUsUtil.PlayFlash(MyRole.RoleColor.ToUnityColor());
            }
        }

        private bool KillCondition => (target?.IsDead ?? false) && target?.MyKiller == MyPlayer;
        private bool CheckKillCondition => KillCondition && !MyPlayer.IsDead;
        void OnCheckGameEnd(EndCriteriaMetEvent ev)
        {
            if (CheckKillCondition) ev.TryOverwriteEnd(NebulaGameEnd.AvengerWin, GameEndReason.Special);
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.AvengerWin && CheckKillCondition);
        

        [OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer == target)
            {
                if (ev.Murderer.AmOwner)
                {
                    new StaticAchievementToken("avenger.common2");
                    StatsDefendAsKiller.Progress();
                }
                if (AmOwner)
                {
                    new StaticAchievementToken("avenger.another1");
                    StatsFailed.Progress();
                }
            }
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if(CheckKillCondition) new StaticAchievementToken("avenger.another2");
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (ev.Dead == this.target)
            {
                new StaticAchievementToken("avenger.common1");
                StatsKillTarget.Progress();
            }
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (target == ev.Player && !MyPlayer.IsDead)
                MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndCondition == NebulaGameEnd.AvengerWin && ev.EndState.Winners.Test(MyPlayer))
            {
                if(MyPlayer.Unbox().GetModifiers<Lover.Instance>().Any(l => l.MyLover.Get()?.Role.Role is Avenger)) new StaticAchievementToken("avenger.challenge");
            }
        }

        [OnlyHost]
        void OnTargetDead(PlayerDieOrDisconnectEvent ev)
        {
            if (!CheckKillCondition && ev.Player == target && !MyPlayer.IsDead && ev is PlayerMurderedEvent or PlayerDisconnectEvent)
            {
                if (MeetingHud.Instance || ExileController.Instance)
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole);
                }
                else
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                }
                NebulaAchievementManager.RpcClearAchievement.Invoke(("avenger.another1", MyPlayer));

            }
        }
    }
}
