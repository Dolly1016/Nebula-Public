
using Virial;
using Virial.Accessibility;
using Virial.DI;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Media;

namespace Nebula.Modules;

internal class MeetingOverlayHolder : AbstractModule<Virial.Game.Game>, OverlayHolder, IGameOperator
{
    static IDividedSpriteLoader IconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.MeetingNotification.png", 100f, 42, 42, true);
    static Image NotificationSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingNotificationDot.png", 135f);
    static public Image[] IconsSprite = Helpers.Sequential(IconSprite.Length).Select(num => new WrapSpriteLoader(()=>IconSprite.GetSprite(num))).ToArray();

    List<(GUIWidgetSupplier overlay, Image? icon, UnityEngine.Color color,Reference<bool> isNew)> icons = new();
    Transform? shower;

    //常駐エンティティ
    public bool IsDeadObject => false;

    public MeetingOverlayHolder()
    {
        this.Register(NebulaAPI.CurrentGame!);
    }

    public void RegisterOverlay(GUIWidgetSupplier overlay, Image icon, UnityEngine.Color color)
    {
        icons.Add((overlay, icon, color, new Reference<bool>() { Value = true }));
        Generate(icons.Count - 1);
    }
    public void RegisterOverlay(GUIWidgetSupplier overlay, Image icon, Virial.Color color) => RegisterOverlay(overlay, icon, color.ToUnityColor());

    void Generate(int index)
    {
        if (!shower) return;

        if (icons.Count <= index) return;
        var icon = icons[index];

        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", shower, new(-3.6f + index * 0.48f, 0f, 0f));
        renderer.sprite = IconSprite.GetSprite(0);
        renderer.color = Color.Lerp(icon.color,Color.white,0.3f);

        var iconInner = UnityHelper.CreateObject<SpriteRenderer>("Inner", renderer.transform, new(0f, 0f, -1f));
        iconInner.sprite = icon.icon.GetSprite();

        var notification = UnityHelper.CreateObject<SpriteRenderer>("Notification", renderer.transform, new(0.19f, 0.19f, -1.5f));
        notification.sprite = NotificationSprite.GetSprite();
        notification.gameObject.SetActive(icon.isNew.Value);

        IEnumerator CoAppear()
        {
            float p = 0f;
            while(true)
            {
                p += Time.deltaTime * 2.4f;
                if (p > 1f) break;

                //曲線(0<=x<=1): x\ +\ \left(\cos0.5x\pi\ \right)^{1.5}\cdot x^{0.3}\cdot2.5
                if (p > 0f)
                    renderer.transform.localScale = Vector3.one * (p + Mathf.Pow(Mathf.Cos(0.5f * p * Mathf.PI), 1.5f) * Mathf.Pow(p, 0.3f) * 2.5f);
                else
                    renderer.transform.localScale = Vector3.zero;
                yield return null;
            }
            renderer.transform.localScale = Vector3.one;
        }

        if (icon.isNew.Value) NebulaManager.Instance.StartCoroutine(CoAppear().WrapToIl2Cpp());

        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new(0.4f, 0.4f);

        var button = renderer.gameObject.SetUpButton(false, [renderer], icon.color);
        button.OnMouseOver.AddListener(() => { VanillaAsset.PlayHoverSE(); NebulaManager.Instance.SetHelpWidget(button, icon.overlay.Invoke()); notification.gameObject.SetActive(false); icon.isNew.Set(false); });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
    }

    void OnMeetingStart(MeetingStartEvent ev)
    {
        shower = UnityHelper.CreateObject("OverlayHolder", MeetingHud.Instance.transform, new(0f, 2.7f, -20f)).transform;
        for (int i = 0; i < icons.Count; i++) Generate(i);
    }
}
