using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Configuration;

public class PresetSettingMenu : MonoBehaviour
{
    static PresetSettingMenu()
    {
        ClassInjector.RegisterTypeInIl2Cpp<PresetSettingMenu>();
    }

    public void Start()
    {
        Reference<MetaContext.ScrollView.InnerScreen> innerRef = new();
        void ShowInner()
        {
            MetaContext context = new();
            foreach(var preset in ConfigPreset.AllPresets)
            {
                if (preset.IsHidden || preset.relatedHolder != null) continue;

                context.Append(new MetaContext.Button(() => {
                    MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent(preset.LoadPreset(new()) ? "preset.loadSuccess" : "preset.loadFailed"));
                    NebulaSettingMenu.Instance?.OpenFirstPage();
                }, new(TextAttribute.BoldAttr) { Size = new(2.5f, 0.4f) }) { RawText = preset.DisplayName }.SetAsMaskedButton());
            }

            innerRef.Value?.SetContext(context);
        }

        MetaScreen screen = MetaScreen.GenerateScreen(new Vector2(2f, 3.6f), transform, new(-4.1f, -0.4f, -10f), false, false, false);
        MetaContext context = new();
        context.Append(new MetaContext.Button(() =>
        {
            var popup = MetaScreen.GenerateWindow(new(4f, 1.7f), HudManager.Instance.transform, Vector3.zero, true, false);
            MetaContext context = new();
            Reference<TextField> fieldRef = new();
            context.Append(new MetaContext.Text(TextAttribute.NormalAttrLeft) { Alignment = IMetaContext.AlignmentOption.Left, TranslationKey = "preset.enterPresetName" });
            context.Append(new MetaContext.VerticalMargin(0.1f));
            context.Append(new MetaContext.TextInput(1, 1.2f, new(3f, 0.28f)) { Hint = "Current Output".Color(Color.gray), TextFieldRef = fieldRef, Alignment = IMetaContext.AlignmentOption.Center });
            context.Append(new MetaContext.VerticalMargin(0.1f));
            context.Append(new MetaContext.Button(() =>
            {
                string name = fieldRef.Value!.Text;
                if (name.Length == 0) name = "Current Output";
                ConfigPreset.OutputAndReloadSettings(name);
                ShowInner();
                popup.CloseScreen();

                MetaUI.ShowConfirmDialog(HudManager.Instance.transform, new TranslateTextComponent("preset.saveSuccess"));
            }, TextAttribute.BoldAttr)
            { Alignment = IMetaContext.AlignmentOption.Center, TranslationKey = "preset.save" });
            popup.SetContext(context);
            TextField.EditFirstField();
        }, TextAttribute.BoldAttr)
        { TranslationKey = "preset.saveAs" });
        context.Append(new MetaContext.Button(() =>
        {
            ConfigPreset.LoadLocal();
            ShowInner();
        }, TextAttribute.BoldAttr)
        { TranslationKey = "preset.reload" });

        screen.SetContext(context);

        MetaScreen mainScreen = MetaScreen.GenerateScreen(new Vector2(6f, 4f), transform, new(0.9f, -0.4f, -10f), false, false, false);
        mainScreen.SetContext(new MetaContext.ScrollView(new(5f, 4f), new MetaContext()) { InnerRef = innerRef, ScrollerTag = "Preset" });
        ShowInner();
    }
}
