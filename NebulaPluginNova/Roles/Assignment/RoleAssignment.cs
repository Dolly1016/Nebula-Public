using Hazel;
using Virial.Assignable;

namespace Nebula.Roles.Assignment;

internal class RoleTable : IRoleTable
{
    public List<(DefinedRole role, int[] arguments, byte playerId)> roles = new();
    public List<(DefinedModifier modifier, int[] arguments, byte playerId)> modifiers = new();

    public void SetRole(byte player, DefinedRole role, int[]? arguments = null)
    {
        roles.Add(new(role, arguments ?? Array.Empty<int>(), player));
    }

    public void SetModifier(byte player, DefinedModifier role, int[]? arguments = null)
    {
        modifiers.Add(new(role, arguments ?? Array.Empty<int>(), player));
    }

    public void Determine()
    {
        List<NebulaRPCInvoker> allInvokers = new();
        foreach (var role in roles) allInvokers.Add(PlayerModInfo.RpcSetAssignable.GetInvoker((role.playerId, role.role.Id, role.arguments, RoleType.Role)));
        foreach (var modifier in modifiers) allInvokers.Add(PlayerModInfo.RpcSetAssignable.GetInvoker((modifier.playerId, modifier.modifier.Id, modifier.arguments, RoleType.Modifier)));

        allInvokers.Add(NebulaGameManager.RpcStartGame.GetInvoker());

        CombinedRemoteProcess.CombinedRPC.Invoke(allInvokers.ToArray());
    }

    public IEnumerable<(byte playerId, DefinedRole role)> GetPlayers(RoleCategory category)
    {
        foreach (var tuple in roles) if ((tuple.role.Category & category) != 0) yield return (tuple.playerId, tuple.role);
    }
}

public class FreePlayRoleAllocator : IRoleAllocator
{
    public void Assign(List<byte> impostors, List<byte> others)
    {
        RoleTable table = new();

        foreach(var p in impostors.Concat(others))
        {
            table.SetRole(p, Crewmate.Crewmate.MyRole);
        }

        table.Determine();
    }
}

public class StandardRoleAllocator : IRoleAllocator
{

    private record GhostRoleChance(DefinedGhostRole role) { public int count = role.AllocationParameters?.RoleCount ?? 0; public int left = role.AllocationParameters?.RoleCount ?? 0; }
    private record RoleChance(DefinedRole role) { public int count = role.AllocationParameters?.RoleCount ?? 0; public int left = role.AllocationParameters?.RoleCount ?? 0; public int cost = 1; public int otherCost = 0; }

    private List<GhostRoleChance> ghostRolePool;

    public StandardRoleAllocator()
    {
        ghostRolePool = new(Roles.AllGhostRoles.Where(r => (r.AllocationParameters?.RoleCount ?? 0) > 0).Select(r => new GhostRoleChance(r)));
    }

    private void OnSetRole(DefinedRole role,params List<RoleChance>[] pool)
    {
        foreach(var remove in GeneralConfigurations.exclusiveAssignmentOptions.Select(e => e.OnAssigned(role))) foreach(var removeRole in remove) foreach (var p in pool) p.RemoveAll(r => r.role == removeRole);
    }


    private void CategoryAssign(RoleTable table, int left,List<byte> main, List<byte> others, List<RoleChance> rolePool, params List<RoleChance>[] allRolePool)
    {
        if (left < 0) left = 15;

        void OnSelected(RoleChance selected)
        {
            table.SetRole(main[0], selected.role);
            main.RemoveAt(0);
            left -= selected.cost;

            //割り当て済み役職を排除
            selected.left--;
            if (selected.left == 0) rolePool.Remove(selected);

            //排他的割り当てを考慮
            OnSetRole(selected.role, allRolePool);
        }

        bool left100Roles = true;
        while (main.Count > 0 && left > 0 && rolePool.Count > 0)
        {
            //コスト超過によって割り当てられない役職を弾く
            rolePool.RemoveAll(c =>
            {
                if (main == others)
                    return c.cost + c.otherCost > left && c.cost + c.otherCost > main.Count;
                else
                    return c.cost > left && c.cost > main.Count && c.otherCost > others.Count;
            });

            //100%割り当て役職が残っている場合
            if (left100Roles)
            {
                var roles100 = rolePool.Where(r => r.role.AllocationParameters!.GetRoleChance(r.count - r.left) == 100f);
                if (roles100.Any(r => true))
                {
                    //役職を選択する
                    OnSelected(roles100.ToArray().Random());
                    continue;
                }
                else
                {
                    left100Roles = false;
                }
            }

            //100%役職がもう残っていない場合
            var sum = rolePool.Sum(r => r.role.AllocationParameters!.GetRoleChance(r.count - r.left));
            var random = System.Random.Shared.NextSingle() * sum;
            foreach(var r in rolePool)
            {
                random -= r.role.AllocationParameters!.GetRoleChance(r.count - r.left);
                if(random < 0f)
                {
                    //役職を選択する
                    OnSelected(r);
                    break;
                }
            }
        }
    }

    public void Assign(List<byte> impostors, List<byte> others)
    {
        RoleTable table = new();

        //ロールプールを作る
        List<RoleChance> GetRolePool(RoleCategory category) => new(Roles.AllRoles.Where(r => r.Category == category && (r.AllocationParameters?.RoleCount ?? 0) > 0).Select(r =>
        new RoleChance(r) { cost = 1,otherCost = 0 }));

        List<RoleChance> crewmateRoles = GetRolePool(RoleCategory.CrewmateRole);
        List<RoleChance> impostorRoles = GetRolePool(RoleCategory.ImpostorRole);
        List<RoleChance> neutralRoles = GetRolePool(RoleCategory.NeutralRole);
        List<RoleChance>[] allRoles = [crewmateRoles, impostorRoles, neutralRoles];

        CategoryAssign(table, GeneralConfigurations.AssignmentImpostorOption, impostors, others, impostorRoles, allRoles);
        CategoryAssign(table, GeneralConfigurations.AssignmentNeutralOption, others, others, neutralRoles, allRoles);
        CategoryAssign(table, GeneralConfigurations.AssignmentCrewmateOption, others, others, crewmateRoles, allRoles);

        foreach (var p in impostors) table.SetRole(p, Impostor.Impostor.MyRole);
        foreach (var p in others) table.SetRole(p, Crewmate.Crewmate.MyRole);

        foreach (var m in Roles.AllAllocatableModifiers().OrderBy(im => im.AssignPriority)) m.TryAssign(table);

        table.Determine();
    }

    public DefinedGhostRole? AssignToGhost(GamePlayer player)
    {
        var pool = ghostRolePool.Where(g => g.role.Category == player.Role.Role.Category && player.Role.Role.CanLoad(g.role)).ToArray();

        //まずは100%割り当て役職を抽出
        var cand = pool.Where(g => g.left > 0 && g.role.AllocationParameters!.GetRoleChance(g.count - g.left) == 100f).ToArray();
        //100%割り当て役職がいなければ
        if (cand.Length == 0)
        {
            //Normal
            if(GeneralConfigurations.GhostAssignmentOption.GetValue() == 0) {
                float sum = pool.Sum(g => g.role.AllocationParameters!.GetRoleChance(g.count - g.left));
                foreach(var p in pool)
                {
                    sum -= p.role.AllocationParameters!.GetRoleChance(p.count - p.left);
                    if(sum < 0f)
                    {
                        p.left--;
                        if (p.left == 0) ghostRolePool.Remove(p);
                        return p.role;
                    }
                }

                return null;
            }

            //Thrilling
            cand = pool.Where(g => System.Random.Shared.NextSingle() * 100f < g.role.AllocationParameters!.GetRoleChance(g.count - g.left)).ToArray();
        }

        if (cand.Length > 0)
        {
            var selected = cand.Random();
            selected.left--;
            if (selected.left == 0) ghostRolePool.Remove(selected);
            return selected.role;
        }

        return null;
    }

}