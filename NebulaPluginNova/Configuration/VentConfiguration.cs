using Nebula.Modules.GUIWidget;
using System.Text;
using UnityEngine;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;
using static Il2CppMono.Security.X509.X520;

namespace Nebula.Configuration;

internal class VentConfiguration : IVentConfiguration
{
    BoolConfiguration? canUseEntry = null;
    IOrderedSharableVariable<float>? coolDownEntry = null;
    IOrderedSharableVariable<float>? durationEntry = null;
    IOrderedSharableVariable<int>? usesEntry = null;

    internal VentConfiguration(string id, bool isOptional, int[]? usesSelection, int usesDefaultValue, float[]? coolDownSelection, float coolDownDefaultValue, float[]? durationSelection, float durationDefaultValue)
    {
        if (isOptional) canUseEntry = new BoolConfigurationImpl(id + ".canUseVent", false) { Title = new TranslateTextComponent("role.general.canUseVent") };
        if (usesSelection != null) usesEntry = new IntegerConfigurationValue(id + ".uses", usesSelection, usesDefaultValue);
        if (coolDownSelection != null) coolDownEntry = new FloatConfigurationValue(id + ".coolDown", coolDownSelection, coolDownDefaultValue);
        if (durationSelection != null) durationEntry = new FloatConfigurationValue(id + ".duration", durationSelection, durationDefaultValue);
    }

    int IVentConfiguration.Uses => usesEntry?.CurrentValue ?? 0;

    float IVentConfiguration.CoolDown => coolDownEntry?.CurrentValue ?? 0f;

    float IVentConfiguration.Duration => durationEntry?.CurrentValue ?? 0f;

    bool IVentConfiguration.CanUseVent => canUseEntry?.GetValue() ?? true;

    bool IConfiguration.IsShown => true;

    string? IConfiguration.GetDisplayText()
    {
        List<string> sb = new();
        bool hasParameter = usesEntry != null || coolDownEntry != null || durationEntry != null;

        if (canUseEntry != null && (!canUseEntry.GetValue() || !hasParameter)) 
            sb.Add(Language.Translate("role.general.canUseVent") + ": " + Language.Translate(canUseEntry.GetValue() ? "options.switch.on" : "options.switch.off"));

        if (hasParameter && (canUseEntry == null || canUseEntry.GetValue()))
        {
            string str = Language.Translate("role.general.ventOption") + ": (";
            string?[] vals = [
                usesEntry != null ? (Language.Translate("role.general.ventUses") + ": " + usesEntry.Value) : null,
                coolDownEntry != null ? (Language.Translate("role.general.ventCoolDown") + ": " + coolDownEntry.Value + Language.Translate("options.sec")) : null,
                durationEntry != null ? (Language.Translate("role.general.ventDuration") + ": " + durationEntry.Value + Language.Translate("options.sec")) : null,
            ];
            str += vals.Where(s => s != null).Join(null, ", ") + ")";
            sb.Add(str);
        }
        return sb.Join(null, "\n");
    }

    GUIWidgetSupplier IConfiguration.GetEditor()
    => () =>
    {
        List<GUIWidget> result = new();

        if(canUseEntry != null) result.Add(canUseEntry.GetEditor().Invoke());

        if ((this as IVentConfiguration).CanUseVent)
        {
            List<GUIWidget> widgets = new();

            void AddOptionToEditor(IOrderedSharableEntry config, Func<string> shower, string translationKey)
            {
                widgets.AddRange([
                    GUI.API.HorizontalMargin(0.1f),
                    GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsValue), translationKey),
                    GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), ":"),
                    GUI.API.HorizontalMargin(0.1f),
                    GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsValueShorter), shower.Invoke()),
                    GUI.API.SpinButton(GUIAlignment.Center, v => { config.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                    ]);
            }

            if (canUseEntry == null) widgets.AddRange([GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), "role.general.ventOption"), GUI.API.HorizontalMargin(0.2f)]);
            if (usesEntry != null) AddOptionToEditor(usesEntry, () => usesEntry.Value.ToString(), "role.general.ventUses");
            if (coolDownEntry != null) AddOptionToEditor(coolDownEntry, () => coolDownEntry.Value + Language.Translate("options.sec"), "role.general.ventCoolDown");
            if (durationEntry != null) AddOptionToEditor(durationEntry, () => durationEntry.Value + Language.Translate("options.sec"), "role.general.ventDuration");

            result.Add(new HorizontalWidgetsHolder(GUIAlignment.Left, widgets));
        }

        return new VerticalWidgetsHolder(GUIAlignment.Left, result);
    };
}
