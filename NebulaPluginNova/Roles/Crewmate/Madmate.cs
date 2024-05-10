using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Madmate : ConfigurableStandardRole, HasCitation
{
    static public Madmate MyRole = new Madmate();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "madmate";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGM;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EmbroilVotersOnExileOption = null!;
    private NebulaConfiguration LimitEmbroiledPlayersToVotersOption = null!;

    private NebulaConfiguration CanFixLightOption = null!;
    private NebulaConfiguration CanFixCommsOption = null!;
    private NebulaConfiguration HasImpostorVisionOption = null!;
    private NebulaConfiguration CanUseVentsOption = null!;
    private NebulaConfiguration CanMoveInVentsOption = null!;
    private NebulaConfiguration CanIdentifyImpostorsOption = null!;
    private NebulaConfiguration[] NumOfTasksToIdentifyImpostorsOptions = null!;


    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanFixLightOption = new NebulaConfiguration(RoleConfig, "canFixLight", null, false, false);
        CanFixCommsOption = new NebulaConfiguration(RoleConfig, "canFixComms", null, false, false);
        HasImpostorVisionOption = new NebulaConfiguration(RoleConfig, "hasImpostorVision", null, false, false);
        CanUseVentsOption = new NebulaConfiguration(RoleConfig, "canUseVents", null, false, false);
        CanMoveInVentsOption = new NebulaConfiguration(RoleConfig, "canMoveInVents", null, false, false);

        EmbroilVotersOnExileOption = new NebulaConfiguration(RoleConfig, "embroilPlayersOnExile", null, false, false);
        LimitEmbroiledPlayersToVotersOption = new NebulaConfiguration(RoleConfig, "limitEmbroiledPlayersToVoters", null, true, true);

        CanIdentifyImpostorsOption = new NebulaConfiguration(RoleConfig, "canIdentifyImpostors", null, 3, 0, 0);

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
    
    public class Instance : Crewmate.Instance, IBindPlayer, RuntimeRole
    {
        List<byte> impostors = new();

        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWin(ev.GameEnd == NebulaGameEnd.ImpostorWin);

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

        public override void OnActivated()
        {
            base.OnActivated();

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
            while (MyRole.CanIdentifyImpostorsOption.GetMappedInt() > impostors.Count && MyPlayer.Tasks.CurrentCompleted >= MyRole.NumOfTasksToIdentifyImpostorsOptions[impostors.Count].GetMappedInt())
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

            if (!MyRole.EmbroilVotersOnExileOption) return;

            if (MyRole.LimitEmbroiledPlayersToVotersOption)
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

        public override void OnGameEnd(EndState endState)
        {
            if(AmOwner && endState.EndCondition == NebulaGameEnd.ImpostorWin && NebulaGameManager.Instance!.AllPlayerInfo().All(p=>p.Role.Role.Category != RoleCategory.ImpostorRole || !p.IsDead))
                new StaticAchievementToken("madmate.challenge");
        }

        public override bool HasAnyTasks => MyRole.CanIdentifyImpostorsOption.GetMappedInt() > 0;

        bool RuntimeAssignable.CanFixComm => MyRole.CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => MyRole.CanFixLightOption;
        bool RuntimeRole.CanMoveInVent => MyRole.CanMoveInVentsOption;
        bool RuntimeRole.CanUseVent => MyRole.CanUseVentsOption;
        bool RuntimeRole.HasImpostorVision => MyRole.HasImpostorVisionOption;
        bool RuntimeRole.IgnoreBlackout => MyRole.HasImpostorVisionOption;
    }
}

