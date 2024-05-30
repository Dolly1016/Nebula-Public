using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

namespace Virial.Media;

public interface Hint
{
    public GUIWidgetSupplier GUI { get; }
}

public class HintWithImage : Hint
{
    private Image Image { get; init; }
    private TextComponent Title { get; init; }
    private TextComponent Detail { get; init; }

    GUIWidgetSupplier Hint.GUI => () =>
    {
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.Margin(new(null, 0.5f)),
            NebulaAPI.GUI.Image(GUIAlignment.Center, Image, new(4f, 2.1f)),
            NebulaAPI.GUI.Margin(new(null, 0.6f)),
            NebulaAPI.GUI.Text(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentTitle), Title),
            NebulaAPI.GUI.Margin(new(null, 0.1f)),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Center, NebulaAPI.GUI.HorizontalMargin(0.16f), NebulaAPI.GUI.Text(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), Detail))
            );
    };

    public HintWithImage(Image image, TextComponent title, TextComponent detail)
    {
        Image = image;
        Title = title;
        Detail = detail;
    }
}

public class HintOnlyText : Hint
{
    private TextComponent Title { get; init; }
    private TextComponent Detail { get; init; }

    GUIWidgetSupplier Hint.GUI => () =>
    {
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.Margin(new(null, 2.1f + 0.6f + 0.5f)),
            NebulaAPI.GUI.Text(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentTitle), Title),
            NebulaAPI.GUI.Margin(new(null, 0.1f)),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Center,NebulaAPI.GUI.HorizontalMargin(0.16f), NebulaAPI.GUI.Text(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), Detail))
            );
    };

    public HintOnlyText(TextComponent title, TextComponent detail)
    {
        Title = title;
        Detail = detail;
    }
}