using Virial;
using Virial.Events.Game;
using Virial.Game;
using Virial.Media;

namespace Nebula.Modules.ScriptComponents;

internal class AbilityGauge : FlexibleLifespan, IGameOperator
{
    static internal readonly ResourceExpandableSpriteLoader GuageBackgroundSprite = new("Nebula.Resources.SpectreGuageBackground.png", 100f, 10, 10);
    static private readonly MultiImage GaugeSprites = DividedExpandableSpriteLoader.FromResource("Nebula.Resources.VerticalGauge.png", 100f, 18, 5, new(0.5f, 0f), 3, 1);
    static private readonly Image GaugeScaleSprite = SpriteLoader.FromResource("Nebula.Resources.VerticalGaugeScale.png", 100f);
    private readonly Func<float> value;
    private readonly Func<bool> isInProgress;
    private const float GaugeMargin = 0.08f;

    private float lastValue = -1f;
    public float Value => value.Invoke();
    public float Max { get; private set; }

    private const float GaugeWidth = 0.4f;
    private const float GaugeHeight = 1.0f;
    private const float GaugeValueHeight = GaugeHeight - GaugeMargin; //ゲージの画像は上の余剰部分を含む高さを必要とするため、これで計算すればよい。
    private const float GaugeValueActualHeight = GaugeHeight - GaugeMargin * 2f; //実際の閾値の位置を知るためにはこちらが良い。
    public float Threshold { get; private set; }
    private Image LowIconSprite { get; }
    private Image HighIconSprite { get; }

    private static Vector2 RendererLocalPos = new(0.36f,-0.47f);
    private SpriteRenderer IconRenderer, GaugeRenderer, GaugeBaseRenderer;
    private GameObject Scaler;

    public void SetActive(bool active)
    {
        GaugeObject.SetActive(active);
    }

    GameObject GaugeObject;
    public AbilityGauge(float max, float threshold, float hue, Image lowIconSprite, Image highIconSprite, Func<float> value, Func<bool> isInProgress)
    {
        this.Threshold = threshold;
        this.LowIconSprite = lowIconSprite;
        this.HighIconSprite = highIconSprite;
        this.value = value;
        this.isInProgress = isInProgress;
        this.Max = max;

        var gauge = HudContent.InstantiateContent("Gauge", true, false, false);
        GaugeObject = gauge.gameObject;
        this.BindGameObject(GaugeObject);

        Scaler = UnityHelper.CreateObject("Scaler", gauge.transform, RendererLocalPos);
        Scaler.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        var background = UnityHelper.CreateObject<SpriteRenderer>("Background", gauge.transform, new(0f, -0.37f, 0.01f));
        background.SetAsExpandableRenderer();
        background.sprite = GuageBackgroundSprite.GetSprite();
        background.size = new(0.8f,0.2f);
        

        GaugeBaseRenderer = UnityHelper.CreateObject<SpriteRenderer>("BaseSprite", Scaler.transform, new(0f,0f,0f));
        GaugeBaseRenderer.SetAsExpandableRenderer();
        GaugeBaseRenderer.sprite = GaugeSprites.GetSprite(0);
        GaugeBaseRenderer.size = new(GaugeWidth, GaugeHeight);

        var gaugeBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("BackSprite", Scaler.transform, new(0f, 0f, -0.01f));
        gaugeBackRenderer.SetAsExpandableRenderer();
        gaugeBackRenderer.sprite = GaugeSprites.GetSprite(1);
        gaugeBackRenderer.size = new(GaugeWidth, GaugeHeight);
        gaugeBackRenderer.material = new(NebulaAsset.HSVShader);
        gaugeBackRenderer.material.SetFloat("_Hue", 360 - hue);

        GaugeRenderer = UnityHelper.CreateObject<SpriteRenderer>("FrontSprite", Scaler.transform, new(0f, 0f, -0.02f));
        GaugeRenderer.SetAsExpandableRenderer();
        GaugeRenderer.sprite = GaugeSprites.GetSprite(2);
        GaugeRenderer.size = new(GaugeWidth, GaugeMargin + 0f);
        GaugeRenderer.material = new(NebulaAsset.HSVShader);
        GaugeRenderer.material.SetFloat("_Hue", 360 - hue);

        IconRenderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", gauge.transform, new(-0.12f, -0.46f, -0.03f));
        IconRenderer.sprite = lowIconSprite.GetSprite();

        var scaleRenderer = UnityHelper.CreateObject<SpriteRenderer>("Scale", Scaler.transform, new(0f, GaugeMargin + (threshold / max) * GaugeValueActualHeight, -0.025f));
        scaleRenderer.sprite = GaugeScaleSprite.GetSprite();
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        float currentVal = Math.Clamp(Value, 0f, Max);
        bool lastLow = lastValue < Threshold;
        bool low = currentVal < Threshold;

        if(Math.Abs(lastValue - currentVal) > 0f)
        {
            if (lastLow != low) IconRenderer.sprite = (low ? LowIconSprite : HighIconSprite).GetSprite();
            GaugeRenderer.size = new(GaugeWidth, GaugeMargin + currentVal / Max * GaugeValueHeight);
            lastValue = currentVal;
        }

        if (isInProgress.Invoke())
        {
            GaugeBaseRenderer.color = Color.white;
            var sin = (float)Helpers.ScaledSin(9f) * 0.015f + 0.015f + 1f;
            Scaler.transform.localScale = new(sin, sin, 1f);
        }
        else
        {
            GaugeBaseRenderer.color = new(0.5f, 0.5f, 0.5f, 1f);
            Scaler.transform.localScale = Vector3.one;
        }

        
    }
}
