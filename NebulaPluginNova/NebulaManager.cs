using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Xml.Schema;
using Nebula.Behavior;
using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Nebula.VisualProgramming;
using Nebula.VisualProgramming.UI;
using System.Data;
using TMPro;
using UnityEngine.Rendering;
using Virial;
using Virial.Helpers;
using Virial.Media;
using Virial.Runtime;
using Virial.VisualProgramming;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;
using static Nebula.Modules.INebulaAchievement;
using static Nebula.Modules.Cosmetics.RingMenu;
using static Nebula.Roles.Neutral.Spectre;
using Virial.Events.Game;
using static Il2CppSystem.Uri;
using Nebula.Patches;
using Nebula.Roles.Crewmate;

namespace Nebula;

public class MouseOverPopupParameters
{
    public PassiveUiElement? RelatedButton { get; set; } = null;
    public Func<bool>? RelatedPredicate { get; set; } = null;
    public Func<Vector2>? RelatedPosition { get; set; } = null;
    public Func<float>? RelatedValue { get; set; } = null;
    public bool CanPileCursor { get; set; } = false;
    public Action? OnClick { get; set; } = null;
    public Image? Icon { get; set; } = null;
}

public class MouseOverPopup : MonoBehaviour
{
    private MetaScreen myScreen = null!;
    private SpriteRenderer background = null!;
    private SpriteRenderer icon = null!;
    private SpriteRenderer valueViewer = null!;
    private Vector2 screenSize;
    public MouseOverPopupParameters Parameters { get; set; } = new MouseOverPopupParameters();
    private SpriteMask mask = null!;
    public bool Piled { get; private set; }
    private BoxCollider2D Collider { get; set; }
    public PassiveUiElement? RelatedObject => Parameters.RelatedButton;

    private Virial.Compat.Size lastSize = new(0f, 0f);
    private bool followMouseCursor = false;
    static MouseOverPopup()
    {
        ClassInjector.RegisterTypeInIl2Cpp<MouseOverPopup>();
    }

    public void Awake()
    {
        background = UnityHelper.CreateObject<SpriteRenderer>("Background", transform, Vector3.zero, LayerExpansion.GetUILayer());
        background.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
        background.drawMode = SpriteDrawMode.Sliced;
        background.tileMode = SpriteTileMode.Continuous;
        background.color = new Color(0.14f, 0.14f, 0.14f, 1f);

        var button = background.gameObject.SetUpButton(false);
        button.OnMouseOver.AddListener(() => Piled = true);
        button.OnMouseOut.AddListener(() => Piled = false);
        button.OnClick.AddListener(() => Parameters.OnClick?.Invoke());
        Collider = button.gameObject.AddComponent<BoxCollider2D>();
        this.Collider.isTrigger = true;

        valueViewer = UnityHelper.CreateObject<SpriteRenderer>("Value", transform, new(0f,0f,-1f), LayerExpansion.GetUILayer());
        valueViewer.sprite = VanillaAsset.FullScreenSprite;
        valueViewer.gameObject.SetActive(false);
        valueViewer.transform.localScale = new(1f, 0.03f);

        icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", transform, new(0f, 0f, -1f), LayerExpansion.GetUILayer());
        icon.gameObject.SetActive(false);

        var group = UnityHelper.CreateObject<SortingGroup>("Group", transform, Vector3.zero);
        mask = UnityHelper.CreateObject<SpriteMask>("Mask", group.transform, Vector3.zero);
        mask.sprite = VanillaAsset.FullScreenSprite;
        mask.transform.localScale = new Vector3(1f, 1f);
        
        screenSize = new Vector2(7f, 4f);
        myScreen = MetaScreen.GenerateScreen(screenSize,group.transform,Vector3.zero,false,false,false);

        gameObject.SetActive(false);

        NebulaGameManager.Instance?.OnSceneChanged();
    }

    public void Irrelevantize()
    {
        Parameters = new();
    }

    public void SetWidgetOld(PassiveUiElement? related, IMetaWidgetOld? widget)
    {
        myScreen.SetWidget(null);

        followMouseCursor = false;
        Parameters = new();

        if (widget == null) {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        Parameters.RelatedButton = related;
        transform.SetParent(UnityHelper.FindCamera(LayerExpansion.GetUILayer())!.transform);

        bool isLeft = Input.mousePosition.x < Screen.width / 2f;
        bool isLower = Input.mousePosition.y < Screen.height / 2f;

        float height = myScreen.SetWidget(widget, out var width);

        if (width.min > width.max)
        {
            gameObject.SetActive(false);
            return;
        }

        float[] xRange = new float[2], yRange = new float[2];
        xRange[0] = -screenSize.x / 2f - 0.15f;
        yRange[1] = screenSize.y / 2f + 0.15f;
        xRange[1] = xRange[0] + (width.max - width.min) + 0.3f;
        yRange[0] = yRange[1] - height - 0.3f;

        Vector2 anchorPoint = new(xRange[isLeft ? 0 : 1], yRange[isLower ? 0 : 1]);

        var pos = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer());
        pos.z = -800f;
        transform.position = pos - (Vector3)anchorPoint;

        //範囲外にはみ出た表示の是正
        {
            var lower = UnityHelper.ScreenToWorldPoint(new(10f, 10f), LayerExpansion.GetUILayer());
            var upper = UnityHelper.ScreenToWorldPoint(new(Screen.width - 10f, Screen.height - 10f), LayerExpansion.GetUILayer());
            float diff;

            diff = (transform.position.x + xRange[0]) - lower.x;
            if (diff < 0f) transform.position -= new Vector3(diff, 0f);

            diff = (transform.position.y + yRange[0]) - lower.y;
            if (diff < 0f) transform.position -= new Vector3(0f, diff);

            diff = (transform.position.x + xRange[1]) - upper.x;
            if (diff > 0f) transform.position -= new Vector3(diff, 0f);

            diff = (transform.position.y + yRange[1]) - upper.y;
            if (diff > 0f) transform.position -= new Vector3(0f, diff);
        }

        UpdateArea(new((width.min + width.max) / 2f, screenSize.y / 2f - height / 2f), new((width.max - width.min) + 0.22f, height + 0.1f));
        Update();
    }

    void UpdateArea(Vector2 localPos, Vector2 localScale)
    {
        Vector3 localPos3 = localPos;
        localPos3.z = 1f;

        background.transform.localPosition = localPos3;
        background.size = localScale;

        this.Collider.size = localScale;

        mask.transform.localPosition = localPos;
        mask.transform.localScale = localScale;
    }

    public void SetWidget(PassiveUiElement? related, Virial.Media.GUIWidget? widget, bool followMouseCursor = false)
    {
        myScreen.SetWidget(null);

        Parameters = new();

        if (widget == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        Parameters.RelatedButton = related;
        transform.SetParent(UnityHelper.FindCamera(LayerExpansion.GetUILayer())!.transform);

        myScreen.SetWidget(widget, new Vector2(0.5f, 0.5f), out var size);

        this.followMouseCursor = followMouseCursor;
        this.lastSize = size;
        var scale = new Vector2(lastSize.Width + 0.22f, lastSize.Height + 0.1f);

        var imagePos = scale * 0.5f - new Vector2(0.55f,0.55f);
        var imageScale = 0.38f;
        if (imagePos.y < 0f)
        {
            imageScale = Math.Max(0.15f, imageScale + (imagePos.y * 0.6f));
            imagePos.y = 0f;
        }
        else imagePos.y *= -1f;
        myScreen.SetBackImage(widget.BackImage,
            widget.GrayoutedBackImage ? 0.03f : 0.6f,
            widget.GrayoutedBackImage ? 0.46f : 0.4f, 
            imagePos, scale, imageScale, widget.GrayoutedBackImage);


        UpdateArea(new(0f, 0f), scale);
        UpdatePosition();

        Update();
    }

    public void UpdatePosition(Vector2? screenPosition = null, bool smoothedCenter = false) {
        Vector2 screenPos = screenPosition ?? Input.mousePosition;
        bool isLeft = screenPos.x < Screen.width / 2f;
        bool isLower = screenPos.y < Screen.height / 2f;

        float smoothX = 0f;
        float smoothY = 0f;
        if (smoothedCenter)
        {
            float xCenter = (Screen.width / 2f - screenPos.x) / 200f;
            float yCenter = (Screen.height / 2f - screenPos.y) / 200f;
            smoothX = Math.Max(0f, 1f - Math.Abs(xCenter));
            smoothY = Math.Max(0f, 1f - Math.Abs(yCenter));
        }

        float[] xRange = [
            -lastSize.Width * 0.5f - 0.15f,
            lastSize.Width * 0.5f + 0.15f
            ];
        float[] yRange = [
            -lastSize.Height * 0.5f - 0.15f,
            lastSize.Height * 0.5f + 0.15f
            ];

        Vector2 anchorPoint = new(Mathf.Lerp(xRange[isLeft ? 0 : 1], 0f, smoothX), Mathf.Lerp(yRange[isLower ? 0 : 1], 0f, smoothY));

        var pos = UnityHelper.ScreenToWorldPoint(screenPos, LayerExpansion.GetUILayer());
        pos.z = -800f;
        transform.position = pos - (Vector3)anchorPoint;

        //範囲外にはみ出た表示の是正
        {
            var lower = UnityHelper.ScreenToWorldPoint(new(10f, 10f), LayerExpansion.GetUILayer());
            var upper = UnityHelper.ScreenToWorldPoint(new(Screen.width - 10f, Screen.height - 10f), LayerExpansion.GetUILayer());
            float diff;

            diff = (transform.position.x + xRange[0]) - lower.x;
            if (diff < 0f) transform.position -= new Vector3(diff, 0f);

            diff = (transform.position.y + yRange[0]) - lower.y;
            if (diff < 0f) transform.position -= new Vector3(0f, diff);

            diff = (transform.position.x + xRange[1]) - upper.x;
            if (diff > 0f) transform.position -= new Vector3(diff, 0f);

            diff = (transform.position.y + yRange[1]) - upper.y;
            if (diff > 0f) transform.position -= new Vector3(0f, diff);
        }
    }

    public void SetRelatedPredicate(Func<bool> predicate)
    {
        this.Parameters.RelatedPredicate = predicate;
    }
    public void SetRelatedPosition(Func<Vector2> position)
    {
        this.Parameters.RelatedPosition = position;
    }

    public void SetRelatedValue(Func<float> value)
    {
        this.Parameters.RelatedValue = value;
    }

    public void Update()
    {
        if((Parameters.RelatedButton is not null && !Parameters.RelatedButton) || !(Parameters.RelatedPredicate?.Invoke() ?? true))
        {
            SetWidget(null, null);
        }

        if (followMouseCursor)
            UpdatePosition(Parameters.RelatedPosition?.Invoke());
        else if(Parameters.RelatedPosition != null)
            UpdatePosition(Parameters.RelatedPosition?.Invoke(), true);

        valueViewer.gameObject.SetActive(Parameters.RelatedValue != null);
        if (Parameters.RelatedValue != null)
        {
            float value = Math.Clamp(Parameters.RelatedValue.Invoke(), 0f, 1f);
            Vector3 valuePos = background.transform.localPosition;
            valuePos.z = -1f;
            valuePos.y -= (background.bounds.size.y * 0.5f);
            valuePos.x += (background.bounds.size.x * (value - 1f) * 0.5f);
            valueViewer.transform.localPosition = valuePos;
            valueViewer.transform.localScale = new(value * background.bounds.size.x, 0.03f, 1f);
        }

        icon.gameObject.SetActive(Parameters.Icon != null);
        if(Parameters.Icon != null)
        {
            Vector3 valuePos = background.transform.localPosition;
            valuePos.z = -1f;
            valuePos.y += (background.bounds.size.y * 0.5f);
            valuePos.x -= (background.bounds.size.x * 0.5f);
            icon.transform.localPosition = valuePos;
            icon.sprite = Parameters.Icon.GetSprite();
        }

        Collider.enabled = Parameters.CanPileCursor;
    }

    public bool ShowAnyOverlay => gameObject.activeSelf;
    public bool ShowUnrelatedOverlay => ShowAnyOverlay && !RelatedObject;
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class NebulaManager : MonoBehaviour
{
    private record PopupProvider(Func<bool> isFinallyDead, Func<bool> predicate, Func<(GUIWidget widget, Func<Vector2> screenPosition, Action? callBack)> supplier, Func<float>? gauge = null, Image? relatedIcon = null);
    public class MetaCommand
    {
        public Virial.Compat.VirtualKeyInput? KeyAssignmentType = null;
        public VirtualInput? DefaultKeyInput = null;
        public VirtualInput? KeyInput => KeyAssignmentType != null ? NebulaInput.GetInput(KeyAssignmentType.Value) : DefaultKeyInput;
        public string TranslationKey;
        public Func<bool> Predicate;
        public Action CommandAction;
        
        public MetaCommand(string translationKey, Func<bool> predicate,Action commandAction)
        {
            TranslationKey = translationKey;
            Predicate = predicate;
            CommandAction = commandAction;
        }
    }
    List<PopupProvider> PopupProviders = null!;
    private List<Tuple<GameObject, PassiveButton?>> allModUi = [];
    static private List<MetaCommand> commands = [];
    static public NebulaManager Instance { get; private set; } = null!;

    public DebugScreen DebugScreen => debugScreen;

    //テキスト情報表示
    private MouseOverPopup mouseOverPopup = null!;
    internal MouseOverPopup MouseOverPopup => mouseOverPopup;
    private DebugScreen debugScreen = null!;

    //コンソール
    private CommandConsole? console = null;

    //リングメニュー
    private RingMenu ringMenu = new();

    static NebulaManager()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaManager>();
    }

    static private RemoteProcess RpcResetGameStart = new(
        "ResetStarting",
        (_) =>
        {
            if(GameStartManager.Instance) GameStartManager.Instance.ResetStartState();
        }
        );
    static public void Preprocess(NebulaPreprocessor preprocess)
    {
        commands.Add(new( "help.command.nogame",
            () => NebulaGameManager.Instance != null && AmongUsClient.Instance &&  AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
            () => NebulaGameManager.Instance?.RpcInvokeForcelyWin(NebulaGameEnd.NoGame, 0)
        ){ DefaultKeyInput = new(KeyCode.F5) });
        
        commands.Add(new("help.command.quickStart",
            () => NebulaGameManager.Instance != null && AmongUsClient.Instance && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined && GameStartManager.Instance && GameStartManager.Instance.startState == GameStartManager.StartingStates.NotStarting,
            () =>
            {
                if (GameStartManager.Instance.StartButton.enabled)
                {
                    GameStartManager.Instance.startState = GameStartManager.StartingStates.Countdown;
                    GameStartManager.Instance.FinallyBegin();
                }
                else
                {
                    DebugScreen.Push(Language.Translate("ui.error.quickStart.cannotStart"), 3f);
                }
            }
        )
        { DefaultKeyInput = new(KeyCode.F1) });
        
        commands.Add(new("help.command.cancelStarting",
            () => NebulaGameManager.Instance != null && AmongUsClient.Instance && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined && GameStartManager.Instance && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown,
            () => RpcResetGameStart.Invoke()
        )
        { DefaultKeyInput = new(KeyCode.F2) });

        commands.Add(new("help.command.console",
            () => true,
            ()=>NebulaManager.Instance.ToggleConsole()
        )
        { DefaultKeyInput = new(KeyCode.Return) });

        commands.Add(new("help.command.saveResult",
            () => LastGameHistory.LastWidget != null,
            () =>
            {
                LastGameHistory.SaveResult(GetPicturePath(out string displayPath));
                /*
                var window = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true, true);
                window.SetWidget(new MetaWidgetOld(LastGameHistory.LastWidget, new MetaWidgetOld.VerticalMargin(0.15f), new MetaWidgetOld.Button(() =>
                {
                    LastGameHistory.SaveResult(GetPicturePath(out string displayPath));
                    window.CloseScreen();
                }, new(Utilities.TextAttributeOld.NormalAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                { TranslationKey = "help.command.showResult.save", Alignment = IMetaWidgetOld.AlignmentOption.Center })
                );
                */
            }
        )
        { DefaultKeyInput = new(KeyCode.F3) });

        commands.Add(new("help.command.toggleSocial",
            () => AmongUsClient.Instance && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && AmongUsClient.Instance.AmHost && (ModSingleton<ShowUp>.Instance?.ShouldBeShownSocialSettings ?? false),
            () => ShowUp.RpcShareSocialSettings.Invoke((false, !ModSingleton<ShowUp>.Instance.CanAppealInGame, ModSingleton<ShowUp>.Instance.CanUseStamps, ModSingleton<ShowUp>.Instance.CanUseEmotes))
        )
        { DefaultKeyInput = new(KeyCode.F4) });


    }

    public void ToggleConsole()
    {
        if (console == null) console = new CommandConsole();
        else console.IsShown = !console.IsShown;

        if (console.IsShown) console.GainFocus();
    }

    public void CloseAllUI()
    {
        foreach (var ui in allModUi) GameObject.Destroy(ui.Item1);
        allModUi.Clear();
    }

    public void RegisterUI(GameObject uiObj,PassiveButton? closeButton)
    {
        allModUi.Add(new Tuple<GameObject, PassiveButton?>(uiObj,closeButton));
    }

    public bool HasSomeUI => allModUi.Count > 0;

    static private string GetCurrentTimeString()
    {
        return DateTime.Now.ToString("yyyyMMddHHmmss");
    }

    static public string GetPicturePath(out string displayPath)
    {
        string dir = "Screenshots";
        displayPath = "ScreenShots";
        string currentTime = GetCurrentTimeString();
        displayPath += "\\" + currentTime + ".png";
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        return dir + "\\" + currentTime + ".png";
    }

    static public IEnumerator CaptureAndSave()
    {
        yield return new WaitForEndOfFrame();

        var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply(false, false);

        File.WriteAllBytesAsync(GetPicturePath(out _), tex.EncodeToPNG());

        DevTeamContact.PushScreenshot(tex);
    }

    internal void ShowRingMenu(RingMenuElement[] elements, Func<bool> showWhile, Action? ifEmpty)
    {
        if (elements.Length == 0)
            ifEmpty?.Invoke();
        else
            ringMenu.Show(elements, showWhile);
    }

    public void Update()
    {
        if (PreloadManager.FinishedPreload)
        {
            //スクリーンショット
            if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Screenshot).KeyDownForAction) StartCoroutine(CaptureAndSave().WrapToIl2Cpp());

            if(DebugTools.DebugMode && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.RightShift))
            {
                var window = MetaScreen.GenerateWindow(new(8f, 4.7f), HudManager.InstanceExists ? HudManager.Instance.transform : null, new Vector3(0f, 0f, -400f), true, true, false, BackgroundSetting.Modern);
                window.SetWidget(new Nebula.Modules.GUIWidget.GUIScrollView(GUIAlignment.Center, new(7.8f, 4.5f), DebugTools.GetEditorWidget()), out _);
            }

            if (AmongUsClient.Instance && AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                var stampInput = NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Stamp);
                if (stampInput.KeyDownForAction)
                {
                    StampHelpers.TryShowStampRingMenu(() => !stampInput.KeyUp);
                }

                if (Input.GetKeyDown(KeyCode.K))
                {
                    /*
                    var player1 = GamePlayer.GetPlayer(0)!;
                    var player2 = GamePlayer.GetPlayer(1)!;

                    int length = 0, begin = 0;
                    int max = 30;
                    Vector2[] pos1 = new Vector2[max];
                    Vector2[] pos2 = new Vector2[max];

                    (var renderer, var filter) = UnityHelper.CreateMeshRenderer("MeshRenderer", null, new(0f, 0f, -10f), LayerExpansion.GetDefaultLayer(), null, UnityHelper.GetMeshRendererMaterial());
                    renderer.material.mainTexture = SpriteLoader.FromResource("Nebula.Resources.Gradation.png", 100f).GetSprite().texture;
                    var mesh = filter.mesh;

                    Vector3[] pos = new Vector3[max * 2];
                    mesh.SetVertices(pos);

                    var color = new Color32(255, 255, 255, 255);
                    Color32[] colors = new Color32[max * 2];
                    for (int i = 0; i < max * 2; i++) colors[i] = color;
                    mesh.SetColors(colors);

                    Vector2[] uvs = new Vector2[max * 2];
                    for(int i = 0; i < max; i++)
                    {
                        uvs[i] = new((float)i / (max - 1) * 0.95f, 0);
                        uvs[i + max] = new((float)i / (max - 1) * 0.95f, 1);
                    }
                    mesh.SetUVs(0, uvs);

                    

                    GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
                    {
                        Vector3 center = HudManager.Instance.transform.position;
                        center.z = -100f;
                        renderer.transform.position = center;
                        center.z = 0f;

                        if (length < max) length++; else begin = (begin + 1) % max;
                        int lastIndex = (begin + length - 1) % max;
                        pos1[lastIndex] = player1.Position.ToUnityVector();
                        pos2[lastIndex] = player2.Position.ToUnityVector();

                        int offset = max - length;
                        for(int i = 0; i < length; i++)
                        {
                            pos[offset + i] = (Vector3)pos1[(begin + i) % max] - center;
                            pos[offset + i + max] = (Vector3)pos2[(begin + i) % max] - center;
                        }

                        mesh.SetVertices(pos);
                        List<int> triangleList = [];

                        Vector2 temp1, temp2, temp3, dir;
                        float cross;
                        for (int i =0;i<length - 1; i++)
                        {
                            temp1 = pos2[(begin + i) % max];
                            temp2 = pos2[(begin + i + 1) % max];
                            temp3 = pos1[(begin + i) % max];
                            dir = temp2 - temp1;
                            cross = dir.x * temp3.y - dir.y * temp3.x;
                            if(cross > 0f) triangleList.AddRange([offset + i + max, offset + i, offset + i + 1 + max]);
                            else triangleList.AddRange([offset + i + max, offset + i + 1 + max, offset + i]);

                            temp1 = pos1[(begin + i) % max];
                            temp2 = pos1[(begin + i + 1) % max];
                            temp3 = pos2[(begin + i + 1) % max];
                            dir = temp2 - temp1;
                            cross = dir.x * temp3.y - dir.y * temp3.x;
                            if (cross > 0f) triangleList.AddRange([offset + i, offset + i + 1, offset + i + 1 + max]);
                            else triangleList.AddRange([offset + i, offset + i + 1 + max, offset + i + 1]);
                        }

                        
                        mesh.SetTriangles(triangleList.ToArray(), 0);
                    }, NebulaAPI.CurrentGame);
                    */




                    //new FunctionBlock(HudManager.Instance.transform, new(0f,0f,-100f));

                    /*
                    var generator = new EdgeGenerator(new(-1f, 0f), [new(2f, 2f), new(2f, 1f), new(2f, 0f), new(2f, -1f), new(2f, -2f)]);
                    StartCoroutine(ManagedEffects.Wait(() =>
                    {
                        generator.Update();
                        return generator.IsActive;
                    }, () => { }).WrapToIl2Cpp());
                    */

                    /*
                    SerializedCircuit circuit = new();
                    circuit.Inputs = [0, 1];
                    circuit.Outputs = [2];
                    circuit.Nodes = [
                        new(){ Inputs = [new(){ Id = 0}, new() { Id = 1}], Outputs = [3], Operation ="__add"},
                        new(){ Inputs = [new(){ Id = 3}, new() { Id = 4}], Outputs = [2], Operation ="__mul"},
                        new(){ Inputs = [new(){ Value = "値を入力してください。"}], Outputs = [4], Operation ="read"}
                        ];
                    var generated = circuit.GenerateCircuit(NodesCollection.Default);
                    var output = generated.GetOutput(0);

                    var env1 = generated.GenerateInstance([new VPFloat(4f), new VPFloat(5f)]);
                    var coroutine = output.PrepareIfNeeded(env1).AsStackfullCoroutine();
                    StartCoroutine(ManagedEffects.Sequence(coroutine.AsEnumerator(), ManagedEffects.Action(() =>
                    {
                        LogUtils.WriteToConsole(output.Get(env1).GetString());
                    })).WrapToIl2Cpp());
                    */

                    /*
                    if (MoreCosmic.AllStamps.Count > 0)
                    {
                        StampHelpers.SpawnStamp(0, MoreCosmic.AllStamps.FirstOrDefault().Value, MeetingBack.GetSprite(), new(-0.61f, 0.09f), HudManager.Instance.transform, Vector3.zero);
                    }
                    else
                    {
                        Debug.Log("no stamp!");
                    }
                    */

                    //Vector2 center = ShipStatus.Instance.MapPrefab.HerePoint.transform.parent.localPosition * -1f * ShipStatus.Instance.MapScale;
                    //File.WriteAllBytesAsync("SpawnableMap" + NebulaPreSpawnLocation.MapName[AmongUsUtil.CurrentMapId] +".png", MapData.GetCurrentMapData().OutputMap(center, new Vector2(10f, 7f) * ShipStatus.Instance.MapScale, 40f).EncodeToPNG());
                }

                //コマンド
                if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Command).KeyDownForAction)
                {
                    MetaWidgetOld widget = new();
                    widget.Append(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left }) { TranslationKey = "help.command", Alignment = IMetaWidgetOld.AlignmentOption.Left });
                    string commandsStr = "";
                    foreach (var command in commands)
                    {
                        if (!command.Predicate.Invoke()) continue;

                        if (commandsStr.Length != 0) commandsStr += "\n";
                        commandsStr += ButtonEffect.KeyCodeInfo.GetKeyDisplayName(command.KeyInput!.TypicalKey);

                        commandsStr += " :" + Language.Translate(command.TranslationKey);
                    }
                    widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { RawText = commandsStr });

                    if (commandsStr.Length > 0) SetHelpWidget(null, widget);

                }

                //コマンド
                if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Command).KeyUp)
                {
                    if (HelpRelatedObject == null) HideHelpWidget();
                }

                //コマンド
                if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Command).KeyState)
                {
                    foreach (var command in commands)
                    {
                        if (!command.Predicate.Invoke()) continue;

                        if (!command.KeyInput!.KeyDown) continue;

                        command.CommandAction.Invoke();
                        HideHelpWidget();
                        break;
                    }
                }
            }
        }

        //ダイアログ管理
        allModUi.RemoveAll(tuple => !tuple.Item1);
        for (int i = 0; i < allModUi.Count; i++)
        {
            var lPos = allModUi[i].Item1.transform.localPosition;
            allModUi[i].Item1.transform.localPosition = new Vector3(lPos.x, lPos.y, -750f - i * 30f);
            allModUi[i].Item2?.gameObject.SetActive(i == allModUi.Count - 1);
        }

        if (allModUi.Count > 0 && Input.GetKeyDown(KeyCode.Escape))
            allModUi[^1].Item2?.OnClick.Invoke();

        //静的に表示されるオーバーレイ
        PopupProviders.RemoveAll(p => p.isFinallyDead());
        if (!mouseOverPopup.ShowAnyOverlay && PopupProviders.Find(popup => popup.predicate.Invoke(), out var found))
        {
            var parameters = found.supplier.Invoke();
            SetHelpWidget(null, parameters.widget);
            mouseOverPopup.SetRelatedPredicate(found.predicate);
            mouseOverPopup.SetRelatedPosition(parameters.screenPosition);
            mouseOverPopup.Parameters.Icon = found.relatedIcon;
            mouseOverPopup.Parameters.RelatedValue = found.gauge;
            parameters.callBack?.Invoke();
            mouseOverPopup.UpdatePosition(parameters.screenPosition.Invoke(), true);
        }

        MoreCosmic.Update();
        ringMenu.Update();
    }

    public void Awake()
    {
        Instance = this;
        gameObject.layer = LayerExpansion.GetUILayer();

        mouseOverPopup = UnityHelper.CreateObject<MouseOverPopup>("MouseOverPopup",transform,Vector3.zero);
        debugScreen = new DebugScreen(transform);

        PopupProviders = [];
    }

    public void LateUpdate()
    {
        NebulaGameManager.Instance?.OnLateUpdate();
    }

    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="isFinallyDead">この表示が完全に不要になったらtrueを返してください。</param>
    /// <param name="predicate">オーバーレイが表示される間はtrueを返してください。</param>
    /// <param name="supplier">表示されるGUIと表示位置(スクリーン座標)の関数、表示時に実行されるコールバックのタプル</param>
    public void RegisterStaticPopup(Func<bool> isFinallyDead, Func<bool> predicate, Func<(GUIWidget widget, Func<Vector2> screenPosition, Action? callback)> supplier, Func<float>? gauge = null, Image? relatedIcon = null)
    {
        PopupProviders.Add(new(isFinallyDead, predicate, supplier, gauge, relatedIcon));
    }

    public void StartDelayAction(float delay, Action action)
    {
        StartCoroutine(Effects.Sequence(Effects.Wait(delay), Effects.Action(action)));
    }
    public void SetHelpWidget(PassiveUiElement? related, IMetaWidgetOld? widget) => mouseOverPopup.SetWidgetOld(related, widget);
    public void SetHelpWidget(PassiveUiElement? related, Virial.Media.GUIWidget? widget, bool followMouseCursor = false) => mouseOverPopup.SetWidget(related, widget, followMouseCursor);
    public void SetHelpWidget(PassiveUiElement? related, string? rawText)
    {
        if (rawText != null)
        {
            SetHelpWidget(related, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, RawText = rawText });
        }
    }

    public void HideHelpWidget() => mouseOverPopup.SetWidget(null, null);
    public void HideHelpWidgetIf(PassiveUiElement? related)
    {
        if(HelpRelatedObject == related) mouseOverPopup.SetWidget(null, null);
    }
    public PassiveUiElement? HelpRelatedObject => mouseOverPopup.RelatedObject;
    public bool ShowingAnyHelpContent => mouseOverPopup.isActiveAndEnabled;
    public void HelpIrrelevantize() => mouseOverPopup.Irrelevantize();

    public Coroutine ScheduleDelayAction(Action action)
    {
        return StartCoroutine(Effects.Sequence(
            Effects.Action((Il2CppSystem.Action)(() => { })),
            Effects.Action(action)
            ));
    }
}