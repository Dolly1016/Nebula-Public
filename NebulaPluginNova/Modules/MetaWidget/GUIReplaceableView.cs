using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.GUIWidget;

/// <summary>
/// 大きさの固定された、中身だけを後から差し替えられる領域。
/// 画面全体を作り直さずに一部だけを更新したいときに使う。
/// </summary>
public class GUIReplaceableView : AbstractGUIWidget
{
    public Virial.Compat.Vector2 Size { get; init; }
    public GUIWidgetSupplier? Inner { get; init; } = null;

    /// <summary>生成された領域。生成前や破棄後はnullまたは無効な画面になる。</summary>
    public GUIScreen? Screen { get; private set; } = null;

    public GUIReplaceableView(GUIAlignment alignment, Virial.Compat.Vector2 size, GUIWidgetSupplier? inner = null) : base(alignment)
    {
        Size = size;
        Inner = inner;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var view = UnityHelper.CreateObject("ReplaceableView", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        //中身は領域の中心に置く
        var screen = new GUIScreenImpl(Anchor.At(new(0.5f, 0.5f)), new(Size), view.transform, UnityEngine.Vector3.zero);
        Screen = screen;
        if (Inner != null) screen.SetWidget(Inner.Invoke(), out _);

        actualSize = new(Size);
        return view;
    }

    /// <summary>中身を差し替える。生成前なら何もしない。</summary>
    public void SetWidget(Virial.Media.GUIWidget? widget) => Screen?.SetWidget(widget, out _);
}
