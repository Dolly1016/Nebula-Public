using Nebula.Behavior;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using Virial.Compat;
using Virial.Media;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Modules.GUIWidget;

public class GUIScrollView : AbstractGUIWidget
{
    //スクローラの位置を記憶する
    private static Dictionary<string, float> distDic = new();
    public static void RemoveDistHistory(string tag) => distDic.Remove(tag);
    public static void UpdateDistHistory(string tag, float y) => distDic[tag] = y;
    public static float TryGetDistHistory(string tag) => distDic.TryGetValue(tag, out var val) ? val : 0f;
    public static Action GetDistHistoryUpdater(Func<float> y, string tag) => () => UpdateDistHistory(tag, y.Invoke());


    public class InnerScreen : GUIScreen
    {
        public bool IsValid => screen;

        private GameObject screen;
        private Size innerSize;
        private float scrollViewSizeY;
        private Scroller scroller;
        private Collider2D scrollerCollider;
        private Anchor myAnchor;

        public InnerScreen(GameObject screen, Size innerSize, Scroller scroller, Collider2D scrollerCollider, float scrollViewSizeY)
        {
            this.screen = screen;
            this.innerSize = innerSize;
            this.scroller = scroller;
            this.scrollerCollider = scrollerCollider;
            this.scrollViewSizeY = scrollViewSizeY;
            this.myAnchor = new(new(0f, 1f), new(-innerSize.Width * 0.5f, innerSize.Height * 0.5f, -0.01f));
        }

        public void SetWidget(Virial.Media.GUIWidget? widget, out Size actualSize)
        {
            if (!screen)
            {
                actualSize = new(0f, 0f);
                return;
            }
            screen.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => GameObject.Destroy(obj)));

            if (widget != null)
            {
                var obj = widget.Instantiate(myAnchor, innerSize, out actualSize);
                if (obj != null)
                {
                    obj.transform.SetParent(screen.transform, false);

                    scroller.SetBounds(new FloatRange(0, Math.Max(0f,actualSize.Height - scrollViewSizeY)), null);
                    scroller.ScrollRelative(UnityEngine.Vector2.zero);

                    foreach (var button in screen.GetComponentsInChildren<PassiveButton>()) button.ClickMask = scrollerCollider;
                }
            }
            else
            {
                actualSize = new Size(0f,0f);
            }
        }

        public void SetStaticWidget(Virial.Media.GUIWidget? widget, Virial.Compat.Vector2 anchor, out Size actualSize)
        {
            screen.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => GameObject.Destroy(obj)));

            if (widget != null)
            {
                Virial.Compat.Vector3 anchoredPos = new(anchor.x,anchor.y,0f);
                anchoredPos.x -= 0.5f;
                anchoredPos.y -= 0.5f;
                anchoredPos.x *= innerSize.Width;
                anchoredPos.y *= innerSize.Height;

                var obj = widget.Instantiate(new(anchor, anchoredPos), innerSize, out actualSize);
                if (obj != null)
                {
                    obj.transform.SetParent(screen.transform, false);

                    scroller.SetBounds(new FloatRange(0, 0), null);
                    scroller.ScrollRelative(UnityEngine.Vector2.zero);

                    foreach (var button in screen.GetComponentsInChildren<PassiveButton>()) button.ClickMask = scrollerCollider;
                }
            }
            else
            {
                actualSize = new Size(0f, 0f);
            }
        }
    }

    public string? ScrollerTag { get; init; } = null;
    public UnityEngine.Vector2 Size { get; init; }
    public bool WithMask { get; init; } = true;
    
    internal ListArtifact<InnerScreen> InnerArtifact { get; private init; }
    public Artifact<GUIScreen> Artifact { get; private init; }

    public GUIWidgetSupplier? Inner { get; init; } = null;

    public GUIScrollView(Virial.Media.GUIAlignment alignment, UnityEngine.Vector2 size, GUIWidgetSupplier? inner) : base(alignment) {
        this.Size = size;

        this.InnerArtifact = new();
        this.Artifact = new GeneralizedArtifact<GUIScreen, InnerScreen>(InnerArtifact);
        this.Inner = inner;
    }


    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var view = UnityHelper.CreateObject("ScrollView", null, new UnityEngine.Vector3(0f, 0f, 0f),LayerExpansion.GetUILayer());
        var inner = UnityHelper.CreateObject("Inner", view.transform, new UnityEngine.Vector3(-0.2f, 0f, -0.1f));
        var innerSize = Size - new UnityEngine.Vector2(0.4f, 0f);

        if (WithMask)
        {
            view.AddComponent<SortingGroup>();
            var mask = UnityHelper.CreateObject<SpriteMask>("Mask", view.transform, new UnityEngine.Vector3(-0.2f, 0, 0));
            mask.sprite = VanillaAsset.FullScreenSprite;
            mask.transform.localScale = innerSize;
        }

        var scroller = VanillaAsset.GenerateScroller(Size, view.transform, new UnityEngine.Vector2(Size.x / 2 - 0.15f, 0f), inner.transform, new FloatRange(0, Size.y), Size.y);
        var hitBox = scroller.GetComponent<Collider2D>();
        var innerScreen = new InnerScreen(inner, new(innerSize), scroller, hitBox,Size.y);
        InnerArtifact.Values.Add(innerScreen);

        innerScreen.SetWidget(Inner?.Invoke(), out var innerActualSize);
        float height = innerActualSize.Height;

        scroller.SetBounds(new FloatRange(0, Math.Max(0f,height - Size.y)), null);

        if (ScrollerTag != null && distDic.TryGetValue(ScrollerTag, out var val))
            scroller.Inner.transform.localPosition = scroller.Inner.transform.localPosition +
                new UnityEngine.Vector3(0f, Mathn.Clamp(val + scroller.ContentYBounds.min, scroller.ContentYBounds.min, scroller.ContentYBounds.max), 0f);

        if (ScrollerTag != null)
            scroller.Inner.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += () => { distDic[ScrollerTag] = scroller.Inner.transform.localPosition.y - scroller.ContentYBounds.min; };

        actualSize = new(Size.x + 0.15f, Size.y + 0.08f);
        scroller.UpdateScrollBars();

        return view;
    }
}


/// <summary>
/// スクロール位置を調整するスクリプト。
/// 特定のウィジェットの表示位置を追跡するために使用する。
/// </summary>
internal class ScrollViewInitializer
{
    private GameObject targetObject;
    private Size size;
    private record ScrollViewInfo(Transform InnerParent, Scroller Scroller, float ViewRange);
    private ScrollViewInitializer(GameObject targetObject, Size size)
    {
        this.targetObject = targetObject;
        this.size = size;
    }
    private bool LookUpScroller([MaybeNullWhen(false)]out ScrollViewInfo info)
    {
        var childParent = targetObject.transform.parent;
        while(childParent != null)
        {
            var parentParent = childParent.parent;
            if (parentParent == null) break;
            if(childParent.name == "Inner" && parentParent.name == "ScrollView")
            {
                var scroller = parentParent.FindChild("NebulaScroller");
                var mask = parentParent.FindChild("Mask");
                if (scroller != null && scroller.gameObject.TryGetComponent<Scroller>(out var scrollerComponent) && mask != null)
                {
                    info = new(childParent, scrollerComponent, mask.localScale.y);
                    return true;
                }
            }
            childParent = parentParent;
        }
        info = null;
        return false;
    }

    private IEnumerator CoAdjust()
    {
        while (true)
        {
            if (!targetObject) yield break;
            if (LookUpScroller(out var info))
            {
                var scroller = info.Scroller;

                var target_y = info.InnerParent.InverseTransformPoint(targetObject.transform.position).y;

                var ideal_scroll_y = info.ViewRange > size.Height ? -target_y : -target_y - (size.Height - info.ViewRange) / 2f;
                var final_scroll_y = scroller.ContentYBounds.Clamp(ideal_scroll_y);

                //スクロール実行ここから
                scroller.velocity = UnityEngine.Vector2.zero;
                UnityEngine.Vector3 localPosition = scroller.Inner.transform.localPosition;
                localPosition.y = final_scroll_y;
                info.InnerParent.localPosition = localPosition;
                scroller.UpdateScrollBars();
                //スクロール実行ここまで

                //強調表示ここから
                var effectPos = info.InnerParent.InverseTransformPoint(targetObject.transform.position);
                effectPos.z = 0.08f;
                var effectRenderer = UnityHelper.CreateSpriteRenderer("TargetObj", info.InnerParent, effectPos);
                effectRenderer.sprite = VanillaAsset.FullScreenSprite;
                effectRenderer.color = Color.clear;
                effectRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                effectRenderer.transform.localScale = new(size.Width + 0.12f, size.Height + 0.05f, 1f);
                effectRenderer.SetBothOrder(0);
                yield return ManagedEffects.Lerp(1.5f, p =>
                {
                    if(effectRenderer) effectRenderer.color = new UnityEngine.Color(1f, 1f, 0f, Easing.OutQuad(1f - p) * 0.3f);
                });

                if (effectRenderer) GameObject.Destroy(effectRenderer.gameObject);
                //強調表示ここまで

                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 指定の大きさのGameObjectをScrollViewの表示範囲内に収めます。表示範囲より大きい場合は、上端か下端のいずれかが表示範囲内に収まります。
    /// </summary>
    /// <param name="targetObject"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    static public IEnumerator CoAdjust(GameObject targetObject, Size size) => new ScrollViewInitializer(targetObject, size).CoAdjust();
    static public void Adjust(GameObject targetObject, Size size) => CoAdjust(targetObject, size).StartOnScene();
}

