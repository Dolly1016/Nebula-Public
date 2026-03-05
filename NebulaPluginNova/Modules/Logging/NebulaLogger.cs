using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.Logging;

internal class NebulaLogger : Virial.Logging.ILogger
{
    string? tag;
    internal NebulaLogger(string? tag)
    {
        this.tag = tag;
    }

    static internal Virial.Logging.ILogger Instance { get; } = new NebulaLogger(null);

    private string ToLabeledMessage(string level, string message)
    {
        if (tag == null) return $"[{level} | General] {message}";
        return $"[{level} | {tag}] {message}";
    }
    private void PushMessage(string level, string message)
    {
        NebulaLogFile.Print(ToLabeledMessage(level, message));
    }

    void Virial.Logging.ILogger.Debug(string message) => PushMessage("Debug", message);

    void Virial.Logging.ILogger.Message(string message) => PushMessage("Message", message);

    void Virial.Logging.ILogger.Warning(string message) => PushMessage("Warning", message);

    void Virial.Logging.ILogger.Error(string message) => PushMessage("Error", message);
}
