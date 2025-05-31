using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.VisualProgramming;

namespace Nebula.VisualProgramming;

/// <summary>
/// シリアライズ可能なレジスタの情報です。
/// </summary>
internal class SerializableRegister
{
    [JsonSerializableField(true)]
    public string Value = null!;
    [JsonSerializableField(true)]
    public int? Id = null!;
}

internal class SerializableNode
{
    [JsonSerializableField]
    public List<SerializableRegister> Inputs;
    [JsonSerializableField]
    public List<int> Outputs;
    [JsonSerializableField]
    public string Operation;
    [JsonSerializableField]
    public List<int> Followers;


    [JsonSerializableField]
    public float X = 0f;
    [JsonSerializableField]
    public float Y = 0f;

    public INode GenerateUnconnectedNode(NodesCollection collection)
    {
        if(collection.TryGetNode(Operation, out var factory))
        {
            return factory.GenerateNode(Outputs);
        }
        throw new InvalidDataException("Unknown operation error.");
    }
}

internal class SerializableCircuit
{
    [JsonSerializableField]
    public List<SerializableNode> Nodes;
    public List<int> Inputs;
    public List<int> Outputs;
    public List<int> StartNodes;

    public ICircuit GenerateCircuit(NodesCollection collection)
    {
        Circuit circuit = new(Inputs.ToArray(), Outputs.ToArray());
        foreach (var node in Nodes) circuit.AddNode(node.GenerateUnconnectedNode(collection));
        circuit.ConnectAll(Nodes);
        return circuit;
    }
}
