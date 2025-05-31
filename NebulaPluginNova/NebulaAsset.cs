using Cpp2IL.Core.Extensions;
using Hazel;
using Nebula.Behavior;
using Nebula.Utilities;
using System.Reflection;
using UnityEngine.Audio;
using Virial.Runtime;
using Virial.Text;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

namespace Nebula;

public enum NebulaAudioClip { 
    ThrowAxe,
    SniperShot,
    SniperEquip,
    Trapper2s,
    Trapper3s,
    TrapperKillTrap,
    Camera,
    PaparazzoDisclose,
    FakeSabo,
    Destroyer1,
    Destroyer2,
    Destroyer3,
    HadarDive,
    HadarGush,
    Justice1,
    Bubble1,
    Bubble2,
    BubbleLong,
    Justice2,
    ExplosionFar1,
    ExplosionFar2,
    ExplosionNear,
    MeteorAlert,
    SpectreEat,
    BalloonBounce,
    BalloonFailed,
    BalloonPop,
    BalloonKill,
    BalloonRock,
    Climber1,
    Climber2,
    Slingshot,
    ButtonBreaking,
    SnatcherSuccess,
    SnatcherFailed,
}

public static class SoundManagerHelper
{
    public static AudioSource PlayOneShot(this SoundManager soundManager, AudioClip clip, bool loop, float volume = 1f, AudioMixerGroup? audioMixer = null)
    {
        if (clip == null) return null!;
        audioMixer ??= loop ? soundManager.MusicChannel : soundManager.SfxChannel;
        
        AudioSource audioSource;
        if (soundManager.allSources.TryGetValue(clip, out audioSource))
        {
            audioSource.volume = volume;
            audioSource.outputAudioMixerGroup = audioMixer;
            audioSource.loop = loop;
            audioSource.PlayOneShot(clip);
        }
        else
        {
            audioSource = soundManager.gameObject.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = audioMixer;
            audioSource.playOnAwake = false;
            audioSource.volume = volume;
            audioSource.loop = loop;
            audioSource.clip = clip;
            audioSource.PlayOneShot(clip);
            soundManager.allSources.Add(clip, audioSource);
        }
        return audioSource;
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public static class NebulaAsset
{
    static internal AssetBundle AssetBundle { get; private set; } = null!;

    private static T Load<T>(string path) where T : UnityEngine.Object => AssetBundle.LoadAsset<T>(path).MarkDontUnload();
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Assets");

        var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nebula.Resources.Assets.nebula_asset");
        AssetBundle = AssetBundle.LoadFromMemory(resourceStream!.ReadBytes());

        MultiplyShader = Load<Shader>("Sprites-Multiply");
        MultiplyBackShader = Load<Shader>("Sprites-MultiplyBackground");
        StoreBackShader = Load<Shader>("Sprites-StoreBackground");
        CustomShadowShader = Load<Shader>("Sprites-CustomShadow");
        GuageShader = Load<Shader>("Sprites-Guage");
        WhiteShader = Load<Shader>("Sprites-White");
        ProgressShader = Load<Shader>("Sprites-Progress");
        HSVShader = Load<Shader>("Sprites-HSV");
        HSVNAShader = Load<Shader>("Sprites-HSV-NoAlpha");
        MeshRendererShader = Load<Shader>("Sprites-ForMeshRenderer");
        MeshRendererMaskedShader = Load<Shader>("Sprites-ForMeshRendererMasked");
        RingMenuShader = Load<Shader>("Sprites-RingMenu");

        DivMap[0] = Load<GameObject>("SkeldDivMap");
        DivMap[1] = Load<GameObject>("MIRADivMap");
        DivMap[2] = Load<GameObject>("PolusDivMap");
        DivMap[3] = null!;
        DivMap[4] = Load<GameObject>("AirshipDivMap");
        DivMap[5] = Load<GameObject>("FungleDivMap");

        audioMap[NebulaAudioClip.ThrowAxe] = Load<AudioClip>("RaiderThrow.wav");
        audioMap[NebulaAudioClip.SniperShot] = Load<AudioClip>("SniperShot.wav");
        audioMap[NebulaAudioClip.SniperEquip] = Load<AudioClip>("SniperEquip.wav");
        audioMap[NebulaAudioClip.Trapper2s] = Load<AudioClip>("PlaceTrap2s.wav");
        audioMap[NebulaAudioClip.Trapper3s] = Load<AudioClip>("PlaceTrap3s.wav");
        audioMap[NebulaAudioClip.TrapperKillTrap] = Load<AudioClip>("PlaceKillTrap.wav");
        audioMap[NebulaAudioClip.Camera] = Load<AudioClip>("Camera.mp3");
        audioMap[NebulaAudioClip.FakeSabo] = Load<AudioClip>("FakeSabo.ogg");
        audioMap[NebulaAudioClip.Destroyer1] = Load<AudioClip>("Destroyer1.ogg");
        audioMap[NebulaAudioClip.Destroyer2] = Load<AudioClip>("Destroyer2.ogg");
        audioMap[NebulaAudioClip.Destroyer3] = Load<AudioClip>("Destroyer3.ogg");
        audioMap[NebulaAudioClip.HadarDive] = Load<AudioClip>("HadarDive.wav");
        audioMap[NebulaAudioClip.HadarGush] = Load<AudioClip>("HadarReappear.wav");
        audioMap[NebulaAudioClip.Justice1] = Load<AudioClip>("Justice1.mp3");
        audioMap[NebulaAudioClip.Justice2] = Load<AudioClip>("Justice2.mp3");
        audioMap[NebulaAudioClip.PaparazzoDisclose] = Load<AudioClip>("PaparazzoDisclose.mp3");
        audioMap[NebulaAudioClip.Bubble1] = Load<AudioClip>("Bubble1.mp3");
        audioMap[NebulaAudioClip.Bubble2] = Load<AudioClip>("Bubble2.mp3");
        audioMap[NebulaAudioClip.BubbleLong] = Load<AudioClip>("BubbleLong.mp3");
        audioMap[NebulaAudioClip.ExplosionFar1] = Load<AudioClip>("ExplosionFar.mp3");
        audioMap[NebulaAudioClip.ExplosionFar2] = Load<AudioClip>("ExplosionFar2.mp3");
        audioMap[NebulaAudioClip.ExplosionNear] = Load<AudioClip>("ExplosionNear.mp3");
        audioMap[NebulaAudioClip.MeteorAlert] = Load<AudioClip>("MeteorAlert.mp3");
        audioMap[NebulaAudioClip.SpectreEat] = Load<AudioClip>("SpectreFriedSE.ogg");
        audioMap[NebulaAudioClip.BalloonBounce] = Load<AudioClip>("BalloonBounce.ogg");
        audioMap[NebulaAudioClip.BalloonFailed] = Load<AudioClip>("BalloonFailed.ogg");
        audioMap[NebulaAudioClip.BalloonPop] = Load<AudioClip>("BalloonPop.mp3");
        audioMap[NebulaAudioClip.BalloonKill] = Load<AudioClip>("BalloonKill.ogg");
        audioMap[NebulaAudioClip.BalloonRock] = Load<AudioClip>("BalloonRock.ogg");
        audioMap[NebulaAudioClip.Climber1] = Load<AudioClip>("Climber1.mp3");
        audioMap[NebulaAudioClip.Climber2] = Load<AudioClip>("Climber2.mp3");
        audioMap[NebulaAudioClip.Slingshot] = Load<AudioClip>("Slingshot.ogg");
        audioMap[NebulaAudioClip.ButtonBreaking] = Load<AudioClip>("ButtonBreaking.mp3");
        audioMap[NebulaAudioClip.SnatcherSuccess] = Load<AudioClip>("SnatcherSuccess.mp3");
        audioMap[NebulaAudioClip.SnatcherFailed] = Load<AudioClip>("SnatcherFailed.mp3");
        BrokenShaderMat = Load<Material>("BrokenShaderMat");

        PaparazzoShot = Load<GameObject>("PhotoObject");
        FriesMinigame = Load<GameObject>("SpectreFriedMinigame");
        SlingshotInMinigame = Load<GameObject>("Slingshot");

        JusticeFont = new FontAssetNoS(JsonStructure.Deserialize<FontAssetNoSInfo>(StreamHelper.OpenFromResource("Nebula.Resources.JusticeFont.json")!)!, new ResourceTextureLoader("Nebula.Resources.JusticeFont.png"));
    }

    private static T LoadAsset<T>(this AssetBundle assetBundle, string name) where T : UnityEngine.Object
    {
        return assetBundle.LoadAsset(name, Il2CppType.Of<T>())?.Cast<T>()!;
    }

    public static T LoadAsset<T>(string name) where T : UnityEngine.Object
    {
        return AssetBundle.LoadAsset(name, Il2CppType.Of<T>())?.Cast<T>()!;
    }

    public static Sprite GetMapSprite(byte mapId, Int32 mask, Vector2? size = null)
    {
        GameObject prefab = DivMap[mapId];
        if (prefab == null) return null!;
        if (size == null) size = prefab.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite.bounds.size * 100f;
        var obj = GameObject.Instantiate(prefab);
        Camera cam = obj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = size.Value.y / 200;
        cam.transform.localScale = Vector3.one;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.cullingMask = 1 << 30;
        cam.enabled = true;

        try
        {
            int children = obj.transform.childCount;
            for (int i = 0; i < children; i++)
            {
                var c = obj.transform.GetChild(i);
                bool enable = (mask & 1) == 1;
                c.GetComponent<Renderer>().material.SetFloat("_Coeff", enable ? 1f : 0.35f);
                c.transform.localPosition += new Vector3(0, 0, 1);
                mask >>= 1;
            }
        }
        catch
        {
        }


        RenderTexture rt = new RenderTexture((int)size.Value.x, (int)size.Value.y, 16);
        rt.Create();

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = cam.targetTexture;
        Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, false);
        texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2D.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;
        GameObject.Destroy(rt);
        GameObject.Destroy(obj);

        return texture2D.ToSprite(100f);
    }

    static public FontAssetNoS JusticeFont { get; private set; } = null!;
    static public Shader MultiplyShader { get; private set; } = null!;
    static public Shader MultiplyBackShader { get; private set; } = null!;
    static public Shader StoreBackShader { get; private set; } = null!;
    static public Shader CustomShadowShader { get; private set; } = null!;
    static public Shader GuageShader { get; private set; } = null!;
    static public Shader WhiteShader { get; private set; } = null!;
    static public Shader ProgressShader { get; private set; } = null!;
    static public Shader HSVShader { get; private set; } = null!;
    static public Shader HSVNAShader { get; private set; } = null!;
    static public Shader MeshRendererShader { get; private set; } = null!;
    static public Shader MeshRendererMaskedShader { get; private set; } = null!;
    static public Shader RingMenuShader { get; private set; } = null!;

    static public ResourceExpandableSpriteLoader SharpWindowBackgroundSprite = new("Nebula.Resources.StatisticsBackground.png", 100f,5,5);
    static public Material BrokenShaderMat { get; private set; } = null!;
    static public GameObject PaparazzoShot { get; private set; } = null!;
    static public GameObject FriesMinigame { get; private set; } = null!;
    static public GameObject SlingshotInMinigame { get; private set; } = null!;

    static public SpriteRenderer CreateSharpBackground(Vector2 size, Color color, Transform transform)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Background", transform, new Vector3(0, 0, 0.25f));
        return CreateSharpBackground(renderer, color, size);
    }

    static public SpriteRenderer CreateSharpBackground(SpriteRenderer renderer, Color color, Vector2 size)
    {
        renderer.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.color = color;
        renderer.size = size;
        return renderer;
    }

    public static GameObject[] DivMap { get; private set; } = new GameObject[6];
    private static Dictionary<NebulaAudioClip, AudioClip> audioMap = [];

    public static void PlayNamedLoopSE(NebulaAudioClip clip, string name, float volume = 0.8f, float pitch = 1.0f)
    {
        var source = SoundManager.Instance.PlayNamedSound(name, audioMap[clip], true, SoundManager.Instance.SfxChannel);
        source.volume = volume;
        source.pitch = pitch;
    }

    public static void StopNamedSE(string name) => SoundManager.Instance.StopNamedSound(name);

    public static void PlaySE(NebulaAudioClip clip, bool oneshot = false, float volume = 0.8f, float pitch = 1.0f)
    {
        if (oneshot)
            SoundManager.Instance.PlayOneShot(audioMap[clip], false, volume).pitch = pitch;
        else
            SoundManager.Instance.PlaySound(audioMap[clip],false,volume).pitch = pitch;
    }

    public static void PlaySE(NebulaAudioClip clip, Vector2 pos, float minDistance, float maxDistance, float volume = 1f) => PlaySE(audioMap[clip], pos, minDistance, maxDistance);

    public static void PlaySE(AudioClip clip,Vector2 pos,float minDistance,float maxDistance, float volume = 1f)
    {
        var audioSource = UnityHelper.CreateObject<AudioSource>("SEPlayer", null, pos);

        audioSource.volume = volume;

        audioSource.transform.position = pos;
        audioSource.priority = 0;
        audioSource.spatialBlend = 1f;
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.maxDistance = maxDistance;
        audioSource.minDistance = minDistance;
        audioSource.rolloffMode = UnityEngine.AudioRolloffMode.Linear;
        audioSource.outputAudioMixerGroup = SoundManager.Instance.SfxChannel;
        audioSource.PlayOneShot(audioSource.clip);

        IEnumerator CoPlay()
        {
            Camera.main.gameObject.TryGetComponent<AudioListener>(out var listener);
            while (audioSource.isPlaying)
            {
                if (listener)
                {
                    //至近距離の音は指向性を無くす
                    float distance = listener.transform.position.Distance(audioSource.transform.position);
                    audioSource.spatialBlend = distance < 0.5f ? 0f : distance < 1.5f ? distance - 0.5f : 1f;
                }
                yield return null;
            }
            GameObject.Destroy(audioSource.gameObject);
            yield break;
        }

        NebulaManager.Instance.StartCoroutine(CoPlay().WrapToIl2Cpp());
    }

    public static readonly RemoteProcess<(NebulaAudioClip clip, Vector2 pos, float minDistance, float maxDistance)> RpcPlaySE = new(
        "PlaySE",
        (message, _) => PlaySE(message.clip, message.pos, message.minDistance, message.maxDistance)
    );

    public static TextMeshNoS InstantiateText(string objName, Transform? parent, Vector3 localPos, FontAssetNoS font, float size, Virial.Text.TextAlignment textAlignment, Vector2 pivot,string text,UnityEngine.Color color, bool showOnlyInMask = false)
    {
        var textObj = UnityHelper.CreateObject<TextMeshNoS>(objName, parent, localPos);
        textObj.Font = font;
        textObj.FontSize = size;
        textObj.TextAlignment = textAlignment;
        textObj.Pivot = pivot;
        textObj.Text = text;
        textObj.Material = new(showOnlyInMask ? MeshRendererMaskedShader : MeshRendererShader);
        textObj.Color = color;
        return textObj;
    }
}
