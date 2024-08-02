using Nebula.Roles.Assignment;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Complex;

public class ShownSecret : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private ShownSecret() : base("secret", "SCR", new(Color.white.RGBMultiplied(0.82f)), [EvilConditionTypeOption, EvilConditionOption, NiceConditionOption], allocateToNeutral: false) { 
    }

    //割り当てる役職が変更されてしまうので、一番最後に割り当てる
    int HasAssignmentRoutine.AssignPriority => 100;

    static internal ValueConfiguration<int> EvilConditionTypeOption = NebulaAPI.Configurations.Configuration("options.role.secret.impostorArousalMethod", ["options.role.secret.impostorArousalMethod.kill", "options.role.secret.impostorArousalMethod.death"], 0);
    static internal IntegerConfiguration EvilConditionOption = NebulaAPI.Configurations.Configuration("options.role.secret.killingForArousal", (1, 10), 2, () => EvilConditionTypeOption.GetValue() == 0);
    static internal IntegerConfiguration NiceConditionOption = NebulaAPI.Configurations.Configuration("options.role.secret.tasksForArousal", (1, 10), 3);

    static public ShownSecret OptionRole = new ShownSecret();


    // このモディファイアは実際に割り当てられることはない
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => throw new NotImplementedException();

    override protected void SetModifier(IRoleTable roleTable, byte playerId)
    {
        var rawTable = roleTable as RoleTable;
        if (rawTable == null) return;
        var tuple = rawTable.roles.Find(r => r.playerId == playerId);
        rawTable.roles.RemoveAll(r => r.playerId == playerId);
        var exArg = 0;
        if (rawTable.modifiers.Any(m => m.modifier == GuesserModifier.MyRole && m.playerId == playerId))
        {
            exArg |= 0b1;
            rawTable.modifiers.RemoveAll(m => m.modifier == GuesserModifier.MyRole && m.playerId == playerId);
        }
        var args = tuple.arguments.Prepend(exArg).Prepend(tuple.role.Id).ToArray();
        roleTable.SetRole(playerId, tuple.role.Category == RoleCategory.ImpostorRole ? Secret.MyEvilRole : Secret.MyNiceRole, args);
    }
}

public class Secret : DefinedRoleTemplate, DefinedRole
{
    static public Secret MyNiceRole = new(false);
    static public Secret MyEvilRole = new(true);

    public bool IsEvil { get; private set; }
    string DefinedAssignable.InternalName => IsEvil ? "evilSecret" : "niceSecret";
    string DefinedAssignable.DisplayName => "???";

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);
    bool DefinedAssignable.ShowOnHelpScreen => false;
    bool IGuessed.CanBeGuessDefault => false;
    bool AssignableFilterHolder.CanLoadDefault(Virial.Assignable.DefinedAssignable assignable) => false;

    public Secret(bool isEvil) : base("secretInGame", 
        new(isEvil ? Palette.ImpostorRed : Palette.CrewmateBlue), 
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam, withAssignmentOption: false, withOptionHolder: false)
    {
        IsEvil = isEvil;
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!]);
    }


    //クルーメイトの場合はLocalで呼び出すこと(タスク置き換えの都合上)
    private static void ScheduleSendArousalRpc(GamePlayer player, int[] savedArgs,List<NetworkedPlayerInfo.TaskInfo>? tasks = null)
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
    public class NiceInstance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyNiceRole;

        public NiceInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        int[]? RuntimeAssignable.RoleArguments => savedArgs;
        
        List<NetworkedPlayerInfo.TaskInfo> savedTasks = new();
        int[] savedArgs;
        DefinedRole savedRole;

        [OnlyMyPlayer]
        void OnSetTaskLocal(PlayerTasksTrySetLocalEvent ev)
        {
            int taskCount = ShownSecret.NiceConditionOption;
            while(ev.Tasks.Count > taskCount)
            {
                var index = System.Random.Shared.Next(taskCount);
                var task = ev.Tasks[index];
                ev.Tasks.RemoveAt(index);
                savedTasks.Add(task.MyTask);
                ev.AddExtraQuota(1);
            }
        }

        [OnlyMyPlayer]
        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            if (MyPlayer.Tasks.IsCompletedCurrentTasks)
                ScheduleSendArousalRpc(MyPlayer.Unbox(), savedArgs, savedTasks);
        }

        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort)
        {
            string str = (isShort ? "?" : "???").Color(Palette.CrewmateBlue);
            if(NebulaGameManager.Instance?.CanSeeAllInfo ?? false) str += $" ({savedRole.DisplayShort.Color(savedRole.UnityColor)})".Color(Color.gray);
            return str;
        }


        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) SetUpChallengeAchievement(MyPlayer);
        }
    }

    public class EvilInstance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyEvilRole;
        public EvilInstance(GamePlayer player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        bool RuntimeRole.HasVanillaKillButton => ShownSecret.EvilConditionTypeOption.GetValue() == 0;
        int[]? RuntimeAssignable.RoleArguments => savedArgs;

        int[] savedArgs;
        DefinedRole savedRole;
        int leftKill = ShownSecret.EvilConditionOption;

        [OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            leftKill--;
            if (leftKill <= 0 && AmOwner) ScheduleSendArousalRpc(MyPlayer, savedArgs);
        }
        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort)
        {
            string str = (isShort ? "?" : "???").Color(Palette.ImpostorRed);
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) str += $" ({savedRole.DisplayShort.Color(savedRole.UnityColor)})".Color(Color.gray);
            return str;
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) SetUpChallengeAchievement(MyPlayer);
        }

        void OnPlayerDead(PlayerDieEvent ev)
        {
            //Covertモードはホストが割り当てを管理する
            if (AmongUsClient.Instance.AmHost && ShownSecret.EvilConditionTypeOption.GetValue() == 1)
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