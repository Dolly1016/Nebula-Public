using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Components;

public interface IHudContent
{
    /// <summary>
    /// 表示位置の優先順位です。
    /// </summary>
    int Priority { get; set; }

    bool IsLeftSide { get; }
    bool IsRightSide => !IsLeftSide;
    bool IsKillButtonContent { get; set; }
    bool IsStaticContent { get; }

    /// <summary>
    /// 末尾の行に配置されるべき要素
    /// </summary>
    bool ShouldBeInLastLine { get; set; }
}
