using Epic.OnlineServices;
using Il2CppSystem.ComponentModel;
using Il2CppSystem.Runtime.Remoting.Messaging;
using JetBrains.Annotations;
using MS.Internal.Xml.XPath;
using Nebula.Modules;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Nebula.Roles.Assignment;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Virial.Assignable;
using Virial.Configuration;
using static Il2CppMono.Security.X509.X520;
using static Il2CppSystem.Linq.Expressions.Interpreter.NullableMethodCallInstruction;

namespace Nebula.Configuration;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NebulaOptionHolder : Attribute
{
}


[NebulaPreLoad(typeof(Roles.Roles))]
[NebulaRPCHolder]
public class NebulaConfigEntryManager
{
    static public DataSaver ConfigData = new DataSaver("Config");
    static public List<INebulaConfigEntry> AllConfig = new();

    static public IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Building Configuration Database";
        yield return null;

        var types = Assembly.GetAssembly(typeof(RemoteProcessBase))?.GetTypes().Where((type) => type.IsDefined(typeof(NebulaOptionHolder)));
        if (types == null) yield break;

        foreach (var type in types) System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        AllConfig.Sort((c1, c2) => string.Compare(c1.Name, c2.Name));

        for (int i = 0; i < AllConfig.Count; i++) AllConfig[i].Id = i;

        ConfigurationHolder.Load();
    }

    static public RemoteProcess<(int id, int value)> RpcShare = new(
        "ShareOption",
       (message, isCalledByMe) =>
       {
           if (!isCalledByMe) AllConfig[message.id].UpdateValue(message.value, false);
       }
    );

    //呼び出し時の引数は使用していない
    static private DivisibleRemoteProcess<int, Tuple<int, int>> RpcShareAll = new DivisibleRemoteProcess<int, Tuple<int, int>>(
        "ShareAllOption",
        (message) =>
        {
            //(Item1)番目から(Item2)-1番目まで
            IEnumerator<Tuple<int, int>> GetDivider()
            {
                int done = 0;
                while (done < AllConfig.Count)
                {
                    int max = Mathf.Min(AllConfig.Count, done + 100);
                    yield return new Tuple<int, int>(done, max);
                    done = max;
                }
            }
            return GetDivider();
        },
        (writer, message) =>
        {
            writer.Write(message.Item1);
            writer.Write(message.Item2 - message.Item1);
            for (int i = message.Item1; i < message.Item2; i++) writer.Write(AllConfig[i].CurrentValue);
        },
       (reader) =>
       {
           int index = reader.ReadInt32();
           int num = reader.ReadInt32();
           for (int i = 0; i < num; i++)
           {
               AllConfig[index].UpdateValue(reader.ReadInt32(), false);
               index++;
           }
           return new Tuple<int, int>(0, 0);
       },
       (message, isCalledByMe) =>
       {
           //メッセージを受け取ったときに処理しているのでここでは何もしない
       }
    );

    static public void ShareAll()
    {
        RpcShareAll.Invoke(0);
    }

    static public void RestoreAll()
    {
        foreach (var cfg in AllConfig) cfg.LoadFromSaveData();
    }

    static public void RestoreAllAndShare()
    {
        RestoreAll();
        ShareAll();
    }

}

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

public class NebulaAssignableFilterConfigEntry<T> where T : class, IAssignableBase, ICodeName
{
    class FilterEntry : INebulaConfigEntry
    {
        NebulaAssignableFilterConfigEntry<T> myConfig { get; init; }
        public int CurrentValue { get; protected set; }
        public int Id { get; set; } = -1;
        public string Name { get; set; }
        public int Index { get; private set; }

        public FilterEntry(NebulaAssignableFilterConfigEntry<T> config, int index)
        {
            myConfig = config;
            Index = index;
            Name = myConfig.Id + "." + index;
            LoadFromSaveData();
            NebulaConfigEntryManager.AllConfig.Add(this);
        }

        public void LoadFromSaveData()
        {
            CurrentValue = myConfig.LoadValue(Index);
        }

        public INebulaConfigEntry UpdateValue(int value, bool save)
        {
            CurrentValue = value;
            if (save) myConfig.SaveValue(Index, value);
            return this;
        }
    }

    private StringArrayDataEntry dataEntry;
    private HashSet<T> assignables = new();
    private FilterEntry[] sharingEntry;

    public string Id { get; private set; }
    public NebulaAssignableFilterConfigEntry(string id, string[] defaultValue)
    {
        Id = id;
        dataEntry = new StringArrayDataEntry(id, NebulaConfigEntryManager.ConfigData, defaultValue);
        assignables = new();

        T[] allAssignable = Roles.Roles.AllAssignables().Select(m => m as T).Where(m => m != null).ToArray()!;

        foreach (var name in dataEntry.Value)
        {
            var r = allAssignable.FirstOrDefault(e => e.CodeName == name);
            if(r != null) assignables.Add(r);
        }

        sharingEntry = new FilterEntry[allAssignable.Length / 30 + 1];
        for (int i = 0; i < sharingEntry.Length; i++) sharingEntry[i] = new(this, i);

    }

    private void Save()
    {
        dataEntry.Value = assignables.Select(m => m.CodeName).ToArray();
    }

    public void SaveValue(int index, int value)
    {
        //該当要素を全削除
        assignables.RemoveWhere(m => (index * 30) <= m.Id && m.Id < (index + 1) * 30);

        for (int i = 0; i < 30; i++)
        {
            //追加するべき役職
            if (((1 << i) & value) != 0 && !assignables.Any(a => a.Id == (i + index * 30))) assignables.Add((Roles.Roles.AllAssignables().FirstOrDefault(a => a is T && a.Id == (i+index*30)) as T)!);
        }

        Save();
    }
    public bool Contains(T assignable) => assignables.Contains(assignable);

    public int LoadValue(int index)
    {
        int value = 0;
        foreach(var m in assignables)
            if ((index * 30) <= m.Id && m.Id < (index + 1) * 30) value |= 1 << m.Id - (index * 30);
        return value;
    }

    public void ToggleAndShare(T assignable)
    {
        if (assignables.Contains(assignable))
            assignables.Remove(assignable);
        else
            assignables.Add(assignable);

        foreach (INebulaConfigEntry config in sharingEntry)
        {
            config.LoadFromSaveData();
            config.Share();
        }

        Save();
    }


    public bool Test(T? assignable)
    {
        if(assignable != null) return !Contains(assignable);
        return false;
    }
}

public class NebulaModifierFilterConfigEntry : NebulaAssignableFilterConfigEntry<IntroAssignableModifier>, ModifierFilter
{
    public NebulaModifierFilterConfigEntry(string id, string[] defaultValue) : base(id,defaultValue) { }

    bool ModifierFilter.Test(DefinedModifier modifier) => Test(modifier as IntroAssignableModifier);
}

public class NebulaGhostRoleFilterConfigEntry : NebulaAssignableFilterConfigEntry<AbstractGhostRole>
{
    public NebulaGhostRoleFilterConfigEntry(string id, string[] defaultValue) : base(id, defaultValue) { }

    //bool GhostRoleFilter.Test(DefinedGhostRole role) => Test(role as AbstractGhostRole);
}


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

    static private IDividedSpriteLoader TagSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ConfigurationTag.png", 100f, 42, 42, true);

    static private GUIWidget GetTagTextWidget(string translationKey) => new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new TranslateTextComponent("configuration.tag." + translationKey));
    static public ConfigurationTag TagChaotic { get; private set; } = new(TagSprite.AsLoader(0), GetTagTextWidget("chaotic"));
    static public ConfigurationTag TagBeginner { get; private set; } = new(TagSprite.AsLoader(1), GetTagTextWidget("beginner"));
    static public ConfigurationTag TagFunny { get; private set; } = new(TagSprite.AsLoader(4), GetTagTextWidget("funny"));
    static public ConfigurationTag TagDifficult { get; private set; } = new(TagSprite.AsLoader(5), GetTagTextWidget("difficult"));
    static public ConfigurationTag TagSNR { get; private set; } = new(TagSprite.AsLoader(2), GetTagTextWidget("superNewRoles"));
}


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
    public IMetaWidgetOld? GetEditor()
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

    public string? GetShownString() {
        try
        {
            return Shower?.Invoke() ?? null;
        }catch
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Warning, null, Id + " is not printable.");
            return null;
        }
    }

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

    string ValueConfiguration.CurrentValue => GetString();
    float ValueConfiguration.AsFloat() => GetFloat();
    int ValueConfiguration.AsInt() => GetMappedInt();
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

    bool ValueConfiguration.UpdateValue(int value)
    {
        for(int i= 0; i <= MaxValue; i++)
        {
            var val = (Mapper?.Invoke(i) ?? i) as int?;
            if (val.HasValue && val.Value == value)
            {
                ChangeValue(i);
                return true;
            }
        }
        return false;
    }

    bool ValueConfiguration.UpdateValue(float value)
    {
        for (int i = 0; i <= MaxValue; i++)
        {
            var val = Mapper?.Invoke(i) as float?;
            if (val.HasValue && val.Value == value)
            {
                ChangeValue(i);
                return true;
            }
        }
        return false;
    }

    bool ValueConfiguration.UpdateValue(bool value)
    {
        for (int i = 0; i <= MaxValue; i++)
        {
            var val = Mapper?.Invoke(i) as bool?;
            if (val.HasValue && val.Value == value)
            {
                ChangeValue(i);
                return true;
            }
        }
        return false;
    }

    bool ValueConfiguration.UpdateValue(string value)
    {
        for (int i = 0; i <= MaxValue; i++)
        {
            var val = Mapper?.Invoke(i) as string;
            if (val == value)
            {
                ChangeValue(i);
                return true;
            }
        }
        return false;
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
    public static int AllGameModeMask = Standard | HostMode | FreePlay;
    public static int AllNormalGameModeMask = Standard | HostMode;
    public static int AllClientGameModeMask = Standard | FreePlay;

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

public class ConfigurationTab
{
    public static List<ConfigurationTab> allTab = new List<ConfigurationTab>();
    public static ConfigurationTab Settings = new ConfigurationTab(0x01,"options.tab.setting",new Color(0.75f,0.75f,0.75f));
    public static ConfigurationTab CrewmateRoles = new ConfigurationTab(0x02, "options.tab.crewmate", Palette.CrewmateBlue);
    public static ConfigurationTab ImpostorRoles = new ConfigurationTab(0x04, "options.tab.impostor", Palette.ImpostorRed);
    public static ConfigurationTab NeutralRoles = new ConfigurationTab(0x08, "options.tab.neutral", new Color(244f / 255f, 211f / 255f, 53f / 255f));
    public static ConfigurationTab GhostRoles = new ConfigurationTab(0x10, "options.tab.ghost", new Color(150f / 255f, 150f / 255f, 150f / 255f));
    public static ConfigurationTab Modifiers = new ConfigurationTab(0x20, "options.tab.modifier", new Color(255f / 255f, 255f / 255f, 243f / 255f));    

    private int bitFlag;
    private string translateKey { get; init; }
    public Color Color { get; private init; }
    public ConfigurationTab(int bitFlag, string translateKey,Color color)
    {
        this.bitFlag = bitFlag;
        allTab.Add(this);
        this.translateKey = translateKey;
        Color = color;
    }

    public string DisplayName { get => Language.Translate(translateKey).Replace("[", StringHelper.ColorBegin(Color)).Replace("]", StringHelper.ColorEnd()); }
    public static ReadOnlyCollection<ConfigurationTab> AllTab { get => allTab.AsReadOnly(); }

    public static ConfigurationTab FromRoleCategory(RoleCategory roleCategory)
    {
        switch (roleCategory)
        {
            case RoleCategory.CrewmateRole:
                return CrewmateRoles;
            case RoleCategory.ImpostorRole:
                return ImpostorRoles;
            case RoleCategory.NeutralRole:
                return NeutralRoles;
        }
        return Settings;
    }

    public static implicit operator int(ConfigurationTab tab) => tab.bitFlag;
    
}