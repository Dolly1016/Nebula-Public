using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;

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

public interface IConfigurationHolder
{
    TextComponent Title { get; }
    TextComponent Detail { get; }
    IEnumerable<IConfiguration> Configurations { get; }
    BitMask<ConfigurationTab> Tabs { get; }
    BitMask<GameModeDefinition> GameModes { get; }
    IEnumerable<ConfigurationTag> Tags { get; }

    /// <summary>
    /// ホルダ内にオプションを追加します。
    /// </summary>
    /// <param name="configuration"></param>
    void AppendConfiguration(IConfiguration configuration);

    /// <summary>
    /// ホルダ内にオプションを追加します。
    /// </summary>
    /// <param name="configuration"></param>
    void AppendConfigurations(IEnumerable<IConfiguration> configuration);

    void AddTags(params ConfigurationTag[] tags);

    /// <summary>
    /// 関連するオプションを取得します。
    /// </summary>
    /// <returns></returns>
    IEnumerable<IConfigurationHolder> RelatedHolders { get; }
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
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    bool ValueConfiguration<bool>.GetValue() => GetValue();

    void ValueConfiguration<bool>.UpdateValue(bool value) => UpdateValue(value);
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
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    int ValueConfiguration<int>.GetValue() => GetValue();

    void ValueConfiguration<int>.UpdateValue(int value) => UpdateValue(value);
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
    internal virtual bool IsShown => true;

    string? IConfiguration.GetDisplayText() => GetDisplayText();

    GUIWidgetSupplier IConfiguration.GetEditor() => GetEditor();

    float ValueConfiguration<float>.GetValue() => GetValue();

    void ValueConfiguration<float>.UpdateValue(float value) => UpdateValue(value);
    bool IConfiguration.IsShown => IsShown;

    /// <summary>
    /// 実数値への暗黙的なキャストができます。
    /// </summary>
    /// <param name="config"></param>

    public static implicit operator float(FloatConfiguration config) => config.GetValue();
}