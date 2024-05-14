using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Configuration;

public interface Configurations
{
    /// <summary>
    /// 真偽値型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    ISharableVariable<bool> BoolVariable(string id, bool defaultValue);

    /// <summary>
    /// 0から指定された最大値までの値のいずれかを格納できる整数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <param name="maxValueExcluded">最大値です。指定された値は含まれません。</param>
    /// <returns></returns>
    ISharableVariable<int> SelectionVariable(string id, int defaultValue, int maxValueExcluded);

    /// <summary>
    /// 指定された値のいずれかを格納できる実数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    ISharableVariable<float> FloatVariable(string id, float[] values, float defaultValue);

    /// <summary>
    /// 指定された値のいずれかを格納できる整数型の共有変数を生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    ISharableVariable<int> IntegerVariable(string id, int[] values, int defaultValue);

    /// <summary>
    /// ホルダを生成します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tabs"></param>
    /// <param name="gamemodes"></param>
    /// <returns></returns>
    IConfigurationHolder Holder(string id, IEnumerable<ConfigurationTab> tabs, IEnumerable<GameModeDefinition> gamemodes);

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
}
