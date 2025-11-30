using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Assignment;

internal class StandardRoleDraft : IRoleDraftAllocator
{
    List<AssignmentCandidate>? IRoleDraftAllocator.Peek(byte playerId, int candidateNum)
    {
        throw new NotImplementedException();
    }

    void IRoleDraftAllocator.PopAs(AssignmentCandidate candidate)
    {
        throw new NotImplementedException();
    }

    void IRoleDraftAllocator.SetRandom(byte playerId)
    {
        throw new NotImplementedException();
    }
}
