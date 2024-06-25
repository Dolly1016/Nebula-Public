﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;
using Virial;
using Nebula.Modules.GUIWidget;
using Epic.OnlineServices.Platform;
using static Il2CppMono.Security.X509.X520;
using Virial.Game;

namespace Nebula.Configuration;

static internal class ConfigurationAssets
{
    static internal TextComponent LeftArrow = new RawTextComponent("<<");
    static internal TextComponent RightArrow = new RawTextComponent(">>");
    static internal GUIWidgetSupplier? GetOptionOverlay(string id)
    {
        string detailId = id + ".detail";

        var widget = DocumentManager.GetDocument(detailId)?.Build(null);

        if (widget == null)
        {
            string? display = Nebula.Modules.Language.Find(detailId);
            if (display != null) widget = new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentStandard), new RawTextComponent(display));
        }

        if (widget == null) return null;
        return widget;
    }
}

internal class BoolConfigurationImpl : Virial.Configuration.BoolConfiguration
{
    internal TextComponent Title;
    private GUIWidgetSupplier editor;
    private ISharableVariable<bool> val;

    public BoolConfigurationImpl(string id, bool defaultValue)
    {
        this.Title = new TranslateTextComponent(id);
        this.val = new BoolConfigurationValue(id, defaultValue);
        this.editor = () => new HorizontalWidgetsHolder(GUIAlignment.Left,
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), this.Title) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(id) },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), new RawTextComponent(":")),
            new NoSGUIMargin(GUIAlignment.Center, new(0.1f, 0f)),
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButtonMedium), new LazyTextComponent(() => ValueAsDisplayString)) { OnClick = _ => { UpdateValue(!GetValue()); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }, Color = Color.Lerp(Color.white, GetValue() ? Color.cyan : Color.red ,0.65f) }
            );
    }

    private string ValueAsDisplayString => NebulaAPI.instance.Language.Translate(val.CurrentValue ? "options.switch.on" : "options.switch.off");
    internal override string? GetDisplayText() => Title.GetString() + ": " + ValueAsDisplayString.Color(Color.Lerp(Color.white, GetValue() ? Color.cyan : Color.red, 0.65f));

    internal Func<bool>? Predicate = null;
    internal override bool IsShown => Predicate?.Invoke() ?? true;
    internal BoolConfigurationImpl SetPredicate(Func<bool>? predicate) {  Predicate = predicate; return this; }

    internal override GUIWidgetSupplier GetEditor() => editor;

    internal override bool GetValue() => val.CurrentValue;

    internal override void UpdateValue(bool value) => val.CurrentValue = value;
    internal override void ChangeValue(bool increase, bool loopAtTerminal = true) => val.CurrentValue = !val.CurrentValue;
}

internal class IntegerConfigurationImpl : Virial.Configuration.IntegerConfiguration
{
    internal TextComponent Title;
    private GUIWidgetSupplier editor;
    private IOrderedSharableVariable<int> val;

    public IntegerConfigurationImpl(string id, int[] selection, int defaultValue, TextComponent? title)
    {
        this.Title = title ?? new TranslateTextComponent(id);
        this.val = new IntegerConfigurationValue(id, selection, defaultValue);
        this.editor = () => new HorizontalWidgetsHolder(GUIAlignment.Left,
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), this.Title) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(id) },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), new RawTextComponent(":")),
            new NoSGUIMargin(GUIAlignment.Center, new(0.1f, 0f)),
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsValue), new LazyTextComponent(() => ValueAsDisplayString)),
            new GUISpinButton(GUIAlignment.Center, v => { val.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
            );
    }

    public IntegerConfigurationImpl DecorateAsRatioConfiguration()
    {
        this.Decorator = (val) => val + Language.Translate("options.percentage");
        return this;
    }

    protected virtual string ValueAsDisplayString { get => Decorator?.Invoke(val.CurrentValue) ?? val.CurrentValue.ToString(); }
    internal override string? GetDisplayText() => Title.GetString() + ": " + ValueAsDisplayString;
    internal Func<int, string>? Decorator = null;

    internal Func<bool>? Predicate = null;
    internal override bool IsShown => Predicate?.Invoke() ?? true;
    internal IntegerConfigurationImpl SetPredicate(Func<bool>? predicate) { Predicate = predicate; return this; }


    internal override GUIWidgetSupplier GetEditor() => editor;

    internal override int GetValue() => val.CurrentValue;

    internal override void UpdateValue(int value) => val.CurrentValue = value;
    internal override void ChangeValue(bool increase, bool loopAtTerminal = true) => val.ChangeValue(increase, loopAtTerminal);
}

internal class FloatConfigurationImpl : Virial.Configuration.FloatConfiguration
{
    internal TextComponent Title;
    private GUIWidgetSupplier editor;
    private IOrderedSharableVariable<float> val;

    public FloatConfigurationImpl(string id, float[] selection, float defaultValue, TextComponent? title)
    {
        this.Title = title ?? new TranslateTextComponent(id);
        this.val = new FloatConfigurationValue(id, selection, defaultValue);
        this.editor = () => new HorizontalWidgetsHolder(GUIAlignment.Left,
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), this.Title) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(id) },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), new RawTextComponent(":")),
            new NoSGUIMargin(GUIAlignment.Center, new(0.1f, 0f)),
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsValue), new LazyTextComponent(() => ValueAsDisplayString)),
            new GUISpinButton(GUIAlignment.Center, v => { val.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
            );
    }

    public FloatConfigurationImpl DecorateAsSecConfiguration()
    {
        this.Decorator = (val) => val + Language.Translate("options.sec");
        return this;
    }

    public FloatConfigurationImpl DecorateAsRatioConfiguration()
    {
        this.Decorator = (val) => val + Language.Translate("options.cross");
        return this;
    }

    protected virtual string ValueAsDisplayString { get => Decorator?.Invoke(val.CurrentValue) ?? val.CurrentValue.ToString(); }
    internal override string? GetDisplayText() => Title.GetString() + ": " + ValueAsDisplayString;
    internal Func<float, string>? Decorator = null;

    internal Func<bool>? Predicate = null;
    internal override bool IsShown => Predicate?.Invoke() ?? true;
    internal FloatConfigurationImpl SetPredicate(Func<bool>? predicate) { Predicate = predicate; return this; }

    internal override GUIWidgetSupplier GetEditor() => editor;

    internal override float GetValue() => val.CurrentValue;

    internal override void UpdateValue(float value) => val.CurrentValue = value;
    internal override void ChangeValue(bool increase, bool loopAtTerminal = true) => val.ChangeValue(increase, loopAtTerminal);
}

internal class StringConfigurationImpl : Virial.Configuration.ValueConfiguration<int>
{
    internal TextComponent Title;
    private GUIWidgetSupplier editor;
    private IOrderedSharableVariable<int> val;
    private string[] mySelection;

    public StringConfigurationImpl(string id, string[] selection, int defaultIndex, TextComponent? title)
    {
        this.mySelection = selection;
        this.Title = title ?? new TranslateTextComponent(id);
        this.val = new SelectionConfigurationValue(id, defaultIndex, selection.Length);
        this.editor = () => new HorizontalWidgetsHolder(GUIAlignment.Left,
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), this.Title) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(id) },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsFlexible), new RawTextComponent(":")),
            new NoSGUIMargin(GUIAlignment.Center, new(0.1f,0f)),
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButton), ConfigurationAssets.LeftArrow) { OnClick = _ => { val.ChangeValue(false, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); } },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsValue), new LazyTextComponent(() => ValueAsDisplayString)),
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButton), ConfigurationAssets.RightArrow) { OnClick = _ => { val.ChangeValue(true, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); } }
            );
    }

    protected virtual string ValueAsDisplayString => Language.Translate(mySelection[val.CurrentValue]);

    internal Func<bool>? Predicate = null;
    bool IConfiguration.IsShown => Predicate?.Invoke() ?? true;
    internal StringConfigurationImpl SetPredicate(Func<bool>? predicate) { Predicate = predicate; return this; }

    string? IConfiguration.GetDisplayText() => Title.GetString() + ": " + ValueAsDisplayString;

    GUIWidgetSupplier IConfiguration.GetEditor() => editor;

    int ValueConfiguration<int>.GetValue() => val.CurrentValue;

    void ValueConfiguration<int>.UpdateValue(int value) => val.CurrentValue = value;
    void ValueConfiguration<int>.ChangeValue(bool increase, bool loopAtTerminal) => val.ChangeValue(increase, loopAtTerminal);
}

internal class RoleCountConfiguration : IntegerConfigurationImpl
{
    static private int[] GetRoleCountMapper(int maxCount)
    {
        int[] mapper = new int[maxCount + 1];
        for(int i = 0; i < mapper.Length; i++) mapper[i] = i - 1;
        return mapper;
    }
    public RoleCountConfiguration(string id, int maxCount, int defaultValue) : base(id, GetRoleCountMapper(maxCount), defaultValue, null) { }

    protected override string ValueAsDisplayString => (this as ValueConfiguration<int>).GetValue() == -1 ? Nebula.Modules.Language.Translate("options.assignment.unlimited")  : base.ValueAsDisplayString;
}

public class EditorConfiguration : IConfiguration
{
    Func<bool> predicate;
    Func<string?> shower;
    private GUIWidgetSupplier editor;

    public EditorConfiguration(Func<string?> shower, GUIWidgetSupplier editor, Func<bool>? predicate = null)
    {
        this.predicate = predicate ?? (() => true);
        this.shower = shower;
        this.editor = editor;
    }

    bool IConfiguration.IsShown => predicate.Invoke();

    string? IConfiguration.GetDisplayText() => shower.Invoke();

    GUIWidgetSupplier IConfiguration.GetEditor() => editor;
}