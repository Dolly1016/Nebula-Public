using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.VisualProgramming.UI;

internal interface IGraphVertex
{
    
}

internal interface IGraphOutVertex : IGraphVertex { 
    IGraphEdge? Edge { get; }

    /// <summary>
    /// 頂点間の接続を試みます。
    /// 失敗した場合はfalseを返してください。
    /// </summary>
    /// <param name="from"></param>
    /// <returns></returns>
    bool TryConnect(IGraphInVertex from);
}

internal interface IGraphInVertex : IGraphVertex { 
    bool HasAnyInput { get; }
}
