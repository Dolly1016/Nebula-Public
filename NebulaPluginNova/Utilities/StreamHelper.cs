using System.Reflection;

namespace Nebula.Utilities;

public static class StreamHelper
{
    public static string ReadToEnd(this Stream stream, bool closeStream = true)
    {
        string result;
        using(var reader = new StreamReader(stream))
        {
             result = reader.ReadToEnd();
        }
        if (closeStream) stream.Dispose();
        return result;
    }

    public static Stream? OpenFromResource(string path)
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        }
        catch
        {
            return null;
        }
    }

    public static Stream? OpenFromDisk(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open);
        }
        catch
        {
            return null;
        }
    }
}
