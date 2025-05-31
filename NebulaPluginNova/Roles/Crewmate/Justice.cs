using Il2CppInterop.Runtime.Injection;
using NAudio.CoreAudioApi;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Patches;
using Steamworks;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public interface IMultibandMater
{
    float[] Band { get; }
    int Length { get; }

    void Update();
}
public class RandomMultibandMeter : IMultibandMater{
    private float[] secretBand;
    public float[] Band { get; private set; }
    public int Length { get; private init; }
    public RandomMultibandMeter(int num) {
        Length = num;
        Band = new float[num];
        secretBand = new float[num];
        for (int i = 0; i < num; i++) Band[i] = 0f;
    }

    public void Update() {

        void SpawnWave()
        {
            float center = System.Random.Shared.NextSingle();
            float height = 0.3f * System.Random.Shared.NextSingle() * 2f;
            height *= height; //大きい山の生まれる頻度を下げる
            float width = 0.2f * System.Random.Shared.NextSingle() * 0.45f;

            for(int i = 0;i < Length; i++)
            {
                float pos = (float)i / Length;
                float x = (pos - center) / width;
                float y = height / (float)Math.Sqrt((x * x) + Math.Cos(x));
                secretBand[i] = Mathf.Max(y, secretBand[i]);
            }
        }

        //毎ティック新たな波を生成する
        for (int i = 0; i < 3; i++) SpawnWave();

        for(int i = 0;i < Length; i++)
        {
            //見た目上の値を上限値にシームレスに寄せる
            if (Band[i] < secretBand[i])
                Band[i] += (secretBand[i] - Band[i]).Delta(8f, 0.1f);
            else
                Band[i] = secretBand[i];

            //上限値を0に近づける
            secretBand[i] -= Time.deltaTime * 0.5f;
            if (secretBand[i] < 0f) secretBand[i] = 0f;
        }  
    }
}

public class JusticeMeetingHud : MonoBehaviour
{
    static JusticeMeetingHud() => ClassInjector.RegisterTypeInIl2Cpp<JusticeMeetingHud>();

    static private readonly SpriteLoader meetingBackMaskSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingUIMask.png", 100f);
    static private readonly SpriteLoader meetingBackSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingBack.png", 100f);
    static private readonly SpriteLoader meetingBackAlphaSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingBackAlpha.png", 100f);
    static private readonly SpriteLoader meetingReticleSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeMeetingReticle.png", 100f);
    static private readonly SpriteLoader meetingViewSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeMeetingView.png", 100f);
    static private readonly SpriteLoader votingHolderLeftSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeHolderLeft.png", 120f);
    static private readonly SpriteLoader votingHolderRightSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeHolderRight.png", 120f);
    static private readonly SpriteLoader votingHolderMaskSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeHolderMask.png", 120f);
    static private readonly SpriteLoader votingHolderFlashSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeHolderFlash.png", 120f);
    static private readonly SpriteLoader votingHolderBlurSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeHolderFlashBlur.png", 120f);

    static private readonly SpriteLoader circleGraphSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeCircleGraph.png", 120f);
    static private readonly SpriteLoader circleGraphBackSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeCircleBackGraph.png", 120f);
    static private readonly SpriteLoader bandGraphSprite = SpriteLoader.FromResource("Nebula.Resources.JusticeBandGraph.png", 120f);

    SpriteRenderer Background1, Background2, BackView;
    
    public GameObject InstantiateCircleGraph(Transform parent, Vector3 localPos, Color color)
    {
        var circleBack = UnityHelper.CreateSpriteRenderer("CircleGraph", parent, localPos);
        circleBack.gameObject.AddComponent<SortingGroup>();
        circleBack.sprite = circleGraphBackSprite.GetSprite();
        circleBack.color = color;
        circleBack.sortingGroupOrder = 35;
        var circleFront = UnityHelper.CreateSpriteRenderer("Front", circleBack.transform, new(0f, 0f, -0.05f));
        circleFront.sprite = circleGraphSprite.GetSprite();
        var material = new Material(NebulaAsset.GuageShader);
        material.SetFloat("_Guage", 0.5f);
        material.SetColor("_Color", Color.Lerp(color, Color.white, 0.5f));
        circleFront.material = material;
        circleFront.transform.localScale = new(-1f, 1f, 1f);
        circleFront.sortingGroupOrder = 36;


        IEnumerator CoAnimCircle()
        {
            float goal = System.Random.Shared.NextSingle();
            material.SetFloat("_Guage", goal);

            float current = 0f;
            float t = 1f;
            float slightT = 0.2f;
            while (circleFront)
            {
                current -= (current - goal).Delta(0.8f, 0.001f);
                t -= Time.deltaTime;
                slightT -= Time.deltaTime;
                if(t < 0f)
                {
                    goal = Math.Clamp(goal + (System.Random.Shared.NextSingle() - 0.5f) * 0.35f, 0f, 1f);
                    t = 1.4f + System.Random.Shared.NextSingle() * 6f;
                }
                if(slightT < 0f)
                {
                    goal = Math.Clamp(goal + (System.Random.Shared.NextSingle() - 0.5f) * 0.15f, 0f, 1f);
                    slightT = 0.2f;
                }

                material.SetFloat("_Guage", current);

                yield return null;
            }
        }
        StartCoroutine(CoAnimCircle().WrapToIl2Cpp());

        return circleBack.gameObject;
    }

    public void InstantiateBandGraph(int num, Transform parent, Vector3 center, Color color)
    {
        var bandHolder = UnityHelper.CreateObject<SortingGroup>("BandHolder", parent, center);

        SpriteRenderer[] renderers = new SpriteRenderer[num];
        float[] filter = new float[num];
        for (int i = 0; i < num; i++)
        {
            renderers[i] = UnityHelper.CreateSpriteRenderer("Graph", bandHolder.transform, new Vector3((i / (float)(num - 1) - 0.5f) * 1.8f, 0f, 0f));
            renderers[i].transform.localScale = new(1f, 0f, 1f);
            renderers[i].color = color;
            renderers[i].sprite = bandGraphSprite.GetSprite();
            filter[i] = 0.5f + Math.Clamp(0.2f * Math.Min(i, num - 1 - i), 0f, 0.5f);
        }

        IMultibandMater multibandMater = new RandomMultibandMeter(num);

        IEnumerator CoUpdate()
        {
            while (true)
            {
                multibandMater.Update();
                for (int i = 0; i < num; i++) renderers[i].transform.localScale = new(1f, multibandMater.Band[i] * filter[i] * 4.4f, 1f);
                yield return null;
            }
        }

        StartCoroutine(CoUpdate().WrapToIl2Cpp());
    }

    private IEnumerator CoAnimColor(SpriteRenderer renderer, Color color1, Color color2, float duration)
    {
        float t = 0f;
        while(t <  duration)
        {
            t += Time.deltaTime;
            renderer.color = Color.Lerp(color1, color2, t / duration);
            yield return null;
        }
        renderer.color = color2;
    }

    private IEnumerator CoAnimColorRepeat(SpriteRenderer renderer, Color color1, Color color2, float duration)
    {
        while (true)
        {
            yield return CoAnimColor(renderer, color1, color2, duration);
            yield return CoAnimColor(renderer, color2, color1, duration);
        }
    }

    public void Begin(GamePlayer player1, GamePlayer player2, Action onMeetingStart)
    {
        int photoIndex1 = player1.PlayerId % PhotoData.Length;
        int photoIndex2 = (photoIndex1 + ((player2.PlayerId + 1) % (PhotoData.Length - 2))) % PhotoData.Length;
        StartCoroutine(SetUpJusticeMeeting(MeetingHud.Instance, player1, player2, onMeetingStart, photoIndex1, photoIndex2).WrapToIl2Cpp());
    }

    private static string[] RandomTexts = ["(despired)", "terminus", "<revolt>", "solitary", "bona vacantia", "despotism", "pizza", "elitism", "suspicion", "justice", "outsider", "discrepancy", "purge", "uniformity", "conviction", "tribunal", "triumph", "heroism", "u - majority"];
    private static string[] RandomAltTexts = ["HERO", "victor", "supreme", "the one", "genius", "prodigy", "detective", "clairvoyant"];
    IEnumerator CoDisappearVotingArea(MeetingHud meetingHud, float duration)
    {
        var states = meetingHud.playerStates.OrderBy(i => Guid.NewGuid()).ToArray();
        var interval = duration / states.Length;
        foreach(var state in states)
        {
            state.gameObject.SetActive(false);
            yield return Effects.Wait(interval);
        }
    }

    IEnumerator CoAnimBackLine(GameObject parent)
    {
        var lineRenderer = UnityHelper.CreateObject<SpriteRenderer>("Line", parent.transform, new(0.01f, 0f, -18f));
        lineRenderer.sprite = VanillaAsset.FullScreenSprite;
        lineRenderer.color = Color.black.AlphaMultiplied(0.85f);
        lineRenderer.transform.localScale = new(20f, 0f, 1f);

        float t = 0f;
        float p = 0f;
        while(t < 2f && parent)
        {
            p += (1 - p).Delta(3.5f, 0.01f);
            lineRenderer.transform.localScale = new(8.79f, p * 0.76f, 1f);
            t += Time.deltaTime;
            yield return null;
        }

        yield return Effects.Wait(1.25f);
        t = 0f;
        while (t < 1f && parent)
        {
            p -= p.Delta(6.9f, 0.01f);
            lineRenderer.transform.localScale = new(8.79f, p * 0.76f, 1f);
            t += Time.deltaTime;
            yield return null;
        }

    }

    IEnumerator CoAnimIntroText(GameObject parent)
    {
        var introText = UnityHelper.CreateObject<TextMeshNoS>("IntroText", parent.transform, new(0f, 0f, -18.5f));
        introText.Font = NebulaAsset.JusticeFont;
        introText.FontSize = 0.48f;
        introText.TextAlignment = Virial.Text.TextAlignment.Center;
        introText.Pivot = new(0.5f, 0.5f);
        introText.Text = "";
        introText.Material = UnityHelper.GetMeshRendererMaterial();
        introText.Color = Color.white;
        yield return null;
        string completedText = System.Random.Shared.NextSingle() < 0.1f ? "The balance is at will!" : "Justice meeting begins...";

        for(int i = 0;i<completedText.Length;i++)
        {
            introText.Text = completedText.Substring(0,i + 1);
            yield return Effects.Wait(0.078f);
        }
        for(int i = 0; i < 3; i++)
        {
            introText.gameObject.SetActive(false);
            yield return Effects.Wait(0.04f);
            introText.gameObject.SetActive(true);
            yield return Effects.Wait(0.04f);
        }
    }

    IEnumerator CoPlayAlertFlash()
    {
        yield return Effects.Wait(0.15f);
        for(int i = 0; i < 3; i++)
        {
            AmongUsUtil.PlayCustomFlash(Color.red, 0.2f, 0.2f, 0.3f, 0.6f);
            yield return Effects.Wait(1.0f + 0.3f);
        }
    }

    static (int photoIndex, Vector2 localPos, float scale)[] PhotoData = [/*(0, new(-1.04f, 0.62f), 0.52f),*/ (1, new(-0.845f, 0.816f), 0.76f), (2, new(-1.04f, 0.77f), 0.7f), (3, new(-1.06f, 0.805f), 0.73f), (4, new(-1.06f, 0.89f), 0.9f), (5, new(-1.07f, 0.81f), 0.75f), (6, new(-0.93f, 0.73f), 0.65f), (7, new(-1.06f, 0.77f), 0.7f)];
    IEnumerator SetUpJusticeMeeting(MeetingHud meetingHud, GamePlayer player1, GamePlayer player2, Action onMeetingStart, int imageIndex1, int imageIndex2)
    {
        meetingHud.TimerText.gameObject.SetActive(false);
        NebulaAsset.PlaySE(NebulaAudioClip.Justice1);
        StartCoroutine(CoDisappearVotingArea(meetingHud, 2.3f).WrapToIl2Cpp());
        StartCoroutine(CoPlayAlertFlash().WrapToIl2Cpp());

        yield return Effects.Wait(0.1f);
        StartCoroutine(CoDisappearVotingArea(meetingHud, 2.3f).WrapToIl2Cpp());

        var introObj = UnityHelper.CreateObject("IntroObj", transform,Vector3.zeroVector);
        StartCoroutine(CoAnimBackLine(introObj).WrapToIl2Cpp());
        StartCoroutine(CoAnimIntroText(introObj).WrapToIl2Cpp());

        var black = UnityHelper.CreateSpriteRenderer("Black", transform, new(0f, 0f, -19f));
        black.transform.localScale = new(1.2f, 1f, 1f);
        black.sprite = meetingBackMaskSprite.GetSprite();
        black.color = new(0f, 0f, 0f, 0f);

        yield return Effects.Wait(2.5f);
        NebulaManager.Instance.StartDelayAction(0.2f, () => NebulaAsset.PlaySE(NebulaAudioClip.Justice2));
        yield return CoAnimColor(black, new(0f, 0f, 0f, 0f), Color.black, 1.2f);
        StartCoroutine(ManagedEffects.Sequence(Effects.Wait(1f).WrapToManaged(), CoAnimColor(black, Color.black, new(0f, 0f, 0f, 0f), 1f), ManagedEffects.Action(()=>GameObject.Destroy(black.gameObject))).WrapToIl2Cpp());
        GameObject.Destroy(introObj);
        meetingHud.TimerText.gameObject.SetActive(true);

        onMeetingStart.Invoke();

        //タイトルテキストが少し右にずれているので修正
        meetingHud.TitleText.transform.localPosition = new(-0.25f, 2.2f, -1f);
        meetingHud.TitleText.text = Language.Translate("game.meeting.justiceMeeting");

        //背景を作る
        var backObj = UnityHelper.CreateObject<SortingGroup>("JusticeBackground", transform, new(0f, 0f, 7f));

        //背景マスク
        var mask = UnityHelper.CreateObject<SpriteMask>("JusticeMask", backObj.transform, Vector3.zero);
        mask.transform.localScale = new(1.2f, 1f, 1f);
        mask.sprite = meetingBackMaskSprite.GetSprite();

        Background1 = UnityHelper.CreateSpriteRenderer("JusticeBack", backObj.transform, new(0f, 0f, 0f));
        Background2 = UnityHelper.CreateSpriteRenderer("JusticeBackAlpha", backObj.transform, new(0f, 0f, -0.1f));
        Background1.sprite = meetingBackSprite.GetSprite();
        Background2.sprite = meetingBackAlphaSprite.GetSprite();
        Background1.transform.localScale = new(4.5f, 2.6f, 1f);
        Background2.transform.localScale = new(4.5f, 2.6f, 1f);
        Background1.color = new(0.002f, 0.03f, 0.16f);
        Background2.color = Justice.MyRole.UnityColor.RGBMultiplied(0.21f);
        Background1.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        Background2.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        StartCoroutine(CoAnimColorRepeat(Background1, new(0.002f, 0.03f, 0.16f), new(0.002f, 0.1f, 0.1f), 5f).WrapToIl2Cpp());

        var backReticle = UnityHelper.CreateSpriteRenderer("JusticeBackReticle", backObj.transform, new(0f, 0f, -0.2f));
        backReticle.transform.localScale = new(0.69f, 0.69f, 1f);
        backReticle.sprite = meetingReticleSprite.GetSprite();
        backReticle.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        var reticleText = NebulaAsset.InstantiateText("ReticleText", backObj.transform, new(0f, -1.9f, -0.2f), NebulaAsset.JusticeFont, 0.42f, Virial.Text.TextAlignment.Center, new(0.5f,0.5f), "", new(0.7f, 0.7f, 0.7f, 0.4f));
        
        IEnumerator CoAnimText(string text)
        {
            reticleText.Text = "";
            reticleText.gameObject.SetActive(true);
            yield return null;
            string targetText = text;
            for(int i = 1;i<=targetText.Length; i++)
            {
                reticleText.Text = targetText.Substring(0, i);
                yield return Effects.Wait(0.08f);
            }
            for (int i = 0; i < 3; i++)
            {
                reticleText.gameObject.SetActive(false);
                yield return Effects.Wait(0.05f);
                reticleText.gameObject.SetActive(true);
                yield return Effects.Wait(0.05f);
            }

            yield return Effects.Wait(5f + System.Random.Shared.NextSingle() * 8f);

            for (int i = 0; i < 3; i++)
            {
                reticleText.gameObject.SetActive(false);
                yield return Effects.Wait(0.05f);
                reticleText.gameObject.SetActive(true);
                yield return Effects.Wait(0.05f);
            }
            reticleText.gameObject.SetActive(false);
            yield return Effects.Wait(0.6f + System.Random.Shared.NextSingle() * 0.6f);
        }
        IEnumerator CoRepeatAnimText()
        {
            string[] texts = [];
            int index = 0;
            while (true)
            {
                if (index == texts.Length) {
                    texts = RandomTexts.OrderBy(_ => Guid.NewGuid()).ToArray();
                    index = 0;
                }

                yield return CoAnimText(texts[index++]);
            }
        }
        StartCoroutine(CoRepeatAnimText().WrapToIl2Cpp());


        BackView = UnityHelper.CreateSpriteRenderer("JusticeBackView", backObj.transform, new(0f, 0f, -0.2f));
        BackView.transform.localScale = new(0.69f, 0.69f, 1f);
        BackView.sprite = meetingViewSprite.GetSprite();
        BackView.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        IEnumerator CoAnimView()
        {
            BackView.color = new(0.5f, 0.5f, 0.5f, 0.4f);

            while (true)
            {
                yield return Effects.Wait(5.5f + System.Random.Shared.NextSingle() * 3f);

                switch (System.Random.Shared.Next(3))
                {
                    case 0:
                        yield return CoAnimColor(BackView, new(1f, 0.3f, 0.3f, 0.4f), new(0.5f, 0.5f, 0.5f, 0.4f), 0.8f);
                        break;
                    case 1:
                        yield return CoAnimColor(BackView, new(0.9f, 0.3f, 0.3f, 0.4f), new(0.5f, 0.5f, 0.5f, 0.4f), 0.3f);
                        yield return Effects.Wait(0.2f);
                        yield return CoAnimColor(BackView, new(1f, 0.3f, 0.3f, 0.4f), new(0.5f, 0.5f, 0.5f, 0.4f), 1.4f);
                        break;
                    case 2:
                        yield return CoAnimColor(BackView, new(0.5f, 0.5f, 0.5f, 0.4f), new(1f, 1f, 1f, 0.6f), 0.2f);
                        yield return CoAnimColor(BackView, new(1f, 1f, 1f, 0.6f), new(0.5f, 0.5f, 0.5f, 0.4f), 0.8f);
                        break;
                }
            }
        }

        StartCoroutine(CoAnimView().WrapToIl2Cpp());


        meetingHud.playerStates.Do(p => p.gameObject.SetActive(false));

        var boardPassGame = VanillaAsset.MapAsset[2].CommonTasks.FirstOrDefault(p => p.MinigamePrefab.name == "BoardingPassGame")?.MinigamePrefab.TryCast<BoardPassGame>();

        int lastNumberIndex = 0;
        int lastAltIndex = 0;
        void SpawnVotingArea(GamePlayer player, Vector3 localPos, Image holder, int photoIndex)
        {
            var flashHolder = UnityHelper.CreateObject("FlashHolder", transform, Vector3.zero);
            var flash = UnityHelper.CreateSpriteRenderer("Flash", flashHolder.transform, localPos + new Vector3(0f, 0f, -19.5f));
            flash.sprite = votingHolderFlashSprite.GetSprite();
            var flashBlur = UnityHelper.CreateSpriteRenderer("FlashBlur", flashHolder.transform, localPos + new Vector3(0f, 0f, -19.5f));
            flashBlur.sprite = votingHolderBlurSprite.GetSprite();

            IEnumerator CoAnimFlash()
            {
                yield return ManagedEffects.Lerp(1.4f, p => {
                    flash.color = new(1f, 1f, 1f, p);
                    flashBlur.color = new(1f, 1f, 1f, p * 0.5f);
                    flashBlur.transform.localScale = new(0.6f + p * 0.1f, 0.6f + p * 0.1f, 1f);
                });
                yield return Effects.Wait(0.1f);
                yield return ManagedEffects.Lerp(0.5f, p => {
                    flash.color = new(1f, 1f, 1f, 1 - p);
                    flashBlur.color = new(1f, 1f, 1f, 0.5f - p * 0.5f);
                    flashBlur.transform.localScale = new(0.7f + p * 0.2f, 0.7f + p * 0.2f, 1f);
                });
                GameObject.Destroy(flashHolder.gameObject);
            }

            IEnumerator CoShake(float duration)
            {
                float t = duration;
                while (t > 0f)
                {
                    float p = (t / duration);
                    flashHolder.transform.localPosition = Vector3.right.RotateZ(System.Random.Shared.NextSingle() * 360f) * p * 0.16f * (System.Random.Shared.NextSingle() * 0.6f + 0.4f);

                    float ipip = (1 - p) * (1 - p);
                    float wait = 0.001f + ipip * 0.2f;
                    float lastTime = Time.time;
                    yield return Effects.Wait(wait);
                    t -= Time.time - lastTime;
                }
                flashHolder.transform.localPosition = Vector3.zero;
                yield break;
            }

            StartCoroutine(CoAnimFlash().WrapToIl2Cpp());
            StartCoroutine(CoShake(1.3f).WrapToIl2Cpp());

            IEnumerator CoSpawnPlayerArea(int photoIndex)
            {
                yield return Effects.Wait(1.3f);
                var back = UnityHelper.CreateSpriteRenderer("JusticePlayerArea", transform, localPos + new Vector3(0f, 0f, 6f));
                //back.gameObject.AddComponent<SortingGroup>();
                back.transform.localScale = new(1f, 1f, 1f);
                back.sprite = holder.GetSprite();
                back.material = HatManager.Instance.PlayerMaterial;
                PlayerMaterial.SetColors(player.PlayerId, back.material);

                var mask = UnityHelper.CreateObject<SortingGroup>("Masked", back.transform, new(0f, 0f, -0.5f));
                var maskRenderer = UnityHelper.CreateObject<SpriteMask>("Mask", mask.transform, Vector3.zero);
                maskRenderer.sprite = votingHolderMaskSprite.GetSprite();

                var playerColor = DynamicPalette.PlayerColors[player.PlayerId];
                var textColor = Color.Lerp(playerColor, DynamicPalette.IsLightColor(playerColor) ? new(0.18f, 0.18f, 0.18f, 1f) : new(1f, 1f, 1f, 1f), 0.4f);

                var graphColor = Color.Lerp(Color.Lerp(DynamicPalette.PlayerColors[player.PlayerId], DynamicPalette.ShadowColors[player.PlayerId], 0.25f), Color.white, 0.28f);
                for (int x = 0; x < 3; x++) InstantiateCircleGraph(back.transform, new(-0.6f + (0.28f * x), -0.5f, -0.2f), graphColor);
                InstantiateBandGraph(24, back.transform, new(0.65f, -0.13f, -0.2f), graphColor);

                var topText = NebulaAsset.InstantiateText("AchievementTopText", back.transform, new(0.67f, 0.87f, -0.5f), NebulaAsset.JusticeFont, 0.14f, Virial.Text.TextAlignment.Center, new(0.5f, 0.5f), "a.k.a.", Color.white.AlphaMultiplied(0.8f));
                if ((NebulaGameManager.Instance?.TryGetTitle(player.PlayerId, out var title) ?? false) && title != null)
                {
                    var textComponent = new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.MeetingTitle), title!.GetTitleComponent(null))
                    {
                        OverlayWidget = title.GetOverlayWidget(false, true, false, true, true),
                        PostBuilder = text =>
                        {
                            text.outlineWidth = 0.1f;
                            text.color = Color.white.AlphaMultiplied(0.8f);
                            text.outlineColor = Color.black;
                        }
                    };
                    var obj = textComponent.Instantiate(new(5f, 5f), out _);
                    obj.AddComponent<SortingGroup>();
                    obj!.transform.SetParent(back.transform);
                    obj.transform.localPosition = new(0.67f, 0.65f,-0.5f);
                }
                else
                {
                    //重複したテキストを回避する
                    lastAltIndex = (lastAltIndex + 1 + System.Random.Shared.Next(RandomAltTexts.Length - 1)) % RandomAltTexts.Length;
                    var achievementAltText = NebulaAsset.InstantiateText("AchievementAltText", back.transform, new(0.67f, 0.65f, -0.5f), NebulaAsset.JusticeFont, 0.27f, Virial.Text.TextAlignment.Center, new(0.5f, 0.5f), RandomAltTexts[lastAltIndex], Color.white.AlphaMultiplied(0.8f));
                }

                //重複した文字を回避する
                lastNumberIndex = (lastNumberIndex + 1 + System.Random.Shared.Next(10 - 1)) % 10;
                var numberText = UnityHelper.CreateObject<TextMeshNoS>("NumberText", mask.transform, new(-0.8f, 1.2f, -0.4f));
                numberText.Font = NebulaAsset.JusticeFont;
                numberText.FontSize = 1.2f;
                numberText.TextAlignment = Virial.Text.TextAlignment.Center;
                numberText.Pivot = new(0.5f, 0.5f);
                numberText.Text = ((char)('0' + lastNumberIndex)).ToString();
                numberText.Material = UnityHelper.GetMeshRendererMaskedMaterial();
                numberText.Color = textColor;


                //重複した画像を回避する
                var photo = UnityHelper.CreateSpriteRenderer("JusticeHolderPhoto", back.transform, PhotoData[photoIndex].localPos.AsVector3(-0.5f));
                photo.transform.localScale = Vector3.one * PhotoData[photoIndex].scale;
                photo.sprite = boardPassGame!.Photos[PhotoData[photoIndex].photoIndex];
                photo.material = HatManager.Instance.PlayerMaterial;
                PlayerMaterial.SetColors(player.PlayerId, photo.material);

                var playerState = meetingHud.playerStates.FirstOrDefault(p => p.TargetPlayerId == player.PlayerId);
                if (playerState != null)
                {
                    playerState.gameObject.SetActive(true);
                    playerState.gameObject.transform.localPosition = localPos + new Vector3(0.11f, -1.08f, -0.9f);
                    playerState.gameObject.transform.localScale = Vector3.one;
                }
            }

            StartCoroutine(CoSpawnPlayerArea(photoIndex).WrapToIl2Cpp());
        }

        SpawnVotingArea(player1, new(-2f, 0f, 0f), votingHolderLeftSprite, imageIndex1);
        SpawnVotingArea(player2, new(2f, 0f, 0f), votingHolderRightSprite, imageIndex2);


        yield break;
    }
}

[NebulaRPCHolder]
public class Justice : DefinedSingleAbilityRoleTemplate<Justice.Ability>, HasCitation, DefinedRole
{
    private Justice():base("justice", new(255, 128, 0), RoleCategory.CrewmateRole, Crewmate.MyTeam, [PutJusticeOnTheBalanceOption, JusticeMeetingTimeOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Justice.png");
    }

    Citation? HasCitation.Citation => Citations.SuperNewRoles;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.GetAsBool(1));

    static private readonly BoolConfiguration PutJusticeOnTheBalanceOption = new BoolConfigurationImpl("options.role.justice.putJusticeOnTheBalance", false);
    static public readonly FloatConfiguration JusticeMeetingTimeOption = NebulaAPI.Configurations.Configuration("options.role.justice.justiceMeetingTime", (30f,300f,15f), 60f, FloatConfigurationDecorator.Second);

    static public readonly Justice MyRole = new();

    static private readonly GameStatsEntry StatsExiled = NebulaAPI.CreateStatsEntry("stats.justice.exiled", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsNonCrewmates = NebulaAPI.CreateStatsEntry("stats.justice.nonCrewmates", GameStatsCategory.Roles, MyRole);

    static readonly RemoteProcess<(GamePlayer p1, GamePlayer p2)> RpcJusticeMeeting = new("JusticeMeeting",
        (message, _) => {
            MeetingModRpc.RpcChangeVotingStyle.LocalInvoke((0xFFFFFF, false, JusticeMeetingTimeOption, true, false));
            MeetingHudExtension.CanShowPhotos = false;
            foreach (var p in MeetingHud.Instance.playerStates) p.SetDisabled();

            var justiceMeeting = UnityHelper.CreateObject<JusticeMeetingHud>("JusticeMeeting", MeetingHud.Instance.transform, Vector3.zero);
            justiceMeeting.Begin(message.p1, message.p2, () =>
            {
                MeetingModRpc.RpcChangeVotingStyle.LocalInvoke(((1 << message.p1.PlayerId) | (1 << message.p2.PlayerId), false, Justice.JusticeMeetingTimeOption, true, false));
            });
        });

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), usedBalance.AsInt()];
        public Ability(GamePlayer player, bool isUsurped, bool usedBalance) : base(player, isUsurped) {
            this.usedBalance = usedBalance;
        }

        static private readonly RoleRPC.Definition UpdateState = RoleRPC.Get<Ability>("justice.heldMeeting", (ability, num, calledByMe) => ability.usedBalance = num == 1);

        bool usedBalance = false;
        bool isMyJusticeMeeting = false;


        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            void StartJusticeMeeting(GamePlayer p1, GamePlayer p2)
            {
                RpcJusticeMeeting.Invoke((p1,p2));

                new StaticAchievementToken("justice.common1");
                if(p1.IsImpostor || p2.IsImpostor) new StaticAchievementToken("justice.common2");
                UpdateState.RpcSync(MyPlayer, 1);
            }

            if (!usedBalance)
            {
                var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
                buttonManager?.RegisterMeetingAction(new(MeetingPlayerButtonManager.Icons.AsLoader(2),
                   p =>
                   {
                       if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                       if (PutJusticeOnTheBalanceOption)
                       {
                           if (MeetingHudExtension.CanInvokeSomeAction)
                           {
                               if (IsUsurped) NebulaAsset.PlaySE(NebulaAudioClip.ButtonBreaking, volume: 1f);
                               else StartJusticeMeeting(p.MyPlayer, MyPlayer);
                               
                               usedBalance = true;
                               
                           }
                       }
                       else
                       {
                           if (p.IsSelected)
                               p.SetSelect(false);
                           else
                           {
                               if (MeetingHudExtension.CanInvokeSomeAction)
                               {
                                   var selected = buttonManager.AllStates.FirstOrDefault(p => p.IsSelected);

                                   if (selected != null && !selected.MyPlayer.IsDead)
                                   {
                                       selected.SetSelect(false);

                                       if (IsUsurped) NebulaAsset.PlaySE(NebulaAudioClip.ButtonBreaking, volume: 1f);
                                       else StartJusticeMeeting(p.MyPlayer, selected.MyPlayer);

                                       usedBalance = true;
                                   }
                                   else
                                   {
                                       selected?.SetSelect(false);
                                       p.SetSelect(true);
                                   }
                               }
                           }
                       }
                   },
                   p => !usedBalance && !p.MyPlayer.IsDead && !MeetingHudExtension.ExileEvenIfTie && !MyPlayer.IsDead && (!p.MyPlayer.AmOwner || !PutJusticeOnTheBalanceOption)
                   ));
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (isMyJusticeMeeting)
            {
                if (ev.Exiled.Any(e => e.AmOwner)) new StaticAchievementToken("justice.another1");
                if(ev.Exiled.Count() == 2)
                {
                    new StaticAchievementToken("justice.common3");
                    if(ev.Exiled.All(e => e.IsImpostor)) new StaticAchievementToken("justice.challenge");
                }

                StatsExiled.Progress(ev.Exiled.Count());
                StatsNonCrewmates.Progress(ev.Exiled.Count(p => !p.IsCrewmate));

                isMyJusticeMeeting = false;
            }
        }
    }
}