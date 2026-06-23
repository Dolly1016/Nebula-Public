using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Utilities;

namespace Virial.Helpers;

static public class StringHelper
{
    public static string DecimalToString(this float num, string digit)
    {
        string text = num.ToString("F" + digit).TrimEnd('0');
        if (text.EndsWith('.')) return text.Substring(0, text.Length - 1);
        return text;
    }

    public static string Color(this string text, Virial.Color color)
    {
        int r = Mathn.RoundToInt(color.R * 255f);
        int g = Mathn.RoundToInt(color.G * 255f);
        int b = Mathn.RoundToInt(color.B * 255f);
        int a = Mathn.RoundToInt(color.A * 255f);

        return $"<color=#{r:X2}{g:X2}{b:X2}{a:X2}>{text}</color>";
    }
}
