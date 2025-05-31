using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.VisualProgramming.UI;

/// <summary>
/// プログラムのグラフと表示中のUIを表します。
/// </summary>
internal class VPGraph
{
    private readonly List<IGraphEdge> edges = [];
    private readonly List<IGraphInVertex> inVertices = [];
    private readonly List<IGraphOutVertex> outVertices = [];
    private readonly List<IGraphNode> nodes = [];

    public IReadOnlyCollection<IGraphEdge> Edges { get; }
    public IReadOnlyCollection<IGraphInVertex> InVertices { get; }
    public IReadOnlyCollection<IGraphOutVertex> OutVertices { get; }
    public IReadOnlyCollection<IGraphNode> Nodes { get; }

    void RemoveNode(IGraphNode node) { }
    void RemoveEdge(IGraphEdge edge) { }
}
