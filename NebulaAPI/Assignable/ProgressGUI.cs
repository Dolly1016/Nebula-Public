using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;
using Virial.Text;

namespace Virial.Assignable;

static public class ProgressGUI
{
    public record OneLineTextElement(float? Margin, string? Text, (Func<string> generator, int length)? Generator)
    {
        public static implicit operator OneLineTextElement(float margin) => new OneLineTextElement(margin, null, null);
        public static implicit operator OneLineTextElement(string text) => new OneLineTextElement(null, text, null);
        public static implicit operator OneLineTextElement((Func<string> generator, int length) text) => new OneLineTextElement(null, null, text);
    }

    static private GUIWidget UpdatableText(Func<string> text, int length) => NebulaAPI.GUI.RealtimeText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayContent, text, length);
    static public GUIWidget RawText(string text) => NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayContent, text);
    static public GUIWidget OneLineText(params IEnumerable<OneLineTextElement> elements) => NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Left, elements.Select(e => e.Margin.HasValue ? NebulaAPI.GUI.HorizontalMargin(e.Margin.Value) : e.Text != null ? RawText(e.Text) : UpdatableText(e.Generator!.Value.generator, e.Generator.Value.length)));
    static public GUIWidget AssignableNameText(DefinedAssignable assignable) => NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayTitle, assignable.DisplayColoredName);
    static public GUIWidget SmallAssignableNameText(DefinedAssignable assignable, string? prefix = null) => NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayContent, (prefix ?? "") + "<b>" + assignable.DisplayColoredName + "</b>");
    static public GUIWidget Holder(params IEnumerable<GUIWidget?> widgets) => NebulaAPI.GUI.VerticalHolder(Virial.Media.GUIAlignment.Left, widgets.Where(w => w != null));
}
