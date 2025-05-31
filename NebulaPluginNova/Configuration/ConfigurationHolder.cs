using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Configuration;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Configuration;

[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class ConfigurationHolder : IConfigurationHolder
{
    TextComponent title, detail;
    List<IConfiguration> myConfigurations;
    BitMask<ConfigurationTab> tabs;
    BitMask<GameModeDefinition> gamemodes;
    List<ConfigurationUpperButton> relatedButtons;
    List<ConfigurationTag> tags;
    Func<bool>? isShown;
    Func<ConfigurationHolderState>? state;

    static private List<ConfigurationHolder> allHolders = [];
    static public IEnumerable<IConfigurationHolder> AllHolders = allHolders;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        allHolders.Sort((h1, h2) => h1.tabs.AsRawPattern != h2.tabs.AsRawPattern ? (int)h1.tabs.AsRawPattern - (int)h2.tabs.AsRawPattern : h1.title.TextForCompare.CompareTo(h2.title.TextForCompare));
    }

    public ConfigurationHolder(TextComponent title, TextComponent detail, BitMask<ConfigurationTab> tabs, BitMask<GameModeDefinition> gamemodes, IEnumerable<IConfiguration> configurations, Func<bool>? isShown = null, Func<ConfigurationHolderState>? state = null)
    {
        this.title = title;
        this.detail = detail;
        this.tabs = tabs;
        this.tags = [];
        this.gamemodes = gamemodes;
        this.myConfigurations = new(configurations);
        this.relatedButtons = [];
        this.isShown = isShown;
        this.state = state;

        allHolders.Add(this);
        
    }

    TextComponent IConfigurationHolder.Title { get => title; set => title = value; }

    TextComponent IConfigurationHolder.Detail { get => detail; set => detail = value; }

    IEnumerable<IConfiguration> IConfigurationHolder.Configurations => myConfigurations;

    BitMask<ConfigurationTab> IConfigurationHolder.Tabs => tabs;

    BitMask<GameModeDefinition> IConfigurationHolder.GameModes => gamemodes;

    IEnumerable<ConfigurationUpperButton> IConfigurationHolder.RelatedInformation => relatedButtons;
    IEnumerable<ConfigurationTag> IConfigurationHolder.Tags => tags;

    IConfigurationHolder IConfigurationHolder.AppendConfiguration(Virial.Configuration.IConfiguration configuration) { myConfigurations.Add(configuration); return this; }
    IConfigurationHolder IConfigurationHolder.AppendConfigurations(IEnumerable<Virial.Configuration.IConfiguration> configuration) { myConfigurations.AddRange(configuration); return this; }

    IConfigurationHolder IConfigurationHolder.AddTags(params ConfigurationTag[] tags)
    {
        this.tags.AddRange(tags);
        return this;
    }

    IConfigurationHolder IConfigurationHolder.AppendRelatedHolders(params IConfigurationHolder[] holders) { relatedButtons.AddRange(holders.Select(h => new ConfigurationUpperButton(h.Title, () => h.IsShown, () => NebulaSettingMenu.Instance?.OpenSecondaryPage(h)))); return this; }

    IConfigurationHolder IConfigurationHolder.AppendRelatedAction(TextComponent label, Func<bool> predicate, Action onClicked)
    {
        relatedButtons.Add(new(label, predicate, onClicked));
        return this;
    }


    bool IConfigurationHolder.IsShown => isShown?.Invoke() ?? true;

    ConfigurationHolderState IConfigurationHolder.DisplayOption => state?.Invoke() ?? ConfigurationHolderState.Activated;
    void IConfigurationHolder.SetDisplayState(Func<ConfigurationHolderState> state) => this.state = state;

    public Image? Illustration { get; set; }
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