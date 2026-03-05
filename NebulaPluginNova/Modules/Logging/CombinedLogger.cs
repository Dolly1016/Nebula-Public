using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Logging;

internal class CombinedLogger : Virial.Logging.ILogger
{
    private Virial.Logging.ILogger[] loggers;
    public CombinedLogger(params Virial.Logging.ILogger[] loggers)
    {
        this.loggers = loggers;
    }
    void Virial.Logging.ILogger.Debug(string message)
    {
        foreach (var logger in loggers) logger.Debug(message);
    }
    void Virial.Logging.ILogger.Error(string message)
    {
        foreach (var logger in loggers) logger.Error(message);
    }
    void Virial.Logging.ILogger.Message(string message)
    {
        foreach (var logger in loggers) logger.Message(message);
    }
    void Virial.Logging.ILogger.Warning(string message)
    {
        foreach (var logger in loggers) logger.Warning(message);
    }
}
