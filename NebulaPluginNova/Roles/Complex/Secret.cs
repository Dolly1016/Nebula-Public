using Nebula.Modules;
using Nebula.Roles.Assignment;
using Nebula.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Complex;

public class ShownSecret : ConfigurableStandardModifier
{
    public override int AssignPriority => 100;

    public NebulaConfiguration EvilConditionOption = null!;
    public NebulaConfiguration NiceConditionOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        NeutralRoleCountOption.Predicate = () => false;
        EvilConditionOption = new NebulaConfiguration(RoleConfig, "killingForArousal", null, 1, 10, 2, 2);
        NiceConditionOption = new NebulaConfiguration(RoleConfig, "tasksForArousal", null, 1, 10, 3, 3);
    }

    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments)
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

public class Secret : AbstractRole
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

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => IsEvil ? new EvilInstance(player,arguments) : new NiceInstance(player,arguments);

    
    public override int RoleCount => 0;
    public override float GetRoleChance(int count) => 0f;

    public override bool CanBeGuessDefault => false;

    public Secret(bool isEvil)
    {
        IsEvil = isEvil;
    }

    public override void Load(){}

    //Localで呼び出すこと(タスク置き換えの都合上)
    private static void ScheduleSendArousalRpc(PlayerModInfo player, int[] savedArgs,List<GameData.TaskInfo>? tasks = null)
    {
        NebulaManager.Instance.ScheduleDelayAction(() =>
        {
            using (RPCRouter.CreateSection("ArousalSecretRole"))
            {
                player.RpcInvokerSetRole(Roles.AllRoles.First(r => r.Id == savedArgs[0]), savedArgs.Skip(2).ToArray()).InvokeSingle();
                if ((savedArgs[1] & 0b1) != 0) player.RpcInvokerSetModifier(GuesserModifier.MyRole, null).InvokeSingle();

                if (tasks != null)
                {
                    new NebulaRPCInvoker(() =>
                    {
                        player.Role.OnSetTaskLocal(ref tasks, out int unacquired);
                        player.Tasks.ResetTasksLocal(tasks);
                        player.Tasks.GainExtraTasks(tasks.Count,false, unacquired,false);
                    }).InvokeSingle();
                    player.Tasks.RpcSync();
                }
            }
        });

        new StaticAchievementToken("secret.common1");
    }

    private static void SetUpChallengeAchievement(PlayerModInfo player)
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

        public NiceInstance(PlayerModInfo player, int[] savedArgs) : base(player)
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

        public override void OnTaskCompleteLocal()
        {
            if (MyPlayer.Tasks.IsCompletedCurrentTasks)
            {
                ScheduleSendArousalRpc(MyPlayer, savedArgs, savedTasks);
            }
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

    public class EvilInstance : Impostor.Impostor.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyEvilRole;
        public EvilInstance(PlayerModInfo player, int[] savedArgs) : base(player)
        {
            this.savedArgs = savedArgs;
            this.savedRole = Roles.AllRoles.First(r => r.Id == savedArgs[0]);
        }

        public override int[]? GetRoleArgument() => savedArgs;

        int[] savedArgs;
        AbstractRole savedRole;
        int leftKill = OptionRole.EvilConditionOption;
        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
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

    }
}