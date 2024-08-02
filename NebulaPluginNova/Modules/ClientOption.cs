using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Rewired.UI.ControlMapper;
using System.Diagnostics;
using TMPro;
using Virial.Runtime;

namespace Nebula.Modules;


[NebulaPreprocess(PreprocessPhase.LoadAddons)]
public class ClientOption
{
    public enum ClientOptionType
    {
        OutputCosmicHash,
        UseNoiseReduction,
        ProcessorAffinity,
        ForceSkeldMeetingSE,
        SpoilerAfterDeath,
        PlayLobbyMusic,
        ButtonArrangement,
        ShowNoSLogoInLobby,
        ShowOnlySpawnableAssignableOnFilter
    }

    static private DataSaver ClientOptionSaver = new("ClientOption");
    static public BooleanDataEntry UseSimpleConfigurationViewerEntry = new("useSimpleConfig", ClientOptionSaver, false);
    static public Dictionary<ClientOptionType,ClientOption> AllOptions = new();
    DataEntry<int> configEntry;
    string id;
    string[] selections;
    ClientOptionType type;

    private static string DiscordWebhookPrefix = "https://discord.com/api/webhooks/";
    public record DiscordWebhookOption(StringDataEntry urlEntry, BooleanDataEntry autoSendEntry) { 
        public string url => DiscordWebhookPrefix + urlEntry.Value; 
        public string urlShorten => urlEntry.Value.Length == 0 ? "    -    ".Color(Color.gray) : urlEntry.Value.Substring(0,3) + " ... " + urlEntry.Value.Substring(urlEntry.Value.Length - 4);

    };
    static public DiscordWebhookOption WebhookOption { get; private set; } = new(new("discordUrl", ClientOptionSaver, ""), new("discordAutoSend", ClientOptionSaver, false));

    

    static public void ShowWebhookSetting(Action? onDetermine = null)
    {
        var window = MetaScreen.GenerateWindow(new(4.2f, 2.3f), HudManager.InstanceExists ? HudManager.Instance.transform : null, Vector3.zero, true, true, withMask: true);

        string GetCurrentWebhookString() => (Language.Translate("ui.discordWebhook.current") + ": ").Bold() + WebhookOption.urlShorten;
        bool SetWebhookStringFromClipboard() {
            string copied = Helpers.GetClipboardString();
            if (!copied.StartsWith(DiscordWebhookPrefix)) return false; //先頭がおかしい場合
            if (copied.Length < DiscordWebhookPrefix.Length + 8) return false; //短すぎる場合
            WebhookOption.urlEntry.Value = copied.Substring(DiscordWebhookPrefix.Length);

            return true;
        }

        TextMeshPro? currentText = null;
        var currentDisplay = new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent(GetCurrentWebhookString())) { PostBuilder = t => currentText = t };
        var checkBox = new NoSGUICheckbox(Virial.Media.GUIAlignment.Center, WebhookOption.autoSendEntry.Value) { OnValueChanged = val => WebhookOption.autoSendEntry.Value = val };
        
        
        window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
            GUI.API.Text(Virial.Media.GUIAlignment.Left,GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), new TranslateTextComponent("ui.discordWebhook.title")),
            GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
                currentDisplay,
                new NoSGUIMargin(Virial.Media.GUIAlignment.Center, new(0.1f, 0f)),
                GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.StandardMediumMasked), new TranslateTextComponent("ui.discordWebhook.urlFromClipboard"), _ => currentText.text = SetWebhookStringFromClipboard() ? GetCurrentWebhookString() : Language.Translate("ui.discordWebhook.failedPasteUrl").Color(Color.red))
            ),
            GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
                checkBox,
                new NoSGUIMargin(Virial.Media.GUIAlignment.Center, new(0.1f,0f)),
                new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new TranslateTextComponent("ui.discordWebhook.autoSend"))
            ),
            new NoSGUIMargin(Virial.Media.GUIAlignment.Center, new(0f, 0.12f)),
            GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), new TranslateTextComponent(onDetermine != null ? "ui.discordWebhook.send" : "ui.discordWebhook.determine"), _ =>
            {
                onDetermine?.Invoke();
                window.CloseScreen();
            })
        ),new(0.5f,0.5f), out _);

        
    }

    public ClientOption(ClientOptionType type,string name,string[] selections,int defaultValue)
    {
        id = name;
        configEntry = new IntegerDataEntry(name, ClientOptionSaver, defaultValue);
        this.selections = selections;
        this.type = type;
        AllOptions[type] = this;
    }

    public string DisplayName => Language.Translate("config.client." + id);
    public string DisplayValue => Language.Translate(selections[configEntry.Value]);
    public int Value => configEntry.Value;
    public Action? OnValueChanged;
    public bool ShowOnClientSetting { get; set; } = true;
    public void Increament()
    {
        configEntry.Value = (configEntry.Value + 1) % selections.Length;
        OnValueChanged?.Invoke();
    }
    static public void Preprocess(NebulaPreprocessor preprocessor)
    {
        new ClientOption(ClientOptionType.OutputCosmicHash, "outputHash", new string[] { "options.switch.off", "options.switch.on" }, 0);
        //new ClientOption(ClientOptionType.UseNoiseReduction, "noiseReduction", new string[] { "options.switch.off", "options.switch.on" }, 0);
        new ClientOption(ClientOptionType.ProcessorAffinity, "processorAffinity", new string[] {
        "config.client.processorAffinity.dontCare",
        "config.client.processorAffinity.dualCoreHT",
        "config.client.processorAffinity.dualCore",
        "config.client.processorAffinity.singleCore"}, 0)
        { OnValueChanged = ReflectProcessorAffinity };
        new ClientOption(ClientOptionType.ForceSkeldMeetingSE, "forceSkeldMeetingSE", new string[] { "options.switch.off", "options.switch.on" }, 0);
        new ClientOption(ClientOptionType.SpoilerAfterDeath, "spoilerAfterDeath", new string[] { "options.switch.off", "options.switch.on" }, 1);
        new ClientOption(ClientOptionType.PlayLobbyMusic, "playLobbyMusic", new string[] { "options.switch.off", "options.switch.on" }, 1) { 
            OnValueChanged = () =>
            {
                if (!LobbyBehaviour.Instance) return;
                bool playMusic = AllOptions[ClientOptionType.PlayLobbyMusic].Value == 1;

                if (playMusic)
                {
                    SoundManager.Instance.CrossFadeSound("MapTheme", LobbyBehaviour.Instance.MapTheme, 0.5f, 1.5f);
                }
                else
                {
                    SoundManager.Instance.CrossFadeSound("MapTheme", null, 0.5f, 1.5f);
                }
            }
        };
        new ClientOption(ClientOptionType.ButtonArrangement, "buttonArrangement", new string[] {
            "config.client.buttonArrangement.default", 
            "config.client.buttonArrangement.raiseOnlyLeft",
            "config.client.buttonArrangement.raiseBoth",
        }, 0);
        new ClientOption(ClientOptionType.ShowNoSLogoInLobby, "showNebulaLogoInLobby", new string[] { "options.switch.off", "options.switch.on" }, 1);
        new ClientOption(ClientOptionType.ShowOnlySpawnableAssignableOnFilter, "showOnlySpawnableAssignableOnFilter", new string[] { "options.switch.off", "options.switch.on" }, 0) { ShowOnClientSetting = false };
        ReflectProcessorAffinity();
    }

    static public void ReflectProcessorAffinity()
    {
        try
        {
            string? mode = null;
            switch (AllOptions[ClientOptionType.ProcessorAffinity].Value)
            {
                case 0:
                    mode = "0";
                    break;
                case 1:
                    mode = "2HT";
                    break;
                case 2:
                    mode = "2";
                    break;
                case 3:
                    mode = "1";
                    break;
            }

            if (mode == null) return;

            var process = System.Diagnostics.Process.GetCurrentProcess();
            string id = process.Id.ToString();

            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = "CPUAffinityEditor.exe";
            processStartInfo.Arguments = id + " " + mode;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            Process.Start(processStartInfo);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class StartOptionMenuPatch
{
    public static void Postfix(OptionsMenuBehaviour __instance)
    {
        __instance.transform.localPosition = new(0, 0, -300);

        foreach (var button in __instance.GetComponentsInChildren<CustomButton>(true))
        {
            if (button.name != "DoneButton") continue;

            button.onClick.AddListener(() => {
                if (AmongUsClient.Instance && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                    HudManager.Instance.ShowVanillaKeyGuide();
            });
        }
        var tabs = new List<TabGroup>(__instance.Tabs.ToArray());

        PassiveButton passiveButton;

        //設定項目を追加する

        GameObject nebulaTab = new GameObject("NebulaTab");
        nebulaTab.transform.SetParent(__instance.transform);
        nebulaTab.transform.localScale = new Vector3(1f, 1f, 1f);
        nebulaTab.SetActive(false);

        var nebulaScreen = MetaScreen.GenerateScreen(new(5f, 4.5f), nebulaTab.transform, new(0f, -0.28f, -10f), false, false, false);

        void SetNebulaWidget()
        {
            var buttonAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(2.05f, 0.26f) };
            MetaWidgetOld nebulaWidget = new();
            nebulaWidget.Append(ClientOption.AllOptions.Values.Where(o => o.ShowOnClientSetting), (option) => new MetaWidgetOld.Button(()=> {
                option.Increament();
                SetNebulaWidget();
            }, buttonAttr) { RawText = option.DisplayName + " : " + option.DisplayValue }, 2, -1, 0, 0.55f);
            nebulaWidget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

            if (!AmongUsClient.Instance || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                nebulaWidget.Append(new MetaWidgetOld.Button(() =>
                {
                    __instance.OpenTabGroup(tabs.Count - 1);
                    SetKeyBindingWidget();
                }, buttonAttr)
                { TranslationKey = "config.client.keyBindings", Alignment = IMetaWidgetOld.AlignmentOption.Center });
            }

            if(NebulaGameManager.Instance?.VoiceChatManager != null)
            {
                nebulaWidget.Append(new MetaWidgetOld.Button(() =>
                {
                    //__instance.Close();
                    NebulaGameManager.Instance?.VoiceChatManager?.OpenSettingScreen(__instance);
                }, buttonAttr)
                { TranslationKey = "config.client.vcSettings", Alignment = IMetaWidgetOld.AlignmentOption.Center });

                nebulaWidget.Append(new MetaWidgetOld.Button(() =>
                {
                    NebulaGameManager.Instance?.VoiceChatManager?.Rejoin();
                }, buttonAttr)
                { TranslationKey = "config.client.vcRejoin", Alignment = IMetaWidgetOld.AlignmentOption.Center });
            }

            nebulaWidget.Append(new MetaWidgetOld.Button(() =>
            {
                ClientOption.ShowWebhookSetting();
            }, buttonAttr)
            { TranslationKey = "config.client.webhook", Alignment = IMetaWidgetOld.AlignmentOption.Center });

            nebulaScreen.SetWidget(nebulaWidget);
        }

        GameObject keyBindingTab = new GameObject("KeyBindingTab");
        keyBindingTab.transform.SetParent(__instance.transform);
        keyBindingTab.transform.localScale = new Vector3(1f, 1f, 1f);
        keyBindingTab.SetActive(false);

        var keyBindingScreen = MetaScreen.GenerateScreen(new(5f, 4.5f), keyBindingTab.transform, new(0f, -0.28f, -10f), false, false, false);

        IKeyAssignment? currentAssignment = null;

        void SetKeyBindingWidget()
        {
            MetaWidgetOld keyBindingWidget = new();
            TMPro.TextMeshPro? text = null;
            keyBindingWidget.Append(IKeyAssignment.AllKeyAssignments, (assignment) =>
            new MetaWidgetOld.Button(() =>
            {
                currentAssignment = assignment;
                SetKeyBindingWidget();
            }, new(TextAttributeOld.NormalAttr) { Size = new Vector2(2.2f, 0.26f) })
            { RawText = assignment.DisplayName + " : " + (currentAssignment == assignment ? Language.Translate("input.recording") : ButtonEffect.KeyCodeInfo.GetKeyDisplayName(assignment.KeyInput)), PostBuilder = (_, _, t) => text = t }, 2, -1, 0, 0.55f);
            keyBindingScreen.SetWidget(keyBindingWidget);
        }

        void CoUpdate()
        {
            if (currentAssignment != null && Input.anyKeyDown)
            {
                foreach (var keyCode in ButtonEffect.KeyCodeInfo.AllKeyInfo.Values)
                {
                    if (Input.GetKeyDown(keyCode.keyCode))
                    {
                        currentAssignment.KeyInput = keyCode.keyCode;
                        currentAssignment = null;
                        SetKeyBindingWidget();
                        break;
                    }
                }
            }
        }

        keyBindingScreen.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += CoUpdate;

        SetNebulaWidget();
        SetKeyBindingWidget();

        //タブを追加する

        tabs[tabs.Count - 1] = (GameObject.Instantiate(tabs[1], null));
        var nebulaButton = tabs[tabs.Count - 1];
        nebulaButton.gameObject.name = "NebulaButton";
        nebulaButton.transform.SetParent(tabs[0].transform.parent);
        nebulaButton.transform.localScale = new Vector3(1f, 1f, 1f);
        nebulaButton.Content = nebulaTab;
        var textObj = nebulaButton.transform.FindChild("Text_TMP").gameObject;
        textObj.GetComponent<TextTranslatorTMP>().enabled = false;
        textObj.GetComponent<TMPro.TMP_Text>().text = "NoS";

        tabs.Add((GameObject.Instantiate(tabs[1], null)));
        var keyBindingTabButton = tabs[tabs.Count - 1];
        keyBindingTabButton.gameObject.name = "KeyBindingButton";
        keyBindingTabButton.transform.SetParent(tabs[0].transform.parent);
        keyBindingTabButton.transform.localScale = new Vector3(1f, 1f, 1f);
        keyBindingTabButton.Content = keyBindingTab;
        keyBindingTabButton.gameObject.SetActive(false);

        passiveButton = nebulaButton.gameObject.GetComponent<PassiveButton>();
        passiveButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        passiveButton.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            __instance.OpenTabGroup(tabs.Count - 2);
            SetNebulaWidget();
        }
        ));

        float y = tabs[0].transform.localPosition.y, z = tabs[0].transform.localPosition.z;
        if (tabs.Count == 4)
            for (int i = 0; i < 3; i++) tabs[i].transform.localPosition = new Vector3(1.7f * (float)(i - 1), y, z);
        else if (tabs.Count == 5)
            for (int i = 0; i < 4; i++) tabs[i].transform.localPosition = new Vector3(1.62f * ((float)i - 1.5f), y, z);

        __instance.Tabs = new Il2CppReferenceArray<TabGroup>(tabs.ToArray());


    }
}
