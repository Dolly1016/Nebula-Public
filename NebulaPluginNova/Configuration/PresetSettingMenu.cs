using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System.Linq;
using Virial;
using Virial.Media;
using Virial.Text;

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

                void LoadPreset()
                {
                    void LoadInner()
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
                        MetaUI.ShowYesOrNoDialog(HudManager.Instance.transform, LoadInner, () => { }, Language.Translate(preset.IsOldType ? "preset.confirmLoadOld" : "preset.confirmLoadUnknown"), true);
                    }
                    else LoadInner();
                }

                var isEditablePreset = (preset is ConfigPreset cp && cp.Addon == null);
                widget.Add(GUI.Instance.RawButton(
                    GUIAlignment.Center, GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardLargeWideMasked),
                    preset.DisplayName,
                    _ => LoadPreset(), elem => NebulaManager.Instance.SetHelpWidget(elem.uiElement, preset.Detail), elem => NebulaManager.Instance.HideHelpWidgetIf(elem.uiElement),
                    clickable => {
                        var window = MetaScreen.GenerateWindow(new(3.5f, isEditablePreset ? 2.85f : 1f), HudManager.Instance.transform, Vector3.zero, true, true, true, BackgroundSetting.Modern);
                        window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                            GUI.API.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, preset.DisplayName),
                            GUI.API.VerticalMargin(0.2f),
                            GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.OptionsButtonLonger, "preset.load", _ => {
                                window.CloseScreen();
                                LoadPreset();
                            }),
                            isEditablePreset ? GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.OptionsButtonLonger, "preset.rename", _ => {
                                //元のプリセットを読む
                                var rawLines = ConfigPreset.ReadRawPreset(preset.Id);
                                if(rawLines == null)
                                {
                                    MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent("preset.openFailed"));
                                    window.CloseScreen();
                                    return;
                                }

                                //新たな名前を決める
                                var nameWindow = MetaScreen.GenerateWindow(new(4f, 1.7f), HudManager.Instance.transform, Vector3.zero, true, false);

                                var textField = new GUITextField(GUIAlignment.Center, new(3f, 0.28f))
                                {
                                    IsSharpField = false,
                                    HintText = "Please input new name!".Color(Color.gray),
                                    WithMaskMaterial = false
                                };
                                nameWindow.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Left,
                                    GUI.API.LocalizedText(GUIAlignment.Left, AttributeAsset.OverlayContent, "preset.enterPresetName"),
                                    GUI.API.VerticalMargin(0.1f),
                                    textField,
                                    GUI.API.VerticalMargin(0.1f),
                                    GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, "preset.save", _ =>
                                    {
                                        var newName = textField.Artifact.FirstOrDefault()?.Text;
                                        if ((newName?.Length ?? 0) == 0) return;

                                        window.CloseScreen();
                                        nameWindow.CloseScreen();

                                        //実際の名前変更処理
                                        var tempPath = ConfigPreset.GetPathFromId(preset.Id) + ".old";
                                        File.Move(ConfigPreset.GetPathFromId(preset.Id), tempPath);
                                        File.WriteAllLines(ConfigPreset.GetPathFromId(newName!), rawLines.Where(l => !l.StartsWith("#DISPLAY")).Prepend(ConfigPreset.ToDisplayNameText(newName!)));
                                        File.Delete(tempPath);
                                        ConfigPreset.LoadLocal();
                                        ShowInner();
                                    })
                                    ), new Vector2(0.5f, 1f), out var nameSize);
                                TextField.EditFirstField();
                            }) : null,
                            isEditablePreset ? GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.OptionsButtonLonger, "preset.overwrite", _ => {
                                MetaUI.ShowYesOrNoDialog(HudManager.Instance.transform, () => {
                                    window.CloseScreen();
                                    bool success = ConfigPreset.OutputAndReloadSettings(preset.Id, preset.DisplayName);
                                    ShowInner();
                                    MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent(success ? "preset.saveSuccess" : "preset.saveFailed"));
                                }, () => { }, Language.Translate("preset.delete.overwrite"), true);
                            }) : null,
                            isEditablePreset ? GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.OptionsButtonLonger, "preset.delete", _ => {
                                MetaUI.ShowYesOrNoDialog(HudManager.Instance.transform, () => {
                                    window.CloseScreen();
                                    ConfigPreset.DeleteAndReloadSettings(preset.Id);
                                    ShowInner();
                                }, () => { }, Language.Translate("preset.delete.confirm"), true);
                            }) : null
                            ), new UnityEngine.Vector2(0.5f, 1f), out _);
                    }));
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
