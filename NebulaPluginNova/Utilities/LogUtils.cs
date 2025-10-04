using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public static class LogUtils
{
    public static void WriteToConsole(string line)
    {
        BepInEx.ConsoleManager.StandardOutStream.WriteLine(line);
#if PC
#else
        NebulaAndroid.NebulaLoader.MyPlugin.Log.LogInfo(line);
#endif
    }
}
