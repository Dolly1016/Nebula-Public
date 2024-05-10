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

public class Secret : AbstractRole, DefinedAssignable
{
    static public ShownSecret OptionRole = new ShownSecret();
    static public Secret MyNiceRole = new(false);
    static public Secret MyEvilRole = new(true);

    public bool IsEvil { get; private set; }
    public override RoleCategory Category => IsEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole;

    public override string LocalizedName => "secretInGame";
    public override string DisplayName => "???";
    public override Color RoleColor => IsEvil ? Palette.ImpostorRed : Palette.CrewmateBlue;
    public override RoleTeam Team => IsEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { yield return IsEvil ? MyNiceRole : MyEvilRole; }

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);
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
                        player.Role.Unbox().OnSetTaskLocal(ref tasks, out int unacquired);
                        player.Tasks.Unbox().ResetTasksLocal(tasks);
                        player.Tasks.Unbox().GainExtraTasks(tasks.Count,false, unacquired,false);
                    }).InvokeSingle();
                    player.Tasks.Unbox().RpcSync();
                }
            }
        });

        new StaticAchievementToken("secret.common1");
    }

    private static void SetUpChallengeAchievement(GamePlayer player)
    {
        new AchievementToken<int>("secret.another1", 0, (_, _) => (NebulaGameManager.Instance?.EndState?.CheckWin(player.PlayerId) ?? false) && player.Role.Role != MyNiceRole && player.Role.Role != MyEvilRole);

        new AchievementToken<int>("secret.challenge", 0, (_, achievement) =>
        {
            return NebulaGameManager.Instance!.AllAchievementTokens.Any(r =>
            r.Achievement is AbstractAchievement a && a.Category.type == AchievementType.Challenge && a.Category.role != null && a.Id != "secret.challenge" && r.UniteTo(false) != AbstractAchievement.ClearState.NotCleared);
        });
    }
    public class NiceInstance : Crewmate.Crewmate.Instance
    {
        public override AbstractRole Role => MyNiceRole;

        public NiceInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        public override int[]? GetRoleArgument() => savedArgs;
        
        List<GameData.TaskInfo> savedTasks = new();
        int[] savedArgs;
        AbstractRole savedRole;

        public override void OnSetTaskLocal(ref List<GameData.TaskInfo> tasks, out int extraQuota)
        {
            int taskCount = OptionRole.NiceConditionOption;
            extraQuota = 0;
            while(tasks.Count > taskCount)
            {
                var index = System.Random.Shared.Next(taskCount);
                var task = tasks[index];
                tasks.RemoveAt(index);
                savedTasks.Add(task);
                extraQuota++;
            }
        }

        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            if (MyPlayer.Tasks.IsCompletedCurrentTasks)
                ScheduleSendArousalRpc(MyPlayer.Unbox(), savedArgs, savedTasks);
        }

        public override string? OverrideRoleName(string lastRoleName, bool isShort)
        {
            return (isShort ? "?" : "???").Color(Palette.CrewmateBlue);
        }

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text += $" ({savedRole.ShortName.Color(savedRole.RoleColor)})".Color(Color.gray);
        }

        public override void OnActivated()
        {
            base.OnActivated();
            if (AmOwner) SetUpChallengeAchievement(MyPlayer);
        }
    }

    public class EvilInstance : Impostor.Impostor.Instance, IBindPlayer
    {
        public override AbstractRole Role => MyEvilRole;
        public EvilInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        public override bool HasVanillaKillButton => OptionRole.EvilConditionTypeOption.CurrentValue == 0;
        public override int[]? GetRoleArgument() => savedArgs;

        int[] savedArgs;
        AbstractRole savedRole;
        int leftKill = OptionRole.EvilConditionOption;

        [OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            leftKill--;
            if (leftKill <= 0 && AmOwner) ScheduleSendArousalRpc(MyPlayer, savedArgs);
        }

        public override string? OverrideRoleName(string lastRoleName, bool isShort)
        {
            return (isShort ? "?" : "???").Color(Palette.ImpostorRed);
        }

        public override void DecorateRoleName(ref string text)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text += $" ({savedRole.ShortName.Color(savedRole.RoleColor)})".Color(Color.gray);
        }

        public override void OnActivated()
        {
            base.OnActivated();
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