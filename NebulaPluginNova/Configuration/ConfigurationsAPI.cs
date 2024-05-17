using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Compat;
using Virial.Configuration;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Configuration;

public class ConfigurationsAPI : Virial.Configuration.Configurations
{
    static public ConfigurationsAPI API { get; private set; } = new();

    IOrderedSharableVariable<bool> Configurations.SharableVariable(string id, bool defaultValue) => new BoolConfigurationValue(id, defaultValue);
    IOrderedSharableVariable<float> Configurations.SharableVariable(string id, FloatSelection values, float defaultValue) => new FloatConfigurationValue(id, values.Selection, defaultValue);
    IOrderedSharableVariable<int> Configurations.SharableVariable(string id, IntegerSelection values, int defaultValue) => new IntegerConfigurationValue(id, values.Selection, defaultValue);
    ISharableVariable<int> Configurations.SharableVariable(string id, int defaultValue, int maxValueExcluded) => new SelectionConfigurationValue(id, defaultValue, maxValueExcluded);
    IConfigurationHolder Configurations.Holder(string id, IEnumerable<ConfigurationTab> tabs, IEnumerable<GameModeDefinition> gamemodes)
    {
        BitMask32<ConfigurationTab> tabMask = new(t => t.AsBit, tabs.Aggregate(0, (val, tab) => val | tab.AsBit));
        BitMask32<GameModeDefinition> gamemodeMask = new(g => g.AsBit, gamemodes.Aggregate(0, (val, tab) => val | tab.AsBit));

        return new ConfigurationHolder(id, tabMask, gamemodeMask, []);
    }

    ModifierFilter Configurations.ModifierFilter(string id) => new ModifierFilterImpl(id);
    GhostRoleFilter Configurations.GhostRoleFilter(string id) => new GhostRoleFilterImpl(id);

    BoolConfiguration Configurations.Configuration(string id, bool defaultValue, Func<bool>? predicate)
    {
        var config = new BoolConfigurationImpl(id, defaultValue);
        if (predicate != null) config.Predicate = predicate;
        return config;
    }

    IntegerConfiguration Configurations.Configuration(string id, IntegerSelection selection, int defaultValue, Func<bool>? predicate)
    {
        var config = new IntegerConfigurationImpl(id, selection.Selection, defaultValue);
        if (predicate != null) config.Predicate = predicate;
        return config;
    }

    FloatConfiguration Configurations.Configuration(string id, FloatSelection selection, float defaultValue, FloatConfigurationDecorator decorator, Func<bool>? predicate)
    {
        var config = new FloatConfigurationImpl(id, selection.Selection, defaultValue);
        if (decorator == FloatConfigurationDecorator.Ratio)
            config.DecorateAsRatioConfiguration();
        if (decorator == FloatConfigurationDecorator.Second)
            config.DecorateAsSecConfiguration();

        return config;
    }

    ValueConfiguration<int> Configurations.Configuration(string id, string[] selection, int defualtIndex, Func<bool>? predicate)
    {
        var config = new StringConfigurationImpl(id, selection, defualtIndex);
        if (predicate != null) config.Predicate = predicate;
        return config;
    }

    IConfiguration Configurations.Configuration(Func<string?> displayShower, GUIWidgetSupplier editor, Func<bool>? predicate) => new EditorConfiguration(displayShower, editor, predicate);

    void Configurations.RequireUpdateSettingScreen()
    {
        if (NebulaSettingMenu.Instance) NebulaSettingMenu.Instance.UpdateSecondaryPage();
    }

    IVentConfiguration Configurations.VentConfiguration(string id, bool isOptional, IntegerSelection? usesSelection, int usesDefaultValue, FloatSelection? coolDownSelection, float coolDownDefaultValue, FloatSelection? durationSelection, float durationDefaultValue)
     => new VentConfiguration(id, isOptional, usesSelection?.Selection, usesDefaultValue, coolDownSelection?.Selection, coolDownDefaultValue, durationSelection?.Selection, durationDefaultValue);

    IRelativeCoolDownConfiguration Configurations.KillConfiguration(TextComponent title, string id, CoolDownType defaultType, FloatSelection immediateSelection, float immediateDefaultValue, FloatSelection relativeSelection, float relativeDefaultValue, FloatSelection ratioSelection, float ratioDefaultValue)
     => new KillCoolDownConfiguration(title, id, defaultType, immediateSelection.Selection, immediateDefaultValue, relativeSelection.Selection, relativeDefaultValue, ratioSelection.Selection, ratioDefaultValue);
}
