using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

public interface IRoleAllocator
{
    public abstract void Assign(List<byte> impostors, List<byte> others);
    public virtual DefinedGhostRole? AssignToGhost(Virial.Game.Player player) => null;
}

public interface IRoleTable
{
    IEnumerable<(byte playerId, DefinedRole role)> GetPlayers(RoleCategory category);
    void SetRole(byte player, DefinedRole role, int[]? arguments = null);
    void SetModifier(byte player, DefinedModifier role, int[]? arguments = null);
    void Determine();
}
