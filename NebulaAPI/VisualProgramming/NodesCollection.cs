using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming;

public delegate Virial.VisualProgramming.INode NodeGenerator(IReadOnlyList<int> outputId);

public record NodeProperty (Func<string> nodeDesc, int InputLength, int OutputLength, Func<int, string> InputDesc, Func<int, string> OutputDesc, Func<int, VPTypeLimitation> InputLimitation, Func<int, IVPTypeProperty> OutputLimitation, int ExecutionInputLength, int ExecutionOutputLength)
{ 

}

public class NodeFactory
{
    private NodeGenerator generator;
    private NodeProperty property;

    public NodeFactory(NodeGenerator generator, NodeProperty property)
    {
        this.generator = generator;
        this.property = property;
    }

    public INode GenerateNode(IReadOnlyList<int> outputId) => generator.Invoke(outputId);
    public NodeProperty Property => property;
}

public interface INodesCollection
{
    bool TryGetNode(string operation, [MaybeNullWhen(false)] out NodeFactory factory);
}
