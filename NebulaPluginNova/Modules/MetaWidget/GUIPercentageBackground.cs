using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.MetaWidget;

internal class GUIPercentageBackground : AbstractGUIWidget
{
    public float Percentage = 1f;
    public Color? Color = null;
    public bool WithMask = false;
    public GUIWidgetSupplier Inner { get; }
    public bool FitToActualSize { get; private init; }
    public GUIPercentageBackground(GUIAlignment alignment, GUIWidgetSupplier inner, float percentage, Color? color = null, bool fitToActualSize = true) : base(alignment)
    {
        this.Inner = inner;
        this.Percentage = percentage;
        this.Color = color;
        this.FitToActualSize = fitToActualSize;
    }

    static private Image squareImage = SpriteLoader.FromResource("Nebula.Resources.White.png", 100f);
    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var frame = UnityHelper.CreateObject("PercentageFrame", null, new(0f, 0f, 0f));

        actualSize = size;
        var widget = Inner.Invoke();
        var innerObj = widget?.Instantiate(size, out actualSize);
        
        innerObj?.transform.SetParent(frame.transform, false);
        if (innerObj != null)
        {
            if(!FitToActualSize) GUI.Instance.MoveGUIContent(widget!.Alignment, innerObj, actualSize, size, -0.1f);
        }

        var frameSize = FitToActualSize ? actualSize : size;
        if (!FitToActualSize) actualSize = size;
        var percentageRenderer = UnityHelper.CreateSpriteRenderer("Percentage", frame.transform, new((Percentage - 1f) * 0.5f * frameSize.Width, 0f, 0f));
        percentageRenderer.sprite = squareImage.GetSprite();
        percentageRenderer.gameObject.layer = LayerExpansion.GetUILayer();
        percentageRenderer.transform.localScale = new(Percentage * frameSize.Width, frameSize.Height, 1f);
        if(Color.HasValue) percentageRenderer.color = Color.Value;

        if (WithMask) percentageRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        return frame.gameObject;
    }
}
