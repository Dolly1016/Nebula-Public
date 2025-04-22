using AsmResolver;
using Nebula.Modules.GUIWidget;
using UnityEngine;
using UnityEngine.Rendering;
using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.MetaWidget;


public class GUIFixedView : AbstractGUIWidget
{
    public class InnerScreen : GUIScreen
    {
        public bool IsValid => screen;

        private GameObject screen;
        private Size innerSize;
        private Anchor myAnchor;

        public InnerScreen(GameObject screen, Size innerSize)
        {
            this.screen = screen;
            this.innerSize = innerSize;
            this.myAnchor = new(new(0f, 1f), new(-innerSize.Width * 0.5f, innerSize.Height * 0.5f, -0.01f));
        }

        public void SetWidget(Virial.Media.GUIWidget? widget, out Size actualSize)
        {
            screen.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => GameObject.Destroy(obj)));

            if (widget != null)
            {
                var obj = widget.Instantiate(myAnchor, innerSize, out actualSize);
                if (obj != null) obj.transform.SetParent(screen.transform, false);
            }
            else
            {
                actualSize = new Size(0f, 0f);
            }
        }
    }

    public UnityEngine.Vector2 Size { get; init; }
    public bool WithMask { get; init; } = true;

    internal ListArtifact<InnerScreen> InnerArtifact { get; private init; }
    public Artifact<GUIScreen> Artifact { get; private init; }

    public GUIWidgetSupplier? Inner { get; init; } = null;

    public GUIFixedView(Virial.Media.GUIAlignment alignment, UnityEngine.Vector2 size, GUIWidgetSupplier? inner) : base(alignment)
    {
        this.Size = size;

        this.InnerArtifact = new();
        this.Artifact = new GeneralizedArtifact<GUIScreen, InnerScreen>(InnerArtifact);
        this.Inner = inner;
    }


    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var view = UnityHelper.CreateObject("FixedView", null, new UnityEngine.Vector3(0f, 0f, 0f), LayerExpansion.GetUILayer());
        var inner = UnityHelper.CreateObject("Inner", view.transform, new UnityEngine.Vector3(-0.2f, 0f, -0.1f));
        var innerSize = Size - new UnityEngine.Vector2(0.4f, 0f);

        if (WithMask)
        {
            view.AddComponent<SortingGroup>();
            var mask = UnityHelper.CreateObject<SpriteMask>("Mask", view.transform, new UnityEngine.Vector3(-0.2f, 0, 0));
            mask.sprite = VanillaAsset.FullScreenSprite;
            mask.transform.localScale = innerSize;
        }

        var innerScreen = new InnerScreen(inner, new(innerSize));
        InnerArtifact.Values.Add(innerScreen);

        innerScreen.SetWidget(Inner?.Invoke(), out var innerActualSize);
        float height = innerActualSize.Height;

        actualSize = new(Size.x + 0.15f, Size.y + 0.08f);

        return view;
    }
}

public class GUIMasking : AbstractGUIWidget
{
    public Virial.Media.GUIWidget? Inner { get; init; } = null;

    public GUIMasking(Virial.Media.GUIWidget inner) : base(inner.Alignment)
    {
        this.Inner = inner;
    }


    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var view = UnityHelper.CreateObject("Masked", null, new UnityEngine.Vector3(0f, 0f, 0f), LayerExpansion.GetUILayer());
        var inner = UnityHelper.CreateObject("Inner", view.transform, new UnityEngine.Vector3(-0.2f, 0f, -0.1f));

        Size innerSize = new(0f, 0f);
        var obj = Inner?.Instantiate(new(100f,100f), out innerSize);
        if (obj != null) obj.transform.SetParent(inner.transform, false);

        view.AddComponent<SortingGroup>();
            var mask = UnityHelper.CreateObject<SpriteMask>("Mask", view.transform, new UnityEngine.Vector3(-0.2f, 0, 0));
            mask.sprite = VanillaAsset.FullScreenSprite;
            mask.transform.localScale = innerSize.ToUnityVector();

        actualSize = innerSize;

        return view;
    }
}
