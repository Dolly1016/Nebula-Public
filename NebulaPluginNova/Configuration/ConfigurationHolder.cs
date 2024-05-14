using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Configuration;
using Virial.Game;
using Virial.Text;

namespace Nebula.Configuration;

internal class ConfigurationHolder : IConfigurationHolder
{
    TextComponent title, detail;
    List<IConfiguration> myConfigurations;
    BitMask<ConfigurationTab> tabs;
    BitMask<GameModeDefinition> gamemodes;
    List<ConfigurationHolder> relatedHolders;
    List<ConfigurationTag> tags;

    public ConfigurationHolder(string id, BitMask<ConfigurationTab> tabs, BitMask<GameModeDefinition> gamemodes, IEnumerable<IConfiguration> configurations)
    {
        this.title = new TranslateTextComponent(id);
        this.detail = new TranslateTextComponent(id + ".detail");
        this.tabs = tabs;
        this.tags = new();
        this.gamemodes = gamemodes;
        this.myConfigurations = new(configurations);
        this.relatedHolders = new();
    }

    TextComponent IConfigurationHolder.Title => title;

    TextComponent IConfigurationHolder.Detail => detail;

    IEnumerable<IConfiguration> IConfigurationHolder.Configurations => myConfigurations;

    BitMask<ConfigurationTab> IConfigurationHolder.Tabs => tabs;

    BitMask<GameModeDefinition> IConfigurationHolder.GameModes => gamemodes;

    IEnumerable<IConfigurationHolder> IConfigurationHolder.RelatedHolders => relatedHolders;
    IEnumerable<ConfigurationTag> IConfigurationHolder.Tags => tags;

    void IConfigurationHolder.AppendConfiguration(Virial.Configuration.IConfiguration configuration) => myConfigurations.Add(configuration);
    void IConfigurationHolder.AppendConfigurations(IEnumerable<Virial.Configuration.IConfiguration> configuration) => myConfigurations.AddRange(configuration);

    void IConfigurationHolder.AddTags(params ConfigurationTag[] tags)
    {
        this.tags.AddRange(tags);
    }
}

public class ConfigurationTags
{
    static private IDividedSpriteLoader TagSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ConfigurationTag.png", 100f, 42, 42, true);
    static private GUIWidget GetTagTextWidget(string translationKey) => new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new TranslateTextComponent("configuration.tag." + translationKey));
    static public ConfigurationTag TagChaotic { get; private set; } = new(TagSprite.AsLoader(0), GetTagTextWidget("chaotic"));
    static public ConfigurationTag TagBeginner { get; private set; } = new(TagSprite.AsLoader(1), GetTagTextWidget("beginner"));
    static public ConfigurationTag TagFunny { get; private set; } = new(TagSprite.AsLoader(4), GetTagTextWidget("funny"));
    static public ConfigurationTag TagDifficult { get; private set; } = new(TagSprite.AsLoader(5), GetTagTextWidget("difficult"));
    static public ConfigurationTag TagSNR { get; private set; } = new(TagSprite.AsLoader(2), GetTagTextWidget("superNewRoles"));
}