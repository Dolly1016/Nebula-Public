using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Compat;

public static class APICompat
{
    static public UnityEngine.Color ToUnityColor(this Virial.Color color) => new UnityEngine.Color(color.R,color.G,color.B,color.A);
}
