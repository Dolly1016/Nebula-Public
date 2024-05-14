using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Configuration;
using Virial.Game;

namespace Nebula.Configuration;

public class ConfigurationsAPI : Virial.Configuration.Configurations
{
    static public ConfigurationsAPI API { get; private set; } = new();

    ISharableVariable<bool> Configurations.BoolVariable(string id, bool defaultValue) => new BoolConfigurationValue(id, defaultValue);
    ISharableVariable<float> Configurations.FloatVariable(string id, float[] values, float defaultValue) => new FloatConfigurationValue(id, values, defaultValue);
    ISharableVariable<int> Configurations.IntegerVariable(string id, int[] values, int defaultValue) => new IntegerConfigurationValue(id, values, defaultValue);
    ISharableVariable<int> Configurations.SelectionVariable(string id, int defaultValue, int maxValueExcluded) => new SelectionConfigurationValue(id, defaultValue, maxValueExcluded);
    IConfigurationHolder Configurations.Holder(string id, IEnumerable<ConfigurationTab> tabs, IEnumerable<GameModeDefinition> gamemodes)
    {
        BitMask32<ConfigurationTab> tabMask = new(t => t.AsBit, tabs.Aggregate(0, (val, tab) => val | tab.AsBit));
        BitMask32<GameModeDefinition> gamemodeMask = new(g => g.AsBit, gamemodes.Aggregate(0, (val, tab) => val | tab.AsBit));

        return new ConfigurationHolder(id, tabMask, gamemodeMask, []);
    }

    ModifierFilter Configurations.ModifierFilter(string id) => new ModifierFilterImpl(id);
    GhostRoleFilter Configurations.GhostRoleFilter(string id) => new GhostRoleFilterImpl(id);
}
