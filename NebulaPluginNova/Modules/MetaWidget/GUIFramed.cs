using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.GUIWidget;

public class NoSGUIFramed : AbstractGUIWidget
{
    public UnityEngine.Vector2 Margin { get; }
    public Color? Color = null;
    public GUIWidgetSupplier? Inner { get; }
    public Action<SpriteRenderer>? PostBuilder { get; set; }

    public NoSGUIFramed(GUIAlignment alignment, GUIWidgetSupplier? inner, UnityEngine.Vector2 margin, Color? color = null) : base(alignment)
    {
        this.Inner = inner;
        this.Margin = margin;
        this.Color = color;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var frame = UnityHelper.CreateObject("SizedFrame", null, new(0f, 0f, -0.8f));
        Virial.Media.GUIWidget? innerWidget = Inner?.Invoke();
        actualSize = new(0f, 0f);
        if (innerWidget != null)
        {
            var innerObj = innerWidget.Instantiate(size, out actualSize);
            innerObj?.transform.SetParent(frame.transform, false);
            if (innerObj != null) innerObj.transform.localPosition = new(0f, 0f, -0.1f);
        }

        var renderer = NebulaAsset.CreateSharpBackground(new(actualSize.Width + Margin.x * 1.8f, actualSize.Height + Margin.y * 1.8f), Color ?? UnityEngine.Color.white, frame.transform);

        actualSize.Width += Margin.x * 2f;
        actualSize.Height += Margin.y * 2f;

        PostBuilder?.Invoke(renderer);

        return frame.gameObject;
    }
}