using Virial.Attributes;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;
using static Virial.Attributes.NebulaPreprocess;

namespace Virial.Configuration;

public class ConfigurationTag
{
    public Media.Image Image { get; private init; }
    public GUIWidgetSupplier Overlay { get; private init; }

    public ConfigurationTag(Image image, GUIWidgetSupplier overlay)
    {
        Image = image;
        Overlay = overlay;
    }
    
}

/// <summary>
/// 設定項目を表します。
/// </summary>
public interface IConfiguration
{
    /// <summary>
    /// 設定画面上の編集用GUI定義を返します。
    /// </summary>
    /// <returns></returns>
    GUIWidgetSupplier GetEditor();

    /// <summary>
    /// ヘルプ画面等に向けたテキストによる設定の表示を表します。
    /// </summary>
    /// <returns></returns>
    string? GetDisplayText();

    /// <summary>
    /// このオプションが現在表示されうるかどうかを返します。
    /// </summary>
    bool IsShown { get; }
}

internal record ConfigurationUpperButton(TextComponent text, Func<bool> predicate, Action onClicked);

public enum ConfigurationHolderState
{
    /// <summary>
    /// ホルダは暗く表示されます。
    /// </summary>
    Inactivated,
    /// <summary>
    /// ホルダは通常通り表示されます。
    /// </summary>
    Activated,
    /// <summary>
    /// ホルダは強調表示されます。
    /// </summary>
    Emphasized
}

public interface IConfigurationHolder
{
    TextComponent Title { get; set; }
    TextComponent Detail { get; set; }
    IEnumerable<IConfiguration> Configurations { get; }
    BitMask<ConfigurationTab> Tabs { get; }
    BitMask<GameModeDefinition> GameModes { get; }
    IEnumerable<ConfigurationTag> Tags { get; }

    /// <summary>
    /// ホルダ内にオプションを追加します。
    /// </summary>
    /// <param name="configuration"></param>
    IConfigurationHolder AppendConfiguration(IConfiguration configuration);

    /// <summary>
    /// ホルダ内にオプションを追加します。
    /// </summary>
    /// <param name="configuration"></param>
    IConfigurationHolder AppendConfigurations(IEnumerable<IConfiguration> configuration);

    IConfigurationHolder AddTags(params ConfigurationTag[] tags);

    /// <summary>
    /// 関連するアクションを追加します。
    /// </summary>
    /// <param name="label"></param>
    /// <param name="onClicked"></param>
    /// <returns></returns>
    IConfigurationHolder AppendRelatedAction(TextComponent label, Func<bool> predicate, Action onClicked);

    /// <summary>
    /// 関連するホルダを設定します。
    /// </summary>
    /// <param name="holders"></param>
    IConfigurationHolder AppendRelatedHolders(params IConfigurationHolder[] holders);

    /// <summary>
    /// 関連するホルダの登録を予約します。
    /// </summary>
    /// <param name="holder"></param>
    IConfigurationHolder ScheduleAddRelated(Func<IConfigurationHolder[]> holder)
    {
        NebulaAPI.Preprocessor?.SchedulePreprocess(PreprocessPhase.PostFixStructure, () => AppendRelatedHolders(holder.Invoke()));
        return this;
    }

    /// <summary>
    /// 現在、編集可能な設定かどうかを調べます。
    /// </summary>
    bool IsShown { get; }
    /// <summary>
    /// 設定の表示の仕方を返します。
    /// </summary>
    ConfigurationHolderState DisplayOption { get; }
    Image? Illustration { get; set; }

    internal IEnumerable<ConfigurationUpperButton> RelatedInformation { get; }

    void SetDisplayState(Func<ConfigurationHolderState> state);
}

/// <summary>
/// 値を持つオプションです。
/// </summary>
public interface ValueConfiguration<T> : IConfiguration
{
    /// <summary>
    /// 値を取得します。
    /// </summary>
    /// <returns></returns>
    T GetValue();

    /// <summary>
    /// 値を更新します。
    /// </summary>
    /// <param name="value"></param>
    void UpdateValue(T value);

    /// <summary>
    /// 値を更新します。
    /// </summary>
    void ChangeValue(bool increase, bool loopAtTerminal = true);
}

/// <summary>
/// 真偽値を格納するオプションです。
/// </summary>
public abstract class BoolConfiguration : ValueConfiguration<bool>
{
    internal abstract string? GetDisplayText();
    internal abstract GUIWidgetSupplier GetEditor();
    internal abstract bool GetValue();
    internal abstract void UpdateValue(bool value);
    internal abstract void ChangeValue(bool increase, bool loopAtTerminal = true);
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    bool ValueConfiguration<bool>.GetValue() => GetValue();

    void ValueConfiguration<bool>.UpdateValue(bool value) => UpdateValue(value);
    void ValueConfiguration<bool>.ChangeValue(bool increase, bool loopAtTerminal) => ChangeValue(increase, loopAtTerminal);
    bool IConfiguration.IsShown => IsShown;

    /// <summary>
    /// 真偽値への暗黙的なキャストができます。
    /// </summary>
    /// <param name="config"></param>

    public static implicit operator bool(BoolConfiguration config) => config.GetValue();
}

/// <summary>
/// 整数値を格納するオプションです。
/// </summary>
public abstract class IntegerConfiguration : ValueConfiguration<int>
{
    internal abstract string? GetDisplayText();
    internal abstract GUIWidgetSupplier GetEditor();
    internal abstract int GetValue();
    internal abstract void UpdateValue(int value);
    internal abstract void ChangeValue(bool increase, bool loopAtTerminal = true);
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    int ValueConfiguration<int>.GetValue() => GetValue();

    void ValueConfiguration<int>.UpdateValue(int value) => UpdateValue(value);
    void ValueConfiguration<int>.ChangeValue(bool increase, bool loopAtTerminal) => ChangeValue(increase, loopAtTerminal);
    bool IConfiguration.IsShown => IsShown;

    /// <summary>
    /// 整数値への暗黙的なキャストができます。
    /// </summary>
    /// <param name="config"></param>

    public static implicit operator int(IntegerConfiguration config) => config.GetValue();
}

/// <summary>
/// 実数値を格納するオプションです。
/// </summary>
public abstract class FloatConfiguration : ValueConfiguration<float>
{
    internal abstract string? GetDisplayText();
    internal abstract GUIWidgetSupplier GetEditor();
    internal abstract float GetValue();
    internal abstract void UpdateValue(float value);
    internal abstract void ChangeValue(bool increase, bool loopAtTerminal = true);
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    float ValueConfiguration<float>.GetValue() => GetValue();

    void ValueConfiguration<float>.UpdateValue(float value) => UpdateValue(value);
    void ValueConfiguration<float>.ChangeValue(bool increase, bool loopAtTerminal) => ChangeValue(increase, loopAtTerminal);
    bool IConfiguration.IsShown => IsShown;

    /// <summary>
    /// 実数値への暗黙的なキャストができます。
    /// </summary>
    /// <param name="config"></param>

    public static implicit operator float(FloatConfiguration config) => config.GetValue();
}