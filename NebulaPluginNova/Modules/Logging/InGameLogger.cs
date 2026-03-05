using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Logging;

internal class InGameLogger : Virial.Logging.ILogger
{
    private float duration;
    public InGameLogger(float duration)
    {
        this.duration = duration;
    }

    private void PushMessage(string message) => DebugScreen.Push(message, duration);
    public void Debug(string message) => PushMessage(message);

    public void Error(string message) => PushMessage(message);
    public void Message(string message) => PushMessage(message);
    public void Warning(string message) => PushMessage(message);
}
