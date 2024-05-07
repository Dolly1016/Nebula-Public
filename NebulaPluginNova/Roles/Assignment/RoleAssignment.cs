using Hazel;
using Virial.Assignable;

namespace Nebula.Roles.Assignment;

public abstract class IRoleAllocator
{
    public class RoleTable
    {
        public List<(AbstractRole role, int[] arguments, byte playerId)> roles = new();
        public List<(AbstractModifier modifier, int[] arguments, byte playerId)> modifiers = new();

        public void SetRole(PlayerControl player, AbstractRole role, int[]? arguments = null)
        {
            roles.Add(new(role, arguments ?? Array.Empty<int>(), player.PlayerId));
        }

        public void SetModifier(PlayerControl player, AbstractModifier role, int[]? arguments = null)
        {
            modifiers.Add(new(role, arguments ?? Array.Empty<int>(), player.PlayerId));
        }

        public void SetRole(byte player, AbstractRole role, int[]? arguments = null)
        {
            roles.Add(new(role, arguments ?? Array.Empty<int>(), player));
        }

        public void SetModifier(byte player, AbstractModifier role, int[]? arguments = null)
        {
            modifiers.Add(new(role, arguments ?? Array.Empty<int>(), player));
        }

        public void Determine()
        {
            List<NebulaRPCInvoker> allInvokers = new();
            foreach (var role in roles) allInvokers.Add(PlayerModInfo.RpcSetAssignable.GetInvoker((role.playerId, role.role.Id, role.arguments, RoleType.Role )));
            foreach (var modifier in modifiers) allInvokers.Add(PlayerModInfo.RpcSetAssignable.GetInvoker((modifier.playerId, modifier.modifier.Id, modifier.arguments, RoleType.Modifier)));

            allInvokers.Add(NebulaGameManager.RpcStartGame.GetInvoker());

            CombinedRemoteProcess.CombinedRPC.Invoke(allInvokers.ToArray());


            foreach (var sendTo in NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.WithNoS))
            {
                //MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    
                    var messageWriter = AmongUsClient.Instance.StartRpcImmediately(p.MyControl.NetId, 44, SendOption.Reliable, (int)sendTo.MyControl.OwnerId);
                    messageWriter.Write((ushort)AmongUs.GameOptions.RoleTypes.Crewmate);

                    AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

                    Debug.Log("Send Vanilla RPC");
                    //messageWriter.EndMessage();
                    //messageWriter.EndMessage();
                }
                //AmongUsClient.Instance.SendOrDisconnect(messageWriter);
                //messageWriter.Recycle();
            }

        }

        public IEnumerable<(byte playerId,AbstractRole role)> GetPlayers(RoleCategory category)
        {
            foreach (var tuple in roles) if ((tuple.role.Category & category) != 0) yield return (tuple.playerId, tuple.role);
        }
    }

    public abstract void Assign(List<PlayerControl> impostors, List<PlayerControl> others);
    public virtual AbstractGhostRole? AssignToGhost(GamePlayer player) => null;
}

public class FreePlayRoleAllocator : IRoleAllocator
{
    public override void Assign(List<PlayerControl> impostors, List<PlayerControl> others)
    {
        RoleTable table = new();

        foreach (var p in impostors) table.SetRole(p,Crewmate.Crewmate.MyRole);
        foreach (var p in others) table.SetRole(p, Crewmate.Crewmate.MyRole);

        foreach (var p in PlayerControl.AllPlayerControls) table.SetModifier(p, Modifier.MetaRole.MyRole);

        table.Determine();
    }
}

public class StandardRoleAllocator : IRoleAllocator
{

    private record GhostRoleChance(AbstractGhostRole role) { public int count; public int left; }
    private record RoleChance(AbstractRole role) { public int count; public int left; public int cost; public int otherCost; }

    private List<GhostRoleChance> ghostRolePool;

    public StandardRoleAllocator()
    {
        ghostRolePool = new(Roles.AllGhostRoles.Where(r => r.RoleCount > 0).Select<AbstractGhostRole, GhostRoleChance>(r => new(r) { count = r.RoleCount, left = r.RoleCount }));
    }

    private void OnSetRole(AbstractRole role,params List<RoleChance>[] pool)
    {
        foreach (var remove in GeneralConfigurations.ExclusiveOptionBody.OnAssigned(role)) foreach(var p in pool) p.RemoveAll(r => r.role == remove);
    }


    private void CategoryAssign(RoleTable table, int left,List<PlayerControl> main, List<PlayerControl> others, List<RoleChance> rolePool, params List<RoleChance>[] allRolePool)
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
                var roles100 = rolePool.Where(r => r.role.GetRoleChance(r.count - r.left) == 100f);
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

            var sum = rolePool.Sum(r => r.role.GetRoleChance(r.count - r.left));
            var random = System.Random.Shared.NextSingle() * sum;
            foreach(var r in rolePool)
            {
                random -= r.role.GetRoleChance(r.count - r.left);
                if(random < 0f)
                {
                    //役職を選択する
                    OnSelected(r);
                    break;
                }
            }
        }
    }

    public override void Assign(List<PlayerControl> impostors, List<PlayerControl> others)
    {
        RoleTable table = new();

        //ロールプールを作る
        List<RoleChance> GetRolePool(RoleCategory category) => new(Roles.AllRoles.Where(r => r.Category == category && r.RoleCount > 0).Select(r =>
        new RoleChance(r) { count = r.RoleCount, left = r.RoleCount,
            cost = 1 + (r.AdditionalRolesConsumeRolePool ? (r.AdditionalRole?.Length ?? 0) : 0),
            otherCost = r.AdditionalRolesConsumeRolePool ? 0 : (r.AdditionalRole?.Length ?? 0)
        }));

        List<RoleChance> crewmateRoles = GetRolePool(RoleCategory.CrewmateRole);
        List<RoleChance> impostorRoles = GetRolePool(RoleCategory.ImpostorRole);
        List<RoleChance> neutralRoles = GetRolePool(RoleCategory.NeutralRole);
        List<RoleChance>[] allRoles = [crewmateRoles, impostorRoles, neutralRoles];

        CategoryAssign(table, GeneralConfigurations.AssignmentImpostorOption, impostors, others, impostorRoles, allRoles);
        CategoryAssign(table, GeneralConfigurations.AssignmentNeutralOption, others, others, neutralRoles, allRoles);
        CategoryAssign(table, GeneralConfigurations.AssignmentCrewmateOption, others, others, crewmateRoles, allRoles);

        foreach (var p in impostors) table.SetRole(p, Impostor.Impostor.MyRole);
        foreach (var p in others) table.SetRole(p, Crewmate.Crewmate.MyRole);

        foreach (var m in Roles.AllIntroAssignableModifiers().OrderBy(im => im.AssignPriority)) m.Assign(table);

        table.Determine();
    }

    public override AbstractGhostRole? AssignToGhost(GamePlayer player)
    {
        var pool = ghostRolePool.Where(g => g.role.Category == player.Role.Role.Category).ToArray();

        //まずは100%割り当て役職を抽出
        var cand = pool.Where(g => g.role.GetRoleChance(g.count - g.left) == 100f).ToArray();
        //100%割り当て役職がいなければ
        if (cand.Length == 0)
        {
            //Normal
            if(GeneralConfigurations.GhostAssignmentOption.CurrentValue == 0) {
                float sum = pool.Sum(g => g.role.GetRoleChance(g.count - g.left));
                foreach(var p in pool)
                {
                    sum -= p.role.GetRoleChance(p.count - p.left);
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
            cand = pool.Where(g => System.Random.Shared.NextSingle() * 100f < g.role.GetRoleChance(g.count - g.left)).ToArray();
        }

        if (cand.Length > 0)
        {
            var selected = cand.Random();
            selected.count--;
            if (selected.left == 0) ghostRolePool.Remove(selected);
            return selected.role;
        }

        return null;
    }

}