using Hazel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming;

public class VPEnvironment
{
    private readonly Dictionary<int, IVPNumeric> variableMap = [];
    private ICircuit circuit;
    private VPEnvironment? parent = null;

    public VPEnvironment(VPEnvironment? parent, ICircuit circuit)
    {
        this.parent = parent;
        this.circuit = circuit;
    }

    public bool TryGetValue(int id, [MaybeNullWhen(false)]out IVPNumeric result)
    {
        VPEnvironment? env = this;
        while(env != null)
        {
            if (env.variableMap.TryGetValue(id, out result)) return true;
            env = env.parent;
        }
        result = null;
        return false;
    }

    public void PushContextIndependentValue(int id, IVPNumeric value)
    {
        variableMap[id] = value;
    }

    public INode GetNode(int id) => circuit.GetNode(id);
}
