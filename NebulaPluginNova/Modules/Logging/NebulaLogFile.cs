using Nebula.Patches;
using System.Text;

namespace Nebula;

internal static class NebulaLogFile
{
    private static StreamWriter writer;
    private static int number;
    public static bool IsPreferential => number == 0;
    private static string FileName = "NebulaLog";
    static internal void Initialize(bool isDummy = false)
    {
        if (isDummy)
        {
            writer = null!;
            return;
        }

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

        writer = new(stream, Encoding.UTF8) { AutoFlush = true };

        lock (writer)
        {
            writer.WriteLine("\n  Nebula on the Ship  Log File \n");
        }

        number = counter;
    }

    internal static void Print(string message)
    {
        if (writer == null) return;

        lock (writer)
        {
            writer.WriteLine(message);
        }

        MemoryLogger.AppendLog(message);
    }
}
