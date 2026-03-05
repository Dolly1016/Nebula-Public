using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Logging;

internal class BepInExLogger : Virial.Logging.ILogger
{
    static private BepInExLogger? instance = null;
    private BepInEx.Logging.ManualLogSource logSource;
    static internal BepInExLogger GetLogger()
    {
        if(instance == null) instance = new BepInExLogger();
        return instance;
    }
    private BepInExLogger() {
        logSource = BepInEx.Logging.Logger.CreateLogSource("Nebula");
    }

    void Virial.Logging.ILogger.Debug(string message)
    {
        logSource.LogDebug(message);
    }

    void Virial.Logging.ILogger.Message(string message)
    {
        logSource.LogInfo(message);
    }

    void Virial.Logging.ILogger.Warning(string message)
    {
        logSource.LogWarning(message);
    }

    void Virial.Logging.ILogger.Error(string message)
    {
        logSource.LogError(message);
    }
}
