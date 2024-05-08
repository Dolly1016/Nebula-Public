using System.Text;

namespace Nebula;

public class NebulaLog
{
    StreamWriter writer;
    int number;
    public bool IsPreferential => number == 0;
    private static string FileName = "NebulaLog";
    public NebulaLog()
    {

        int counter = 0;
        Stream? stream;

        while (true)
        {
            string path = FileName;
            if (counter > 0) path += " " + counter;
            path += ".txt";

            try
            {
                stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,FileShare.Read);
                stream.SetLength(0);
            }
            catch
            {
                counter++;
                continue;
            }

            //Debug.Log("My log file path :" + path);

            break;
        }

        writer = new(stream, Encoding.UTF8);
        writer.AutoFlush = true;

        lock (writer)
        {
            writer.WriteLine("\n  Nebula on the Ship  Log File \n");
        }

        number = counter;
    }

    public class LogCategory
    {
        public string Category;
        public LogCategory(string category) {
            this.Category = category;
        }

        static public LogCategory MoreCosmic = new("MoreCosmic");
        static public LogCategory Language = new("Language");
        static public LogCategory Addon = new("Addon");
        static public LogCategory Document = new("Documentation");
        static public LogCategory Preset = new("Preset");
        static public LogCategory Scripting = new("Scripting");
        static public LogCategory Role = new("Role");
        static public LogCategory System = new("System");
    }

    public class LogLevel
    {
        public string? Level;
        public int LevelMask;
        public LogLevel(string? level, int mask)
        {
            this.Level = level;
            this.LevelMask = mask;
        }

        static public LogLevel Log = new("Log", 0x0001);
        static public LogLevel Warning = new("Warning", 0x0002);
        static public LogLevel Error = new("Error", 0x0004);
        static public LogLevel FatalError = new("FatalError", 0x0008);

        static public LogLevel AllLevel = new(null, 0xFFFF);

        static public int ToMask(params LogLevel[] level)
        {
            int mask = 0;
            foreach (var l in level) mask |= l.LevelMask;
            return mask;
        }
    }

    public void Print(string message) => Print(LogLevel.Log, null, message);
    public void Print(LogCategory category, string message) => Print(LogLevel.Log, category, message);
    public void Print(LogLevel level, string message) => Print(level, null, message);
    public void PrintWithBepInEx(LogLevel level, LogCategory? category, string message)
    {
        Print(level, category, message);
        string rawMessage = "[NoS]" + ToRawMessage(level, category, message);
        if (level == LogLevel.Log)
            Debug.Log(rawMessage);
        else if (level == LogLevel.Warning)
            Debug.LogWarning(rawMessage);
        else
            Debug.LogError(rawMessage);
    }

    public void Print(LogLevel level, LogCategory? category, string message)
    {
        message = message.Replace("\n", "\n    ");
        string header = (category?.Category ?? "Generic");
        if (level.Level != null) header = level.Level + " | " + header;

        lock (writer)
        {
            writer.WriteLine("[" + header + "] " + message);
        }
    }

    string ToRawMessage(LogLevel level, LogCategory? category, string message)
    {
        message = message.Replace("\n", "\n    ");
        string header = (category?.Category ?? "Generic");
        if (level.Level != null) header = level.Level + " | " + header;
        return "[" + header + "]" + message;
    }
}
