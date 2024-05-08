using Il2CppInterop.Runtime.Injection;

namespace Nebula.VoiceChat;

public class VoiceChatInfo : MonoBehaviour
{
    static VoiceChatInfo() => ClassInjector.RegisterTypeInIl2Cpp<VoiceChatInfo>();

    static SpriteLoader backgroundSprite = SpriteLoader.FromResource("Nebula.Resources.UpperBackground.png", 100f);
    static SpriteLoader iconRadioSprite = SpriteLoader.FromResource("Nebula.Resources.UpperIconRadio.png", 100f);

    private MetaScreen myScreen = null!;

    public void Awake() {
        gameObject.AddComponent<SpriteRenderer>().sprite = backgroundSprite.GetSprite();
        myScreen = MetaScreen.GenerateScreen(new Vector2(2f, 0.45f), transform, new Vector3(0, 0, -1f), false, false, false);
    }

    private IMetaWidgetOld? radioWidget = null;
    private float timer = 0f;
    private bool isMute = false;
    private VCFilteringMode filter => NebulaGameManager.Instance?.VoiceChatManager?.FilteringMode ?? VCFilteringMode.All;
    private bool MustShow => timer > 0f || radioWidget != null || isMute || filter != VCFilteringMode.All;

    public void SetRadioWidget(string displayText,Color color)
    {
        var widget = new MetaWidgetOld();

        widget.Append(
            new ParallelWidgetOld(
            new(new MetaWidgetOld.Image(iconRadioSprite.GetSprite()) { Width = 0.22f, Alignment = IMetaWidgetOld.AlignmentOption.Center }, 0.35f),
            new(new MetaWidgetOld()
            .Append(new MetaWidgetOld.VerticalMargin(0.015f))
            .Append(new MetaWidgetOld.Text(SmallTextAttribute) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "voiceChat.info.radio" })
            .Append(new MetaWidgetOld.VerticalMargin(-0.07f))
            .Append(new MetaWidgetOld.Text(TextAttribute) { Alignment = IMetaWidgetOld.AlignmentOption.Center, RawText = displayText.Color(color) })
            , 1.6f))
            { Alignment = IMetaWidgetOld.AlignmentOption.Center });
        radioWidget = widget;

        ShowWidget();
    }

    public void UnsetRadioWidget()
    {
        radioWidget = null;
        ShowWidget();
    }

    public void ShowWidget()
    {
        timer = 2.7f;

        if (isMute && filter != VCFilteringMode.All)
            myScreen.SetWidget(new MetaWidgetOld.Text(TextAttribute) { RawText = Language.Translate("voiceChat.info.mute") + "\n" + Language.Translate("voiceChat.info.filtering" + (int)filter), Alignment = IMetaWidgetOld.AlignmentOption.Center });
        else if (filter != VCFilteringMode.All)
            myScreen.SetWidget(new MetaWidgetOld.Text(TextAttribute) { RawText = Language.Translate("voiceChat.info.filtering" + (int)filter), Alignment = IMetaWidgetOld.AlignmentOption.Center });
        else if (isMute)
            myScreen.SetWidget(new MetaWidgetOld.Text(TextAttribute) { RawText = Language.Translate("voiceChat.info.mute"), Alignment = IMetaWidgetOld.AlignmentOption.Center });
        else if (radioWidget != null)
            myScreen.SetWidget(radioWidget);
        else
            myScreen.SetWidget(new MetaWidgetOld.Text(TextAttribute) { RawText = Language.Translate("voiceChat.info.unmute"), Alignment = IMetaWidgetOld.AlignmentOption.Center });
    }

    public static TextAttributeOld TextAttribute { get; private set; } = new(TextAttributeOld.BoldAttr) { Size = new(1.2f, 0.4f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaxSize = 1.8f, FontMinSize = 1f, FontSize = 1.8f };
    public static TextAttributeOld SmallTextAttribute { get; private set; } = new(TextAttributeOld.BoldAttr) { Size = new(1.2f, 0.15f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaxSize = 1.2f, FontMinSize = 0.7f, FontSize = 1.2f };
    public void SetMute(bool mute)
    {
        if (isMute == mute) return;
        isMute = mute;

        ShowWidget();
    }

    public void Update()
    {
        float y = transform.localPosition.y;
        if (MustShow)
        {
            timer -= Time.deltaTime;
            y -= (y - 2.6f) * Mathf.Clamp01(Time.deltaTime * 4.1f);
        }
        else
        {
            y -= (y - 4f) * Mathf.Clamp01(Time.deltaTime * 2.8f);
        }

        transform.localPosition = new Vector3(0f, y, transform.localPosition.z);
    }
}
