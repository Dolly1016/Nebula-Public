using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

static public class StringHelper
{
    public static string DecimalToString(this float num, string digit)
    {
        string text = num.ToString("F" + digit).TrimEnd('0');
        if (text.EndsWith('.')) return text.Substring(0, text.Length - 1);
        return text;
    }
}
