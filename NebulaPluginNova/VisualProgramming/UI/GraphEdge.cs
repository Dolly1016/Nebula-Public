using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.VisualProgramming.UI;

internal interface IGraphEdge
{
    IGraphOutVertex From { get; }
    IGraphInVertex To { get; }

}
