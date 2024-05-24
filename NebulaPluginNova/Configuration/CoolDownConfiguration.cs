using Il2CppSystem;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;
using static Il2CppMono.Security.X509.X520;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace Nebula.Configuration;

internal class KillCoolDownConfiguration : IRelativeCoolDownConfiguration
{
    static string[] typeTranslateKeys = ["options.killCoolDown.type.immediate", "options.killCoolDown.type.relative", "options.killCoolDown.type.ratio"];
    IOrderedSharableVariable<int> coolDownTypeEntry;
    IOrderedSharableVariable<float> immediateEntry;
    IOrderedSharableVariable<float> relativeEntry;
    IOrderedSharableVariable<float> ratioEntry;
    string CurrentCoolDownStr => coolDownTypeEntry.Value switch { 2 => ratioCoolDownStr, 1 => relativeCoolDownStr, _ => immediateCoolDownStr };
    IOrderedSharableVariable<float> CurrentEntry => coolDownTypeEntry.Value switch { 2 => ratioEntry, 1 => relativeEntry, _ => immediateEntry };
    TextComponent title;

    internal KillCoolDownConfiguration(TextComponent title, string id, CoolDownType defaultType, float[] immediateSelection, float immediateDefaultValue, float[] relativeSelection, float relativeDefaultValue, float[] ratioSelection, float ratioDefaultValue)
    {
        this.title = title;
        coolDownTypeEntry = new IntegerConfigurationValue(id + ".type", [0, 1, 2], (int)defaultType);
        immediateEntry = new FloatConfigurationValue(id + ".immediate", immediateSelection, immediateDefaultValue);
        relativeEntry = new FloatConfigurationValue(id + ".relative", relativeSelection, relativeDefaultValue);
        ratioEntry = new FloatConfigurationValue(id + ".ratio", ratioSelection, ratioDefaultValue);
    }

    string immediateCoolDownStr => immediateEntry.Value + Language.Translate("options.sec");
    string relativeCoolDownStr => relativeEntry.Value switch { < 0 => "", > 0 => "+", _ => "±" } + relativeEntry.Value + Language.Translate("options.sec");
    string ratioCoolDownStr => ratioEntry.Value + Language.Translate("options.cross");

    float IRelativeCoolDownConfiguration.CoolDown => coolDownTypeEntry.Value switch
    {
        2 => ratioEntry.Value * AmongUsUtil.VanillaKillCoolDown,
        1 => System.Math.Max(0f, relativeEntry.Value + AmongUsUtil.VanillaKillCoolDown),
        _ => immediateEntry.Value
    };

    bool IConfiguration.IsShown => true;

    string? IConfiguration.GetDisplayText() => title.GetString() + ": " + CurrentCoolDownStr +( " (" + (this as IRelativeCoolDownConfiguration).CoolDown + Language.Translate("options.sec") + ")").Color(Color.gray);

    GUIWidgetSupplier IConfiguration.GetEditor() =>
    new HorizontalWidgetsHolder(GUIAlignment.Left,
        GUI.API.Text(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf), title),
        GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), ":"),
        GUI.API.HorizontalMargin(0.1f),
        GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsButtonLonger), typeTranslateKeys[coolDownTypeEntry.Value], _ => { coolDownTypeEntry.ChangeValue(true, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
        GUI.API.HorizontalMargin(0.2f),
        GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsValueShorter), CurrentCoolDownStr),
        GUI.API.SpinButton(GUIAlignment.Center, v => { CurrentEntry.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
    );
    
}
