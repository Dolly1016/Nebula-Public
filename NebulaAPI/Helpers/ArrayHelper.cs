using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

public static class ArrayHelper
{
    public static float[] Selection(float min,float max, float step)
    {
        List<float> result = new();
        float val = min;
        while(val < max)
        {
            result.Add(val);
            val += step;
        }
        result.Add(max);
        return result.ToArray();
    }

    public static int[] Selection(int min, int max, int step = 1)
    {
        List<int> result = new();
        int val = min;
        while (val < max)
        {
            result.Add(val);
            val += step;
        }
        result.Add(max);
        return result.ToArray();
    }
}
