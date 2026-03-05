using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Logging;

namespace Nebula.Modules.Logging;

internal class LoggingImpl : Virial.Logging.Logging
{
    static internal Virial.Logging.Logging API { get; } = new LoggingImpl();
    Virial.Logging.ILogger Virial.Logging.Logging.InGameLogger(float duration) => new InGameLogger(duration);
    Virial.Logging.ILogger Virial.Logging.Logging.NebulaLogger(string? tag) => new NebulaLogger(tag);
    Virial.Logging.ILogger Virial.Logging.Logging.BepInExLogger() => BepInExLogger.GetLogger();
    Virial.Logging.ILogger Virial.Logging.Logging.CombinedLogger(params IEnumerable<Virial.Logging.ILogger> loggers) => new CombinedLogger(loggers.ToArray());
}
