using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Madmate : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public Madmate MyRole = new Madmate();
    private Madmate() : base("madmate", new(Palette.ImpostorRed), RoleCategory.CrewmateRole, Crewmate.MyTeam, [CanFixLightOption, CanFixCommsOption, HasImpostorVisionOption, CanUseVentsOption, CanMoveInVentsOption, EmbroilVotersOnExileOption, LimitEmbroiledPlayersToVotersOption, CanIdentifyImpostorsOption]) { }
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGM;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration EmbroilVotersOnExileOption = new BoolConfigurationImpl("role.madmate.embroilPlayersOnExile", false);
    static private BoolConfiguration LimitEmbroiledPlayersToVotersOption = new BoolConfigurationImpl("role.madmate.limitEmbroiledPlayersToVoters", true);

    static private BoolConfiguration CanFixLightOption = new BoolConfigurationImpl("role.madmate.canFixLight", false);
    static private BoolConfiguration CanFixCommsOption = new BoolConfigurationImpl("role.madmate.canFixComms", false);
    static private BoolConfiguration HasImpostorVisionOption = new BoolConfigurationImpl("role.madmate.hasImpostorVision", false);
    static private BoolConfiguration CanUseVentsOption = new BoolConfigurationImpl("role.madmate.canUseVents", false);
    static private BoolConfiguration CanMoveInVentsOption = new BoolConfigurationImpl("role.madmate.canMoveInVents", false);
    static private IntegerConfiguration CanIdentifyImpostorsOption = new IntegerConfigurationImpl("role.madmate.canIdentifyImpostors", ArrayHelper.Selection(0, 3), 0);
    static private NebulaConfiguration[] NumOfTasksToIdentifyImpostorsOptions = null!; //TODO


    protected override void LoadOptions()
    {
        NumOfTasksToIdentifyImpostorsOptions = new NebulaConfiguration[]{
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors0",null,0,10,2,2),
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors1",null,0,10,4,4),
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors2",null,0,10,6,6)
        };

        CanIdentifyImpostorsOption.Shower = () =>
        {
            return CanIdentifyImpostorsOption.DefaultShowerString
                + StringExtensions.Color(
                    " (" +
                    NumOfTasksToIdentifyImpostorsOptions
                        .Take(CanIdentifyImpostorsOption.GetMappedInt())
                        .Join(option => option.ToDisplayString(), ", ")
                    + ")", Color.gray);
        };

        foreach (var option in NumOfTasksToIdentifyImpostorsOptions) { option.Shower = () => null; option.Editor = NebulaConfiguration.EmptyEditor; }

        new NebulaConfiguration(RoleConfig, () =>
        {
            if (CanIdentifyImpostorsOption.CurrentValue == 0) return null;

            List<IMetaParallelPlacableOld> placable = new();

            for (int i = 0; i < CanIdentifyImpostorsOption.CurrentValue; i++)
            {
                if (i != 0) placable.Add(new MetaWidgetOld.HorizonalMargin(0.25f));

                var option = NumOfTasksToIdentifyImpostorsOptions[i];
                placable.Add(NebulaConfiguration.OptionButtonWidget(() => option.ChangeValue(false), "<<"));
                placable.Add(new MetaWidgetOld.Text(NebulaConfiguration.OptionShortValueAttr) { RawText = option.ToDisplayString() });
                placable.Add(NebulaConfiguration.OptionButtonWidget(() => option.ChangeValue(true), ">>"));
            }

            return new CombinedWidgetOld(placable.ToArray());
        });
    }
    
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        List<byte> impostors = new();

        public Instance(GamePlayer player) : base(player) {}

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.IsWin |= ev.GameEnd == NebulaGameEnd.ImpostorWin;

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= ev.GameEnd == NebulaGameEnd.CrewmateWin;


        public override bool HasCrewmateTasks => false;

        void SetMadmateTask()
        {
            if (AmOwner)
            {
                var numOfTasksOptions = MyRole.NumOfTasksToIdentifyImpostorsOptions.Take(MyRole.CanIdentifyImpostorsOption.GetMappedInt());
                int max = numOfTasksOptions.Max(option => option.GetMappedInt());

                using (RPCRouter.CreateSection("MadmateTask"))
                {
                    MyPlayer.Tasks.Unbox().ReplaceTasksAndRecompute(max, 0, 0);
                    MyPlayer.Tasks.Unbox().BecomeToOutsider();
                }
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            SetMadmateTask();
            if(AmOwner) IdentifyImpostors();
        }

        public void OnGameStart(GameStartEvent ev)
        {
            SetMadmateTask();
            if (AmOwner) IdentifyImpostors();
        }

        private void IdentifyImpostors()
        {
            //インポスター判別のチャンスだけ繰り返す
            while (CanIdentifyImpostorsOption > impostors.Count && MyPlayer.Tasks.CurrentCompleted >= MyRole.NumOfTasksToIdentifyImpostorsOptions[impostors.Count].GetMappedInt())
            {
                var pool = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole && !impostors.Contains(p.PlayerId)).ToArray();
                //候補が残っていなければ何もしない
                if (pool.Length == 0) return;
                impostors.Add(pool[System.Random.Shared.Next(pool.Length)].PlayerId);
            }
        }

        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev) => IdentifyImpostors();
        

        public override void DecorateOtherPlayerName(GamePlayer player, ref string text, ref Color color)
        {
            if (impostors.Contains(player.PlayerId) && player.Role.Role.Category == RoleCategory.ImpostorRole) color = Palette.ImpostorRed;
        }

        [Local, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (NebulaGameManager.Instance!.AllPlayerInfo().Any(p => !p.IsDead && p.Role.Role.Category == RoleCategory.ImpostorRole))
                new StaticAchievementToken("madmate.common1");

            if (!EmbroilVotersOnExileOption) return;

            if (LimitEmbroiledPlayersToVotersOption)
                ExtraExileRoleSystem.MarkExtraVictim(MyPlayer.Unbox(), false, true);
            else
            {
                var voters = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && !p.AmOwner && p.Role.Role.Category != RoleCategory.ImpostorRole).ToArray();
                if (voters.Length > 0) voters.Random().VanillaPlayer.ModMarkAsExtraVictim(MyPlayer.VanillaPlayer, PlayerState.Embroiled, EventDetail.Embroil);
            }

            

        }

        [Local, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if(ev.Murderer?.Role.Role.Category == RoleCategory.ImpostorRole)
                new StaticAchievementToken("madmate.another1");
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin && NebulaGameManager.Instance!.AllPlayerInfo().All(p=>p.Role.Role.Category != RoleCategory.ImpostorRole || !p.IsDead))
                new StaticAchievementToken("madmate.challenge");
        }

        RoleTaskType RuntimeRole.TaskType => CanIdentifyImpostorsOption > 0 ? RoleTaskType.RoleTask : RoleTaskType.NoTask;

        bool RuntimeAssignable.CanFixComm => CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => CanFixLightOption;
        bool RuntimeRole.CanMoveInVent => CanMoveInVentsOption;
        bool RuntimeRole.CanUseVent => CanUseVentsOption;
        bool RuntimeRole.HasImpostorVision => HasImpostorVisionOption;
        bool RuntimeRole.IgnoreBlackout => HasImpostorVisionOption;
    }
}

