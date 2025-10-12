using Il2CppInterop.Runtime.Injection;
using Virial;

namespace Nebula.VoiceChat;

public class VoiceChatInfo : MonoBehaviour
{
    static VoiceChatInfo() => ClassInjector.RegisterTypeInIl2Cpp<VoiceChatInfo>();

    static SpriteLoader backgroundSprite = SpriteLoader.FromResource("Nebula.Resources.UpperBackground.png", 100f);
    

    private MetaScreen myScreen = null!;

    void Awake() {
        ModSingleton<VoiceChatInfo>.Instance = this;
        gameObject.AddComponent<SpriteRenderer>().sprite = backgroundSprite.GetSprite();
        myScreen = MetaScreen.GenerateScreen(new Vector2(2f, 0.45f), transform, new Vector3(0, 0, -1f), false, false, false);
        time = 2.4f;
    }

    void OnDestroy()
    {
        if (ModSingleton<VoiceChatInfo>.Instance == this) ModSingleton<VoiceChatInfo>.Instance = null!;
    }

    private IMetaWidgetOld? currentWidget = null;

    float time = 0f;
    public void Update()
    {
        float y = transform.localPosition.y;

        if (ModSingleton<NoSVCRoom>.Instance != null)
        {

            var widget = ModSingleton<NoSVCRoom>.Instance.UpdateWidget(out var found);
            if (widget != currentWidget || found) time = 2.4f;
            if (widget != currentWidget)
            {
                currentWidget = widget;
                myScreen.SetWidget(currentWidget);
            }


            if (time > 0f)
            {
                time -= Time.deltaTime;
                y -= (y - 2.6f) * Mathf.Clamp01(Time.deltaTime * 5.1f);
            }
            else
            {
                y -= (y - 4f) * Mathf.Clamp01(Time.deltaTime * 2.8f);
            }
        }
        else
        {
            y -= (y - 4f) * Mathf.Clamp01(Time.deltaTime * 2.8f);
        }

        transform.localPosition = new Vector3(0f, y, transform.localPosition.z);
    }
}
