using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Modules.MetaContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Nebula.Configuration;

public class PresetSettingMenu : MonoBehaviour
{
    static PresetSettingMenu()
    {
        ClassInjector.RegisterTypeInIl2Cpp<PresetSettingMenu>();
    }

    public void Start()
    {
        var screen = new GUIScreenImpl(Virial.Media.Anchor.At(new(0.5f,0.5f)), new(2f, 3.6f),transform, new(-4.1f, -0.4f, -10f));

        Virial.Compat.Artifact<GUIScreen> innerRef = null!;
        void ShowInner()
        {
            List<GUIContext> context = new();
            foreach (var preset in IConfigPreset.AllPresets)
            {
                if (preset.IsHidden || preset.RelatedHolder != null) continue;

                context.Add(GUI.Instance.RawButton(
                    GUIAlignment.Center, GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardLargeWideMasked),
                    preset.DisplayName,
                    () => {
                        MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent(preset.LoadPreset() ? "preset.loadSuccess" : "preset.loadFailed"));
                        NebulaSettingMenu.Instance?.OpenFirstPage();
                    }));
            }
            
            innerRef.Do(screen => screen.SetContext(GUI.Instance.VerticalHolder(GUIAlignment.Center, context), out _));
        }

        var context = NebulaImpl.Instance.GUILibrary.VerticalHolder(Virial.Media.GUIAlignment.Center,
            GUI.Instance.LocalizedButton(
                Virial.Media.GUIAlignment.Center,
                GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardMediumMasked),
                "preset.saveAs",
                () =>
                {
                    var popup = MetaScreen.GenerateWindow(new(4f, 1.7f), HudManager.Instance.transform, Vector3.zero, true, false);
                    MetaContextOld context = new();
                    Reference<TextField> fieldRef = new();
                    context.Append(new MetaContextOld.Text(TextAttribute.NormalAttrLeft) { Alignment = IMetaContextOld.AlignmentOption.Left, TranslationKey = "preset.enterPresetName" });
                    context.Append(new MetaContextOld.VerticalMargin(0.1f));
                    context.Append(new MetaContextOld.TextInput(1, 1.2f, new(3f, 0.28f)) { Hint = "Current Output".Color(Color.gray), TextFieldRef = fieldRef, Alignment = IMetaContextOld.AlignmentOption.Center });
                    context.Append(new MetaContextOld.VerticalMargin(0.1f));
                    context.Append(new MetaContextOld.Button(() =>
                    {
                        string name = fieldRef.Value!.Text;
                        if (name.Length == 0) name = "Current Output";
                        ConfigPreset.OutputAndReloadSettings(name);
                        ShowInner();
                        popup.CloseScreen();

                        MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent("preset.saveSuccess"));
                    }, TextAttribute.BoldAttr)
                    { Alignment = IMetaContextOld.AlignmentOption.Center, TranslationKey = "preset.save" });
                    popup.SetContext(context);
                    TextField.EditFirstField();
                }),
            GUI.Instance.LocalizedButton(
                Virial.Media.GUIAlignment.Center,
                GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.StandardMediumMasked),
                "preset.reload",
                () => {
                    ConfigPreset.LoadLocal();
                    ShowInner();
                })
            );

        screen.SetContext(context, out _);

        MetaScreen mainScreen = MetaScreen.GenerateScreen(new Vector2(6f, 4f), transform, new(0.9f, -0.4f, -10f), false, false, false);
        mainScreen.SetContext(GUI.Instance.ScrollView(Virial.Media.GUIAlignment.Top, new(5f, 4f), "Preset", null, out innerRef), out _);

        ShowInner();
    }
}
