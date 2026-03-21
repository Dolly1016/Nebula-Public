using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Runtime;

namespace Nebula.Roles.Assignment;


internal class StandardRoleDraft : IRoleDraftAllocator
{
    private record CostOccupation(int Cost, byte PlayerId);
    private class Cost
    {
        public Cost(int max)
        {
            this.Max = max;
        }

        public void Push(CostOccupation occupation) => occupations.Add(occupation);
        public void RemoveBy(byte playerId) => occupations.RemoveAll(o => o.PlayerId == playerId);
        public int Max { get; private init; }
        private List<CostOccupation> occupations = [];
    }

    private class RolePoolElement
    {
        public DefinedRole Role { get; private init; }
        private AllocationParameters? param = null;

        public RolePoolElement(DefinedRole role, AllocationParameters? param = null)
        {
            this.Role = role;
            this.param = param ?? role.AllocationParameters;
            this.MaxCount = this.param?.RoleCountSum ?? 0;
            this.CurrentCount = new Cost(this.MaxCount);

            this.TeamCost  = this.param?.TeamCost ?? 0;
            this.OtherCost = this.param?.OtherCost ?? 0;

            this.TeamPlayers = (this.param?.TeamAssignment.Length ?? 0) + 1;
            this.OtherPlayers = (this.param?.OthersAssignment.Length ?? 0) + 0;
        }

        public int MaxCount { get; private init; }
        public Cost CurrentCount { get; private init; }
        public int TeamCost { get; }
        public int OtherCost { get; }
        public int TeamPlayers { get; }
        public int OtherPlayers { get; }
    }

    private record AssignmentConstants(
        //バニラ設定から読み取れる人数
        int Impostors,
        //配役設定から読み取れる、Mod役職の割り当て数
        int MaxModdedImpostors,
        int MaxModdedCrewmates,
        int MaxNeutrals,
        //プレイヤー数
        int Players,
        byte[] PlayerIds
        );
    
    public StandardRoleDraft() { }

    private AssignmentConstants Constants { get; set; }
    
    /*
    private void CreateRolePool()
    {
        void CreateRolePool(out List<RoleChance> impostorRoles, out List<RoleChance> crewmateRoles, out List<RoleChance> neutralRoles, out List<RoleChance>[] customChances, out List<RoleChance>[] allRoles)
        {
            List<RoleChance> GetRolePool(RoleCategory category) => new(Roles.AllRoles.Where(r => r.Category == category && (r.AllocationParameters?.RoleCountSum ?? 0) > 0).Select(r => new RoleChance(r) { cost = 1, otherCost = 0 }));
            customChances = AssignmentType.AllTypes.Select(t => new List<RoleChance>(Roles.AllRoles.Where(r => t.Predicate.Invoke(r.AssignmentStatus, r) && (r.GetCustomAllocationParameters(t)?.RoleCountSum ?? 0) > 0).Select(r => new RoleChance(r, r.GetCustomAllocationParameters(t)) { cost = 1, otherCost = 0 }))).ToArray();

            crewmateRoles = GetRolePool(RoleCategory.CrewmateRole);
            impostorRoles = GetRolePool(RoleCategory.ImpostorRole);
            neutralRoles = GetRolePool(RoleCategory.NeutralRole);
            allRoles = [crewmateRoles, impostorRoles, neutralRoles, .. customChances];

            //はじめに排他的割り当ての抽選を行う
            foreach (var exclusiveOption in IExclusiveAssignmentRule.AllRules)
            {
                List<DefinedRole> cand = [];
                foreach (var pool in allRoles)
                {
                    foreach (var chance in pool)
                    {
                        if (exclusiveOption.Contains(chance.role) && !cand.Contains(chance.role)) cand.Add(chance.role);
                    }
                }
                if (cand.Count <= 1) continue;

                cand.RemoveRandomOne();
                foreach (var pool in allRoles) pool.RemoveAll(chance => cand.Contains(chance.role));
            }
        }
    }
    */

    

    private void SetUp(byte[] players)
    {
        Constants = new(
            AmongUsUtil.AdjustedImpostors(players.Length),
            GeneralConfigurations.AssignmentImpostorOption,
            GeneralConfigurations.AssignmentCrewmateOption,
            GeneralConfigurations.AssignmentNeutralOption,
            players.Length,
            players
            );
    }


}
