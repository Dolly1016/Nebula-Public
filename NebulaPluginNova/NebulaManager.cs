using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Map;
using Nebula.Roles.Assignment;
using UnityEngine.Rendering;
using Virial.Runtime;

namespace Nebula;

public class MouseOverPopup : MonoBehaviour
{
    private MetaScreen myScreen = null!;
    private SpriteRenderer background = null!;
    private Vector2 screenSize;
    private PassiveUiElement? relatedButton;
    private SpriteMask mask = null!;
    public PassiveUiElement? RelatedObject => relatedButton;
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
        relatedButton = null;
    }

    public void SetWidgetOld(PassiveUiElement? related, IMetaWidgetOld? widget)
    {
        myScreen.SetWidget(null);

        if (widget == null) {
            gameObject.SetActive(false);
            relatedButton = null;
            return;
        }

        gameObject.SetActive(true);

        relatedButton = related;
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

        mask.transform.localPosition = localPos;
        mask.transform.localScale = localScale;
    }

    public void SetWidget(PassiveUiElement? related, Virial.Media.GUIWidget? widget)
    {
        myScreen.SetWidget(null);

        if (widget == null)
        {
            gameObject.SetActive(false);
            relatedButton = null;
            return;
        }

        gameObject.SetActive(true);

        relatedButton = related;
        transform.SetParent(UnityHelper.FindCamera(LayerExpansion.GetUILayer())!.transform);

        bool isLeft = Input.mousePosition.x < Screen.width / 2f;
        bool isLower = Input.mousePosition.y < Screen.height / 2f;

        myScreen.SetWidget(widget, new Vector2(0.5f, 0.5f), out var size);

        

        float[] xRange = new float[2], yRange = new float[2];
        xRange[0] = -size.Width * 0.5f - 0.15f;
        xRange[1] = size.Width * 0.5f + 0.15f;
        yRange[0] = -size.Height * 0.5f - 0.15f;
        yRange[1] = size.Height * 0.5f + 0.15f;

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

        UpdateArea(new(0f,0f),new(size.Width + 0.22f, size.Height + 0.1f));
        Update();
    }

    public void Update()
    {
        if(relatedButton is not null && !relatedButton)
        {
            SetWidget(null, null);
        }

    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class NebulaManager : MonoBehaviour
{
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

    private List<Tuple<GameObject, PassiveButton?>> allModUi = new();
    static private List<MetaCommand> commands = new();
    static public NebulaManager Instance { get; private set; } = null!;

    //テキスト情報表示
    private MouseOverPopup mouseOverPopup = null!;

    //コンソール
    private CommandConsole? console = null;

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
                GameStartManager.Instance.startState = GameStartManager.StartingStates.Countdown;
                GameStartManager.Instance.FinallyBegin();
            }
        )
        { DefaultKeyInput = new(KeyCode.F1) });
        
        commands.Add(new("help.command.cancelStarting",
            () => NebulaGameManager.Instance != null && AmongUsClient.Instance && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined && GameStartManager.Instance && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown,
            RpcResetGameStart.Invoke
        )
        { DefaultKeyInput = new(KeyCode.F2) });

        commands.Add(new("help.command.console",
            () => true,
            ()=>NebulaManager.Instance.ToggleConsole()
        )
        { DefaultKeyInput = new(KeyCode.Return) });

        commands.Add(new("help.command.saveResult",
            () => LastGameHistory.LastWidget != null,
            () => LastGameHistory.SaveResult(GetPicturePath(out string displayPath))
        )
        { DefaultKeyInput = new(KeyCode.F3) });


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
        tex.Apply();

        File.WriteAllBytesAsync(GetPicturePath(out string displayPath), tex.EncodeToPNG());
    }

    
    public void Update()
    {
        if (PreloadManager.FinishedPreload)
        {

            //スクリーンショット
            if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Screenshot).KeyDownForAction) StartCoroutine(CaptureAndSave().WrapToIl2Cpp());

            if (AmongUsClient.Instance && AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.NotJoined)
            {
                
                if (Input.GetKeyDown(KeyCode.K))
                {
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
            allModUi[i].Item1.transform.localPosition = new Vector3(lPos.x, lPos.y, -500f - i * 50f);
            allModUi[i].Item2?.gameObject.SetActive(i == allModUi.Count - 1);
        }

        if (allModUi.Count > 0 && Input.GetKeyDown(KeyCode.Escape))
            allModUi[allModUi.Count - 1].Item2?.OnClick.Invoke();

        MoreCosmic.Update();
    }
    public void LateUpdate()
    {
        NebulaGameManager.Instance?.LateUpdate();
    }

    public void Awake()
    {
        Instance = this;
        gameObject.layer = LayerExpansion.GetUILayer();

        mouseOverPopup = UnityHelper.CreateObject<MouseOverPopup>("MouseOverPopup",transform,Vector3.zero);
    }

    public void StartDelayAction(float delay, Action action)
    {
        StartCoroutine(Effects.Sequence(Effects.Wait(delay), Effects.Action(action)));
    }
    public void SetHelpWidget(PassiveUiElement? related, IMetaWidgetOld? widget) => mouseOverPopup.SetWidgetOld(related, widget);
    public void SetHelpWidget(PassiveUiElement? related, Virial.Media.GUIWidget? widget) => mouseOverPopup.SetWidget(related, widget);
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