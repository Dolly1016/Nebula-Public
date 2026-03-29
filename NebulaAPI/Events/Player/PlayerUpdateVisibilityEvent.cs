using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーの可視性を更新します。毎ティック呼び出されます。
/// 霊界視点ではプレイヤーを透明にしても半透明で視認できます。また、自分自身の見た目は透明にしても半透明で視認できます。
/// </summary>
public class PlayerUpdateVisibilityEvent : AbstractPlayerEvent
{
    public VisibilityLevel Visibility { get; set; }
    public VisibilityLevel LastVisibility { get; }

    public enum VisibilityLevel
    {
        /// <summary>
        /// 完全に見える状態。
        /// </summary>
        Visible = 0,
        /// <summary>
        /// 半透明に見える状態。
        /// </summary>
        SemiTransparent = 1,
        /// <summary>
        /// 完全に見えない状態。
        /// </summary>
        Invisible = 2
    }

    public void SetVisible() => Visibility = VisibilityLevel.Visible;
    public void SetSemitransparent() => Visibility = VisibilityLevel.SemiTransparent;
    public void SetInvisible() => Visibility = VisibilityLevel.Invisible;

    internal PlayerUpdateVisibilityEvent(Virial.Game.Player player, VisibilityLevel visibility, VisibilityLevel lastVisibility) : base(player)
    {
        Visibility = visibility;
        LastVisibility = lastVisibility;
    }
}
