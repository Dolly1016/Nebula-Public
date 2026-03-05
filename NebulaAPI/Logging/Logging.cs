using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Logging;

public interface Logging
{
    ILogger InGameLogger(float duration);
    ILogger NebulaLogger(string? tag = null);
    ILogger BepInExLogger();
    ILogger CombinedLogger(params IEnumerable<ILogger> loggers);
}
