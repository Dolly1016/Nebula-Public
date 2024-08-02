using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Media;

namespace Nebula.Configuration;

public class PresetSettingMenu : MonoBehaviour
{
    static PresetSettingMenu()
    {
        ClassInjector.RegisterTypeInIl2Cpp<PresetSettingMenu>();
    }

    public GameSettingMenu GameSettingMenu { get; set; }
    public void Start()
    {
        var screen = new GUIScreenImpl(Virial.Media.Anchor.At(new(0.5f,0.5f)), new(2f, 3.6f),transform, new(-3.3f, -0.4f, -10f));

        Virial.Compat.Artifact<GUIScreen> innerRef = null!;
        void ShowInner()
        {
            List<GUIWidget> widget = new();
            foreach (var preset in IConfigPreset.AllPresets)
            {
                if (preset.IsHidden || preset.RelatedHolder != null) continue;

                widget.Add(GUI.Instance.RawButton(
                    GUIAlignment.Center, GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardLargeWideMasked),
                    preset.DisplayName,
                    _ => {
                        void Load()
                        {
                            bool success = preset.LoadPreset();
                            if (success) ConfigurationValues.RpcSharePresetName.Invoke(preset.DisplayName);
                            //値を再読み込み
                            foreach (var child in GameSettingMenu.GameSettingsTab.Children) child.Initialize();

                            MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent(success ? "preset.loadSuccess" : "preset.loadFailed"));

                            try
                            {
                                NebulaSettingMenu.Instance?.OpenFirstPage();
                            }
                            catch { }
                        }

                        if (preset.IsOldType || preset.IsUnknownType)
                        {
                            MetaUI.ShowYesOrNoDialog(HudManager.Instance.transform, Load, () => { }, Language.Translate(preset.IsOldType ? "preset.confirmLoadOld" : "preset.confirmLoadUnknown"), true);
                        }
                        else Load();
                    }, elem => NebulaManager.Instance.SetHelpWidget(elem.uiElement, preset.Detail), elem => NebulaManager.Instance.HideHelpWidgetIf(elem.uiElement)));
            }
            
            innerRef.Do(screen => screen.SetWidget(GUI.Instance.VerticalHolder(GUIAlignment.Center, widget), out _));
        }

        var widget = NebulaAPI.GUI.VerticalHolder(Virial.Media.GUIAlignment.Center,
            GUI.Instance.LocalizedButton(
                Virial.Media.GUIAlignment.Center,
                GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardMediumMasked),
                "preset.saveAs",
                _ =>
                {
                    var popup = MetaScreen.GenerateWindow(new(4f, 1.7f), HudManager.Instance.transform, Vector3.zero, true, false);
                    MetaWidgetOld widget = new();
                    Reference<TextField> fieldRef = new();
                    widget.Append(new MetaWidgetOld.Text(TextAttributeOld.NormalAttrLeft) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = "preset.enterPresetName" });
                    widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));
                    widget.Append(new MetaWidgetOld.TextInput(1, 1.2f, new(3f, 0.28f)) { Hint = "Current Output".Color(Color.gray), TextFieldRef = fieldRef, Alignment = IMetaWidgetOld.AlignmentOption.Center });
                    widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));
                    widget.Append(new MetaWidgetOld.Button(() =>
                    {
                        string name = fieldRef.Value!.Text;
                        if (name.Length == 0) name = "Current Output";
                        bool success = ConfigPreset.OutputAndReloadSettings(name);
                        ShowInner();
                        popup.CloseScreen();

                        MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent(success ? "preset.saveSuccess" : "preset.saveFailed"));

                    }, TextAttributeOld.BoldAttr)
                    { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "preset.save" });
                    popup.SetWidget(widget);
                    TextField.EditFirstField();
                }),
            GUI.Instance.LocalizedButton(
                Virial.Media.GUIAlignment.Center,
                GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardMediumMasked),
                "preset.reload",
                _ => {
                    ConfigPreset.LoadLocal();
                    ShowInner();
                })
            );

        screen.SetWidget(widget, out _);

        MetaScreen mainScreen = MetaScreen.GenerateScreen(new Vector2(6f, 4f), transform, new(1.5f, -0.1f, -10f), false, false, false);
        mainScreen.SetWidget(GUI.Instance.ScrollView(Virial.Media.GUIAlignment.Top, new(5f, 4f), "Preset", null, out innerRef), out _);

        ShowInner();
    }
}
