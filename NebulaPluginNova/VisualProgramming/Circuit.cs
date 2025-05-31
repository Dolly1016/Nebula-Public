using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.VisualProgramming;

namespace Nebula.VisualProgramming;

internal class Circuit : ICircuit
{
    private readonly Dictionary<int, IVPCalculable> registryMap = [];
    private readonly List<INode> nodes = [];
    private readonly int[] inputs;
    private readonly int[] outputs;

    int ICircuit.InputLength => inputs.Length;

    int ICircuit.OutputLength => outputs.Length;

    VPEnvironment ICircuit.GenerateInstance(IVPNumeric[] input)
    {
        var env = new VPEnvironment(null, this);
        for(int i = 0;i<input.Length;i++) env.PushContextIndependentValue(this.inputs[i], input[i]);
        return env;
    }

    IVPCalculable ICircuit.GetOutput(int index)
    {
        return registryMap[outputs[index]];
    }

    public void AddNode(INode node)
    {
        nodes.Add(node);
        for (int i = 0; i < node.OutputLength; i++)
        {
            var output = node.GetOutput(i);
            if(output.Id != -1) registryMap[output.Id] = output;
        }
    }

    public INode GetNode(int nodeId) => nodes[nodeId];
    public void ConnectAll(IReadOnlyList<SerializableNode> serializedNodes)
    {
        for(int n = 0; n < serializedNodes.Count; n++)
        {
            for (int i = 0; i < nodes[n].InputLength; i++)
            {
                var inputData = serializedNodes[n].Inputs[i];
                if (inputData.Id != null)
                {
                    if(registryMap.TryGetValue(inputData.Id.Value, out var registry)) nodes[n].SetInput(i, registry);
                }
                else nodes[n].SetInput(i, new VPConstant(new VPString(inputData.Value)));
            }
        }
    }

    public Circuit(int[] inputs, int[] outputs)
    {
        this.inputs = inputs;
        this.outputs = outputs;
        foreach (var input in inputs) this.registryMap[input] = new VPDummyValue(input);
    }
}
