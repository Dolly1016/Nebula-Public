using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Assignment;

internal class StandardRoleDraft : IRoleDraftAllocator
{
    private int TotalImpostors, TotalNeutrals, TotalMaxCrewmates, TotalMinCrewmates;
    private int Total100Impostors, TotalRandomImpostors;
    private int Total100Neutrals, TotalRandomNeutrals;
    private int TotalMax100Crewmates, TotalMin100Crewmates; //割り当てが進むにつれ変動する。
    private int TotalMaxRandomCrewmates, TotalMinRandomCrewmates; //割り当てが進むにつれ変動する。


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
