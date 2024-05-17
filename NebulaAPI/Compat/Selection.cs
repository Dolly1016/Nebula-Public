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
/// 主にConfigurationで使用する実数型の選択肢の表現を簡略化するためのものです。
/// </summary>
public class FloatSelection
{
    public float[] Selection { get; internal init; }

    public FloatSelection(float[] selection)
    {
        this.Selection = selection;
    }

    static public implicit operator FloatSelection(float[] selections) => new FloatSelection(selections);
    static public implicit operator FloatSelection((float min, float max, float step) parameter) => new FloatSelection(ArrayHelper.Selection(parameter.min, parameter.max, parameter.step));
}

/// <summary>
/// 主にConfigurationで使用する整数型の選択肢の表現を簡略化するためのものです。
/// </summary>
public class IntegerSelection
{
    public int[] Selection { get; internal init; }

    public IntegerSelection(int[] selection)
    {
        this.Selection = selection;
    }

    static public implicit operator IntegerSelection(int[] selections) => new IntegerSelection(selections);
    static public implicit operator IntegerSelection((int min, int max, int step) parameter) => new IntegerSelection(ArrayHelper.Selection(parameter.min, parameter.max, parameter.step));
    static public implicit operator IntegerSelection((int min, int max) parameter) => new IntegerSelection(ArrayHelper.Selection(parameter.min, parameter.max));
}
