using Rewired.Utils.Platforms.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;
using Virial.Helpers;

namespace Virial.Compat;

/// <summary>
/// オプションのコンストラクタで使用する、実数型の選択肢の簡略化された表現です。
/// </summary>
public class FloatSelection
{
    public float[] Selection { get; internal init; }

    public FloatSelection(float[] selection)
    {
        this.Selection = selection;
    }

    /// <summary>
    /// 配列から実数型の選択肢を生成します。
    /// </summary>
    /// <param name="selections">選択肢に含まれる値</param>
    static public implicit operator FloatSelection(float[] selections) => new FloatSelection(selections);
    /// <summary>
    /// 最小値、最大値、刻み幅から実数型の選択肢を生成します。
    /// </summary>
    /// <param name="parameter">最小値、最大値、刻み幅からなるタプル</param>
    static public implicit operator FloatSelection((float min, float max, float step) parameter) => new FloatSelection(ArrayHelper.Selection(parameter.min, parameter.max, parameter.step));
}

/// <summary>
/// オプションのコンストラクタで使用する、整数型の選択肢の簡略化された表現です。
/// </summary>
public class IntegerSelection
{
    public int[] Selection { get; internal init; }

    public IntegerSelection(int[] selection)
    {
        this.Selection = selection;
    }

    /// <summary>
    /// 配列から整数型の選択肢を生成します。
    /// </summary>
    /// <param name="selections">選択肢に含まれる値</param>
    static public implicit operator IntegerSelection(int[] selections) => new IntegerSelection(selections);
    /// <summary>
    /// 最小値、最大値、刻み幅から整数型の選択肢を生成します。
    /// </summary>
    /// <param name="parameter">最小値、最大値、刻み幅からなるタプル</param>
    static public implicit operator IntegerSelection((int min, int max, int step) parameter) => new IntegerSelection(ArrayHelper.Selection(parameter.min, parameter.max, parameter.step));
    /// <summary>
    /// 最小値、最大値から刻み幅1の整数型の選択肢を生成します。
    /// </summary>
    /// <param name="parameter">最小値、最大値からなるタプル</param>
    static public implicit operator IntegerSelection((int min, int max) parameter) => new IntegerSelection(ArrayHelper.Selection(parameter.min, parameter.max));
}
