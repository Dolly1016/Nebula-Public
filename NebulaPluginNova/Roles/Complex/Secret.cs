using Nebula.Roles.Assignment;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Complex;

public class ShownSecret : ConfigurableStandardModifier
{
    public override int AssignPriority => 100;

    public NebulaConfiguration EvilConditionTypeOption = null!;
    public NebulaConfiguration EvilConditionOption = null!;
    public NebulaConfiguration NiceConditionOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        NeutralRoleCountOption.Predicate = () => false;
        EvilConditionTypeOption = new NebulaConfiguration(RoleConfig, "impostorArousalMethod", null, ["options.role.secret.impostorArousalMethod.kill", "options.role.secret.impostorArousalMethod.death"], 0, 0);
        EvilConditionOption = new NebulaConfiguration(RoleConfig, "killingForArousal", null, 1, 10, 2, 2) { Predicate = () => EvilConditionTypeOption.CurrentValue == 0 };
        NiceConditionOption = new NebulaConfiguration(RoleConfig, "tasksForArousal", null, 1, 10, 3, 3);
    }

    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments)
    {
        throw new NotImplementedException();
    }

    public override string LocalizedName => "secret";

    public override Color RoleColor => Color.white.RGBMultiplied(0.82f);
    public override string CodeName => "SCR";
    override protected void AssignToTable(IRoleAllocator.RoleTable roleTable, byte playerId)
    {
        var tuple = roleTable.roles.Find(r => r.playerId == playerId);
        roleTable.roles.RemoveAll(r => r.playerId == playerId);
        var exArg = 0;
        if (roleTable.modifiers.Any(m => m.modifier == GuesserModifier.MyRole && m.playerId == playerId))
        {
            exArg |= 0b1;
            roleTable.modifiers.RemoveAll(m => m.modifier == GuesserModifier.MyRole && m.playerId == playerId);
        }
        var args = tuple.arguments.Prepend(exArg).Prepend(tuple.role.Id).ToArray();
        roleTable.SetRole(playerId, tuple.role.Category == RoleCategory.ImpostorRole ? Secret.MyEvilRole : Secret.MyNiceRole, args);
    }
}

public class Secret : AbstractRole, DefinedRole
{
    static public ShownSecret OptionRole = new ShownSecret();
    static public Secret MyNiceRole = new(false);
    static public Secret MyEvilRole = new(true);

    public bool IsEvil { get; private set; }
    public override RoleCategory Category => IsEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole;

    string DefinedAssignable.LocalizedName => "secretInGame";
    string DefinedAssignable.DisplayName => "???";
    public override Color RoleColor => IsEvil ? Palette.ImpostorRed : Palette.CrewmateBlue;
    public override RoleTeam Team => IsEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.Team;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return IsEvil ? MyNiceRole : MyEvilRole; }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);
    bool DefinedAssignable.ShowOnHelpScreen => false;

    public override int RoleCount => 0;
    public override float GetRoleChance(int count) => 0f;

    public override bool CanBeGuessDefault => false;

    public Secret(bool isEvil)
    {
        IsEvil = isEvil;
    }

    public override void Load(){}

    //クルーメイトの場合はLocalで呼び出すこと(タスク置き換えの都合上)
    private static void ScheduleSendArousalRpc(GamePlayer player, int[] savedArgs,List<GameData.TaskInfo>? tasks = null)
    {
        NebulaManager.Instance.ScheduleDelayAction(() =>
        {
            using (RPCRouter.CreateSection("ArousalSecretRole"))
            {
                player.Unbox().RpcInvokerSetRole(Roles.AllRoles.First(r => r.Id == savedArgs[0]), savedArgs.Skip(2).ToArray()).InvokeSingle();
                if ((savedArgs[1] & 0b1) != 0) player.Unbox().RpcInvokerSetModifier(GuesserModifier.MyRole, null).InvokeSingle();

                if (tasks != null)
                {
                    new NebulaRPCInvoker(() =>
                    {
                        var result = GameOperatorManager.Instance!.Run(new PlayerTasksTrySetLocalEvent(player, tasks.ToArray()));
                        player.Tasks.Unbox().ResetTasksLocal(result.VanillaTasks.ToList());
                        player.Tasks.Unbox().GainExtraTasks(result.Tasks.Count, false, result.ExtraQuota, false);
                    }).InvokeSingle();
                    player.Tasks.Unbox().RpcSync();
                }
            }
        });

        new StaticAchievementToken("secret.common1");
    }

    private static void SetUpChallengeAchievement(GamePlayer player)
    {
        new AchievementToken<int>("secret.another1", 0, (_, _) => (NebulaGameManager.Instance?.EndState?.Winners.Test(player) ?? false) && player.Role.Role != MyNiceRole && player.Role.Role != MyEvilRole);

        new AchievementToken<int>("secret.challenge", 0, (_, achievement) =>
        {
            return NebulaGameManager.Instance!.AllAchievementTokens.Any(r =>
            r.Achievement is AbstractAchievement a && a.Category.type == AchievementType.Challenge && a.Category.role != null && a.Id != "secret.challenge" && r.UniteTo(false) != AbstractAchievement.ClearState.NotCleared);
        });
    }
    public class NiceInstance : Crewmate.Crewmate.Instance, RuntimeRole
    {
        public override AbstractRole Role => MyNiceRole;
        public override string DisplayRoleName => base.DisplayRoleName;

        public NiceInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        int[]? RuntimeAssignable.RoleArguments => savedArgs;
        
        List<GameData.TaskInfo> savedTasks = new();
        int[] savedArgs;
        AbstractRole savedRole;

        void OnSetTaskLocal(PlayerTasksTrySetLocalEvent ev)
        {
            int taskCount = OptionRole.NiceConditionOption;
            while(ev.Tasks.Count > taskCount)
            {
                var index = System.Random.Shared.Next(taskCount);
                var task = ev.Tasks[index];
                ev.Tasks.RemoveAt(index);
                savedTasks.Add(task.MyTask);
                ev.AddExtraQuota(1);
            }
        }

        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            if (MyPlayer.Tasks.IsCompletedCurrentTasks)
                ScheduleSendArousalRpc(MyPlayer.Unbox(), savedArgs, savedTasks);
        }

        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort)
        {
            return (isShort ? "?" : "???").Color(Palette.CrewmateBlue);
        }

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text += $" ({savedRole.ShortName.Color(savedRole.RoleColor)})".Color(Color.gray);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) SetUpChallengeAchievement(MyPlayer);
        }
    }

    public class EvilInstance : Impostor.Impostor.Instance, RuntimeRole
    {
        public override AbstractRole Role => MyEvilRole;
        public EvilInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        public override bool HasVanillaKillButton => OptionRole.EvilConditionTypeOption.CurrentValue == 0;
        int[]? RuntimeAssignable.RoleArguments => savedArgs;

        int[] savedArgs;
        AbstractRole savedRole;
        int leftKill = OptionRole.EvilConditionOption;

        [OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            leftKill--;
            if (leftKill <= 0 && AmOwner) ScheduleSendArousalRpc(MyPlayer, savedArgs);
        }

        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort) => (isShort ? "?" : "???").Color(Palette.ImpostorRed);
        

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text += $" ({savedRole.ShortName.Color(savedRole.RoleColor)})".Color(Color.gray);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) SetUpChallengeAchievement(MyPlayer);
        }

        void OnPlayerDead(PlayerDieEvent ev)
        {
            //Covertモードはホストが割り当てを管理する
            if (AmongUsClient.Instance.AmHost && OptionRole.EvilConditionTypeOption.CurrentValue == 1)
            {
                //死者、非インポスター、シークレットしかいないとき
                if (NebulaGameManager.Instance?.AllPlayerInfo().All(p => p.IsDead || p.Role.Role.Category != RoleCategory.ImpostorRole || p.Role.Role == MyEvilRole) ?? false)
                {
                    var selected = NebulaGameManager.Instance?.AllPlayerInfo().Where(p => p.Role.Role == MyEvilRole).ToArray().Random();
                    if(selected != null && selected.Role is EvilInstance ei) ScheduleSendArousalRpc(selected, ei.savedArgs);
                }
            }
        }

    }
}