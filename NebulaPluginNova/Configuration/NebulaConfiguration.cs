using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Nebula.Roles.Assignment;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;

namespace Nebula.Configuration;



public interface INebulaConfigEntry
{
    public int CurrentValue { get; }
    public int Id { get; set; }
    public string Name { get; }
    public void LoadFromSaveData();
    public INebulaConfigEntry UpdateValue(int value, bool save);

    public void Share()=> NebulaConfigEntryManager.RpcShare.Invoke((Id, CurrentValue));
}


public class NebulaStandardConfigEntry : INebulaConfigEntry
{

    public int CurrentValue { get; protected set; }
    private IntegerDataEntry dataEntry;
    public int Id { get; set; } = -1;
    public string Name { get; set; }

    public NebulaStandardConfigEntry(string id, int defaultValue)
    {
        dataEntry = new IntegerDataEntry(id, NebulaConfigEntryManager.ConfigData, defaultValue);
        LoadFromSaveData();
        NebulaConfigEntryManager.AllConfig.Add(this);
        Name = id;
    }

    public void LoadFromSaveData()
    {
        CurrentValue = dataEntry.Value;
    }

    public INebulaConfigEntry UpdateValue(int value, bool save)
    {
        CurrentValue = value;
        if (save) dataEntry.Value = value;
        return this;
    }
}

public class NebulaStringConfigEntry : INebulaConfigEntry
{

    public int CurrentValue { get; protected set; }
    private StringDataEntry dataEntry;
    public int Id { get; set; } = -1;
    public string Name { get; set; }
    private Func<string, int> mapper;
    private Func<int, string> serializer;

    public NebulaStringConfigEntry(string id, string defaultValue,Func<string,int> mapper, Func<int, string> serializer)
    {
        dataEntry = new StringDataEntry(id, NebulaConfigEntryManager.ConfigData, defaultValue);
        this.mapper = mapper;
        this.serializer = serializer;

        LoadFromSaveData();
        NebulaConfigEntryManager.AllConfig.Add(this);
        Name = id;
    }

    public void LoadFromSaveData()
    {
        CurrentValue = mapper.Invoke(dataEntry.Value);
    }

    public INebulaConfigEntry UpdateValue(int value, bool save)
    {
        CurrentValue = value;
        if (save) dataEntry.Value = serializer.Invoke(value);
        return this;
    }
}



/*
public class ConfigurationHolder
{
    static public List<ConfigurationHolder> AllHolders = new();
    static public void Load()
    {
        AllHolders.Sort((c1, c2) =>
        {
            if (c1.tabMask != c2.tabMask) return c1.tabMask - c2.tabMask;
            if (c1.Priority != c2.Priority) return c1.Priority - c2.Priority;
            return string.Compare(c1.Id, c2.Id);
        });
    }

    private INebulaConfigEntry? entry = null;
    private Func<bool>? predicate = null;
    private int tabMask,gamemodeMask;
    public string Id { get; private set; }
    public int Priority { get; set; }
    public Virial.Text.TextComponent Title { get; private init; }
    private List<NebulaConfiguration> myConfigurations = new();
    public IEnumerable<NebulaConfiguration> MyConfigurations => myConfigurations;
    public IAssignableBase? RelatedAssignable = null;

    private List<ConfigurationTag> myTags = new();
    public IEnumerable<ConfigurationTag> Tags => myTags;
    
    public ConfigurationHolder AddTags(params ConfigurationTag[] tags) { myTags.AddRange(tags); return this; }

    public Func<bool>? IsActivated { get; set; } = null;

    internal void RegisterOption(NebulaConfiguration config) => myConfigurations.Add(config);

    public ConfigurationHolder(string id, Virial.Text.TextComponent? title,int tabMask,int gamemodeMask)
    {
        Id = id;
        AllHolders.Add(this);
        this.tabMask = tabMask;
        this.gamemodeMask = gamemodeMask;
        this.Title = title ?? new TranslateTextComponent(id);
        this.Priority = 0;
    }

    public ConfigurationHolder SetDefaultShownState(bool shownDefault)
    {
        if (entry == null) entry = new NebulaStandardConfigEntry(Id, shownDefault ? 1 : 0);
        return this;
    }

    public ConfigurationHolder SetPredicate(Func<bool> predicate)
    {
        this.predicate = predicate;
        return this;
    }

    public IMetaParallelPlacableOld GetWidget()
    {
        MetaWidgetOld widget = new();
        foreach(var config in myConfigurations)
        {
            if (!config.IsShown) continue;

            var editor = config.GetEditor();
            if (editor != null) widget.Append(editor);
        }
        return widget;
    }

    public int TabMask { get => tabMask; set => tabMask = value; }
    public int GameModeMask => gamemodeMask;
    public bool IsShown => ((entry?.CurrentValue ?? 1) == 1) && (predicate?.Invoke() ?? true);
    public void Toggle()
    {
        if (entry == null) return;
        entry.UpdateValue(entry.CurrentValue == 1 ? 0 : 1, true);
        entry.Share();
    }

    public void GetShownString(ref StringBuilder builder)
    {
        builder ??= new();

        builder.Append(Title.GetString() + "\n");
        foreach (var config in MyConfigurations)
        {
            if (!config.IsShown) continue;

            string? temp = config.GetShownString();
            if (temp == null) continue;

            builder.Append("   " + temp.Replace("\n", "\n      "));
            builder.AppendLine();
        }
    }
}
*/

public class NebulaConfiguration : ValueConfiguration
{
    public class NebulaByteConfiguration
    {
        private NebulaConfiguration myConfiguration;
        private int myIndex;
        private bool defaultValue;
        public string Id { get; private set; }

        public NebulaByteConfiguration(NebulaConfiguration config, string id, int index,bool defaultValue)
        {
            myConfiguration = config;
            myIndex = index;
            this.defaultValue = defaultValue;
            this.Id = id;
        }

        public void ToggleValue()
        {
            myConfiguration.ChangeValue(myConfiguration.CurrentValue ^ (1 << myIndex));
        }

        private bool RawValue => (myConfiguration.CurrentValue & (1 << myIndex)) != 0;
        public bool CurrentValue => RawValue == defaultValue;

        public void ChangeValue(bool value)
        {
            if (value != CurrentValue) ToggleValue();
        }

        public static implicit operator bool(NebulaByteConfiguration config) => config.CurrentValue;
    }

    private INebulaConfigEntry? entry;
    public ConfigurationHolder? MyHolder { get; private set; }
    public Func<object?, string>? Decorator { get; set; } = null;
    public Func<int, object?>? Mapper { get; set; } = null;
    public Func<bool>? Predicate { get; set; } = null;
    public int GameModeMask { get; set; } = CustomGameMode.AllGameModeMask;
    public Func<IMetaWidgetOld?>? Editor { get; set; } = null;
    public Func<string?>? Shower { get; set; }
    public bool LoopAtBothEnds { get; set; } = true;
    public int MaxValue { get; private init; }
    private int InvalidatedValue { get; init; }
    public Virial.Text.TextComponent Title { get; set; }
    public string Id => entry?.Name ?? "Undefined";
    public bool IsShown => (MyHolder?.IsShown ?? true) && ((GeneralConfigurations.CurrentGameMode & GameModeMask) != 0) && (Predicate?.Invoke() ?? true);
    public Action? OnValueChanged = null;

    public static List<NebulaConfiguration> AllConfigurations = new();

    public static GUIWidget? GetDetailWidget(string detailId) {
        var widget = DocumentManager.GetDocument(detailId)?.Build(null, false);

        if (widget == null)
        {
            string? display = Language.Find(detailId);
            if (display != null) widget = new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentStandard), new RawTextComponent(display));
        }

        return widget;
    }

    public void TitlePostBuild(TextMeshPro text, string? detailId)
    {
        GUIWidget? widget = null;

        detailId ??= Id;
        detailId += ".detail";

        widget = GetDetailWidget(detailId);

        if (widget == null) return;

        var buttonArea = UnityHelper.CreateObject<BoxCollider2D>("DetailArea", text.transform, Vector3.zero);
        var button = buttonArea.gameObject.SetUpButton();
        buttonArea.size = text.rectTransform.sizeDelta;
        buttonArea.isTrigger = true;
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, widget));
        button.OnMouseOut.AddListener(()=>NebulaManager.Instance.HideHelpWidget());
    }
    internal IMetaWidgetOld? GetEditor()
    {
        if (Editor != null)
            return Editor.Invoke();
        return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
            new MetaWidgetOld.Text(OptionTitleAttr) { RawText = Title.GetString(), PostBuilder = (text) => TitlePostBuild(text, null) },
            OptionTextColon,
            OptionButtonWidget(() => ChangeValue(false), "<<"),
            new MetaWidgetOld.Text(OptionValueAttr) { RawText = ToDisplayString()},
            OptionButtonWidget(() => ChangeValue(true), ">>")
            );
    }

    internal string? GetShownString() {
        try
        {
            return Shower?.Invoke() ?? null;
        }catch
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Warning, null, Id + " is not printable.");
            return null;
        }
    }

    string? IConfiguration.GetDisplayText() => GetShownString();

    static public TextAttributeOld GetOptionBoldAttr(float width, TMPro.TextAlignmentOptions alignment = TMPro.TextAlignmentOptions.Center) => new(TextAttributeOld.BoldAttr)
    {
        FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
        Size = new Vector2(width, 0.4f),
        Alignment = alignment
    };
    static public TextAttributeOld OptionTitleAttr = GetOptionBoldAttr(4f,TMPro.TextAlignmentOptions.Left);
    static public TextAttributeOld OptionValueAttr = GetOptionBoldAttr(1.1f);
    static public TextAttributeOld OptionShortValueAttr = GetOptionBoldAttr(0.7f);
    static public TextAttributeOld OptionButtonAttr = new(TextAttributeOld.BoldAttr) {
        FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
        Size = new Vector2(0.32f, 0.22f) 
    };
    static public MetaWidgetOld.Button OptionButtonWidget(Action clickAction,string rawText,float? width = null) {
        return new MetaWidgetOld.Button(() =>
        {
            clickAction();
            if (NebulaSettingMenu.Instance) NebulaSettingMenu.Instance.UpdateSecondaryPage();
        }, width.HasValue ? new(OptionButtonAttr) { Size = new(width.Value, 0.22f) } : OptionButtonAttr)
        {
            RawText = rawText,
            PostBuilder = (button, renderer, text) => { renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; }
        };
    }
    static public IMetaParallelPlacableOld OptionTranslatedText(string translationKey, float width) => new MetaWidgetOld.Text(new(OptionTitleAttr) { Size = new Vector2(width, 0.4f), Alignment = TMPro.TextAlignmentOptions.Center }) { TranslationKey = translationKey };
    static public IMetaParallelPlacableOld OptionRawText(string text, float width) => new MetaWidgetOld.Text(new(OptionTitleAttr) { Size = new Vector2(width, 0.4f), Alignment = TMPro.TextAlignmentOptions.Center }) { RawText = text };
    static public IMetaParallelPlacableOld OptionTextColon => OptionRawText(":",0.2f);


    static public Func<object?, string> PercentageDecorator = (mapped) => mapped + Language.Translate("options.percentage");
    static public Func<object?, string> OddsDecorator = (mapped) => mapped + Language.Translate("options.cross");
    static public Func<object?, string> SecDecorator = (mapped) => mapped + Language.Translate("options.sec");
    static public Func<IMetaWidgetOld?> EmptyEditor = () => null;

    public string DefaultShowerString => Title.GetString() + " : " + ToDisplayString();
    
    public NebulaConfiguration(ConfigurationHolder? holder, Func<NebulaConfiguration> referTo)
    {

    }

    public NebulaConfiguration(ConfigurationHolder? holder, Func<IMetaWidgetOld?> editor, Func<string?>? shower = null)
    {
        MyHolder = holder;
        MyHolder?.RegisterOption(this);
        Editor = editor;

        entry = null;
        Title = new RawTextComponent("Undefined");

        Shower = shower;

        AllConfigurations.Add(this);
    }

    public NebulaConfiguration ReplaceTitle(string translationKey, bool withHolderPrefix = true)
    {
        if (withHolderPrefix) translationKey = MyHolder.Id + "." + translationKey;
        Title = new TranslateTextComponent(translationKey);
        return this;
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, int maxValue,int defaultValue,int invalidatedValue)
    {
        MaxValue = maxValue;
        defaultValue = Mathf.Clamp(defaultValue,0,maxValue);
        InvalidatedValue = Mathf.Clamp(invalidatedValue, 0, maxValue);

        MyHolder = holder;
        MyHolder?.RegisterOption(this);

        string entryId = id;
        if (holder != null) entryId = holder.Id + "." + entryId;

        entry = new NebulaStandardConfigEntry(entryId, defaultValue);
        var clampVal = Mathf.Clamp(entry.CurrentValue, 0, maxValue);
        if (entry.CurrentValue != clampVal) entry.UpdateValue(clampVal, false);

        Title = title ?? new TranslateTextComponent(entryId);

        Shower = () => DefaultShowerString;

        AllConfigurations.Add(this);
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, int minValue,int maxValue, int defaultValue, int invalidatedValue) :
        this(holder, id, title, maxValue-minValue, defaultValue-minValue, invalidatedValue - minValue)
    {
        Mapper = (i) => i + minValue;
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, bool defaultValue, bool invalidatedValue) :
        this(holder, id, title, 1, defaultValue ? 1 : 0, invalidatedValue ? 1 : 0)
    {
        Mapper = (i) => i == 1;
        Decorator = (v) => Language.Translate((bool)v! ? "options.switch.on" : "options.switch.off");
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, object?[] selections, object? defaultValue, object? invalidatedValue,Func<object?,string> decorator) :
        this(holder, id, title, selections.Count() - 1, Array.IndexOf(selections, defaultValue), Array.IndexOf(selections, invalidatedValue))
    {
        Mapper = (i) => selections[i];
        Decorator = decorator;
    }

    public NebulaConfiguration(ConfigurationHolder? holder,string id, Virial.Text.TextComponent? title, string[] selections,string defaultValue,string invalidatedValue):
        this(holder,id, title, selections.Length-1,Array.IndexOf(selections,defaultValue), Array.IndexOf(selections, invalidatedValue))
    {
        Mapper = (i) => selections[i];
        Decorator = (v) => Language.Translate((string?)v);
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, string[] selections, int defaultIndex,int invalidatedIndex) :
       this(holder, id, title, selections.Length - 1, defaultIndex, invalidatedIndex)
    {
        Mapper = (i) => selections[i];
        Decorator = (v) => Language.Translate((string?)v);
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, float[] selections, float defaultValue, float invalidatedValue) :
        this(holder, id, title, selections.Length - 1, Array.IndexOf(selections, defaultValue), Array.IndexOf(selections, invalidatedValue))
    {
        Mapper = (i) => selections[i];
    }

    public NebulaConfiguration(ConfigurationHolder? holder, string id, Virial.Text.TextComponent? title, float min,float max,float step, float defaultValue, float invalidatedValue) :
        this(holder, id, title, (int)((max - min) / step), (int)((defaultValue-min)/step), (int)((invalidatedValue - min) / step))
    {
        Mapper = (i) => (float)(step * i + min);
    }

    public void ChangeAs(string mapped,bool share)
    {
        if (MaxValue >= 128)
        {
            ChangeValue(int.TryParse(mapped, out var num) ? num : 0,share);
        }
        else
        {
            for (int i = 0; i <= MaxValue; i++)
            {
                if ((GetMapped(i)?.ToString() ?? "null").Equals(mapped))
                {
                    ChangeValue(i, share);
                    break;
                }
            }
        }
    }

    public void ChangeValue(bool increment)
    {
        if (entry == null) return;
        var current = entry.CurrentValue;
        current += increment ? 1 : -1;
        if (LoopAtBothEnds)
        {
            if (current < 0) current = MaxValue;
            if (current > MaxValue) current = 0;
        }
        else
            current = Mathf.Clamp(current, 0, MaxValue);
        entry.UpdateValue(current, true);
        OnValueChanged?.Invoke();
        entry.Share();
    }

    public void ChangeValue(int newValue,bool share = true)
    {
        if (entry == null) return;
        entry.UpdateValue(Mathf.Clamp(newValue, 0, MaxValue), true);
        OnValueChanged?.Invoke();
        if (share) entry.Share();
    }

    public int CurrentValue => (IsShown && entry != null) ? entry.CurrentValue : InvalidatedValue;

    //各種Predicateを通さず、設定の生の値を取得します。
    public int CurrentUncheckedValue => entry?.CurrentValue ?? -1;

    public object? GetMapped() => GetMapped(CurrentValue);

    private object? GetMapped(int currentValue)
    {
        return Mapper != null ? Mapper.Invoke(currentValue) : currentValue;
    }

    public int GetMappedInt()
    {
        return (int)GetMapped()!;
    }

    public float GetFloat()
    {
        return (float)GetMapped()!;
    }

    public string GetString()
    {
        return GetMapped()?.ToString()!;
    }

    public bool GetBool()
    {
        return (GetMapped() as bool?) ?? false;
    }

    public string ToDisplayString()
    {
        return Decorator?.Invoke(GetMapped()) ?? GetString() ?? "None";
    }

    public static implicit operator bool(NebulaConfiguration config) => config.GetBool();
    public static implicit operator int(NebulaConfiguration config) => config.GetMappedInt();
}

public class CustomGameMode
{
    public static List<CustomGameMode> allGameMode = new List<CustomGameMode>();
    public static CustomGameMode Standard = new CustomGameMode(0x01, "gamemode.standard", () => new StandardRoleAllocator(), 4) { AllowSpecialEnd = true }
        .AddEndCriteria(NebulaEndCriteria.SabotageCriteria)
        .AddEndCriteria(NebulaEndCriteria.ImpostorKillCriteria)
        .AddEndCriteria(NebulaEndCriteria.CrewmateAliveCriteria)
        .AddEndCriteria(NebulaEndCriteria.CrewmateTaskCriteria)
        .AddEndCriteria(NebulaEndCriteria.JackalKillCriteria);
    public static CustomGameMode HostMode = new CustomGameMode(0x04, "gamemode.hostMode", () => new StandardRoleAllocator(), 4, true) { AllowSpecialEnd = true, AllowWithoutNoS = true }
        .AddEndCriteria(NebulaEndCriteria.SabotageCriteria)
        .AddEndCriteria(NebulaEndCriteria.ImpostorKillCriteria)
        .AddEndCriteria(NebulaEndCriteria.CrewmateAliveCriteria)
        .AddEndCriteria(NebulaEndCriteria.CrewmateTaskCriteria)
        .AddEndCriteria(NebulaEndCriteria.JackalKillCriteria);
    public static CustomGameMode FreePlay = new CustomGameMode(0x02, "gamemode.freeplay", () => new FreePlayRoleAllocator(), 0) { AllowWithoutNoS = false/*true*/ };
    public static BitMask<GameModeDefinition> AllGameModeMask = new BitMask32<GameModeDefinition>(t => t.AsBit, Standard | HostMode | FreePlay);
    public static BitMask<GameModeDefinition> AllNormalGameModeMask = new BitMask32<GameModeDefinition>(t => t.AsBit, Standard | HostMode);
    public static BitMask<GameModeDefinition> AllClientGameModeMask = new BitMask32<GameModeDefinition>(t => t.AsBit, Standard | FreePlay);

    private int bitFlag;
    public string TranslateKey { get; private init; }
    public Func<IRoleAllocator> RoleAllocator { get; private init; }
    public List<NebulaEndCriteria> GameModeCriteria { get; private init; } = new();
    public int MinPlayers { get; private init; }
    public bool AllowSpecialEnd { get; private init; } = false;
    public bool AllowWithoutNoS { get; private init; } = false;
    public CustomGameMode(int bitFlag,string translateKey, Func<IRoleAllocator> roleAllocator, int minPlayers, bool skip = false)
    {
        this.bitFlag = bitFlag;
        this.RoleAllocator = roleAllocator;
        if(!skip) allGameMode.Add(this);
        this.TranslateKey = translateKey;
        MinPlayers = minPlayers;
    }

    private CustomGameMode AddEndCriteria(NebulaEndCriteria criteria)
    {
        GameModeCriteria.Add(criteria);
        return this;
    }
    public static ReadOnlyCollection<CustomGameMode> AllGameMode { get => allGameMode.AsReadOnly(); }

    public static implicit operator int(CustomGameMode gamemode) => gamemode.bitFlag;
}