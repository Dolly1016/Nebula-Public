using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Virial.Configuration;

public enum FloatConfigurationDecorator
{
    /// <summary>
    /// 値は修飾されません。
    /// </summary>
    None,
    /// <summary>
    /// 倍率を設定するオプションとして値を修飾します。
    /// </summary>
    Ratio,
    /// <summary>
    /// 秒数を設定するオプションとして値を修飾します。
    /// </summary>
    Second,
    /// <summary>
    /// タスクフェイズを設定するオプションとして値を修飾します。
    /// </summary>
    TaskPhase,
    /// <summary>
    /// 確率を設定するオプションとして値を修飾します。
    /// </summary>
    Percentage,
}

/// <summary>
/// ベント設定オプションのエディタです。
/// </summary>
public interface IVentConfiguration : IConfiguration
{
    /// <summary>
    /// ベントの使用可能回数を返します。
    /// </summary>
    public int Uses { get; }

    /// <summary>
    /// ベント使用のクールダウンを返します。
    /// </summary>
    public float CoolDown { get; }

    /// <summary>
    /// ベントに潜伏できる時間を返します。
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// ベントを使用できるかどうかを取得します。
    /// </summary>
    public bool CanUseVent { get; }
}

/// <summary>
/// クールダウン設定で使用する、クールダウンの設定方法です。
/// </summary>
public enum CoolDownType
{
    Immediate = 0,
    Relative = 1,
    Ratio = 2
}


/// <summary>
/// クールダウン設定のエディタです。
/// </summary>
public interface IRelativeCoolDownConfiguration : IConfiguration{
    /// <summary>
    /// 現在のクールダウンです。
    /// </summary>
    float CoolDown { get; }
    float GetCoolDown(float baseCooldown);
}

public interface ITaskConfiguration
{
    IEnumerable<IConfiguration> Configurations { get; }
    IConfiguration AsGroup(Virial.Color color);
    void GetTasks(out int shortTasks, out int longTasks, out int commonTasks);
    bool RequiresTasks { get; }
}

public interface Configurations
{
    /// <summary>
    /// 真偽値型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    IOrderedSharableVariable<bool> SharableVariable(string id, bool defaultValue);

    /// <summary>
    /// 0から指定された最大値までの値のいずれかを格納できる整数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <param name="maxValueExcluded">最大値です。指定された値は含まれません。</param>
    /// <returns></returns>
    ISharableVariable<int> SharableVariable(string id, int defaultValue, int maxValueExcluded);

    /// <summary>
    /// 指定された値のいずれかを格納できる実数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    IOrderedSharableVariable<float> SharableVariable(string id, FloatSelection values, float defaultValue);

    /// <summary>
    /// 指定された値のいずれかを格納できる整数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    IOrderedSharableVariable<int> SharableVariable(string id, IntegerSelection values, int defaultValue);


    /// <summary>
    /// ホルダを生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tabs"></param>
    /// <param name="gamemodes"></param>
    /// <returns></returns>
    IConfigurationHolder Holder(TextComponent title, TextComponent detail, IEnumerable<ConfigurationTab> tabs, IEnumerable<GameModeDefinition> gamemodes, Func<bool>? predicate = null, Func<ConfigurationHolderState>? state = null);

    /// <summary>
    /// ホルダを生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tabs"></param>
    /// <param name="gamemodes"></param>
    /// <returns></returns>
    IConfigurationHolder Holder(string id, IEnumerable<ConfigurationTab> tabs, IEnumerable<GameModeDefinition> gamemodes, Func<bool>? predicate = null) => Holder(NebulaAPI.GUI.LocalizedTextComponent(id), NebulaAPI.GUI.LocalizedTextComponent(id + ".detail"), tabs, gamemodes,predicate);

    /// <summary>
    /// モディファイアフィルタを生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    ModifierFilter ModifierFilter(string id);

    /// <summary>
    /// 幽霊役職フィルタを生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    GhostRoleFilter GhostRoleFilter(string id);

    BoolConfiguration Configuration(string id, bool defaultValue, Func<bool>? predicate = null, TextComponent? title = null);
    IntegerConfiguration Configuration(string id, IntegerSelection selection, int defaultValue, Func<bool>? predicate = null, Func<int, string>? decorator = null, TextComponent? title = null);
    FloatConfiguration FloatOption(string id, FloatSelection selection, float defaultValue, Func<bool>? predicate) => FloatOption(id, selection, defaultValue, predicate: predicate);
    FloatConfiguration Configuration(string id, FloatSelection selection, float defaultValue, FloatConfigurationDecorator decorator = FloatConfigurationDecorator.None, Func<bool>? predicate = null, TextComponent? title = null);
    FloatConfiguration Configuration(string id, FloatSelection selection, float defaultValue, Func<float, string> decorator, Func<bool>? predicate = null, TextComponent? title = null);
    ValueConfiguration<int> Configuration(string id, string[] selection, int defualtIndex, Func<bool>? predicate = null, TextComponent? title = null);
    IConfiguration Configuration(Func<string?> displayShower, GUIWidgetSupplier editor, Func<bool>? predicate = null);

    IVentConfiguration VentConfiguration(string id, bool isOptional, IntegerSelection? usesSelection, int usesDefaultValue, FloatSelection? coolDownSelection, float coolDownDefaultValue, FloatSelection? durationSelection, float durationDefaultValue);
    IVentConfiguration NeutralVentConfiguration(string id, bool isOptional) => VentConfiguration(id, isOptional, null, -1, (0f, 60f, 5f), 15f, (2.5f, 30f, 2.5f), 10f);

    ITaskConfiguration TaskConfiguration(string id, bool forceTasks, bool containsCommonTasks, Func<bool>? predicate = null, string? translationKey = null);

    IRelativeCoolDownConfiguration KillConfiguration(string id, CoolDownType defaultType, FloatSelection immediateSelection, float immediateDefaultValue, FloatSelection relativeSelection, float relativeDefaultValue, FloatSelection ratioSelection, float ratioDefaultValue, Func<bool>? predicate = null, Func<float>? baseKillCooldown = null)
        => KillConfiguration(NebulaAPI.GUI.LocalizedTextComponent(id), id, defaultType, immediateSelection, immediateDefaultValue, relativeSelection, relativeDefaultValue, ratioSelection, ratioDefaultValue, predicate, baseKillCooldown);
    IRelativeCoolDownConfiguration KillConfiguration(TextComponent title, string id, CoolDownType defaultType, FloatSelection immediateSelection, float immediateDefaultValue, FloatSelection relativeSelection, float relativeDefaultValue, FloatSelection ratioSelection, float ratioDefaultValue, Func<bool>? predicate = null, Func<float>? baseKillCooldown = null);

    /// <summary>
    /// ゲーム内設定画面を開いているとき、画面の更新を要求します。
    /// </summary>
    void RequireUpdateSettingScreen();

    /// <summary>
    /// 名前から共有可能な値を取得します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    ISharableVariable<T>? GetSharableVariable<T>(string id);

    /// <summary>
    /// 現在、ジャッカライズ可能であればtrueを返します。
    /// </summary>
    bool CanJackalize { get; }
}
