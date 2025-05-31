using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.VisualProgramming.UI;

internal interface IGraphNode
{
    IGraphInVertex GetExecutionInput();

    Vector2 Position { get; }

    /// <summary>
    /// ノードを移動させます。
    /// </summary>
    /// <param name="position"></param>
    void MoveTo(Vector2 position);

}
