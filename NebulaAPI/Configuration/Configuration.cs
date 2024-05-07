using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Media;

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

public interface IConfigurationEntry
{
    //GUIWidgetSupplier GetEditor();
    string? GetDisplayText();
}

public interface IConfigurationHolder : IConfigurationEntry
{
}

public interface ModifierFilter
{
    bool Test(DefinedModifier modifier);
}

public interface RoleFilter
{
    bool Test(DefinedRole role);
}

/// <summary>
/// 値を持つオプションです。
/// </summary>
public interface ValueConfiguration : IConfigurationEntry
{
    /// <summary>
    /// 現在の値を文字列で取得します。
    /// </summary>
    string CurrentValue { get; }

    /// <summary>
    /// 現在の値を実数型で取得します。
    /// 実数型のオプションでない場合、取得に失敗し、例外が発生します。
    /// </summary>
    /// <returns>設定値</returns>
    float AsFloat();

    /// <summary>
    /// 現在の値を整数型で取得します。
    /// 整数型のオプションでない場合、取得に失敗し、例外が発生します。
    /// </summary>
    /// <returns>設定値</returns>
    int AsInt();

    /// <summary>
    /// 値を整数で指定します。ホストのみ使用可能です。
    /// </summary>
    /// <param name="value">新たな設定値</param>
    /// <returns>成功した場合true</returns>
    bool UpdateValue(int value);

    /// <summary>
    /// 値を実数で指定します。ホストのみ使用可能です。
    /// </summary>
    /// <param name="value">新たな設定値</param>
    /// <returns>成功した場合true</returns>
    bool UpdateValue(float value);

    /// <summary>
    /// 値を真偽値で指定します。ホストのみ使用可能です。
    /// </summary>
    /// <param name="value">新たな設定値</param>
    /// <returns>成功した場合true</returns>
    bool UpdateValue(bool value);

    /// <summary>
    /// 値を文字列で指定します。ホストのみ使用可能です。
    /// </summary>
    /// <param name="value">新たな設定値</param>
    /// <returns>成功した場合true</returns>
    bool UpdateValue(string value);

}