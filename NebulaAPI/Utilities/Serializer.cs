using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Virial.Utilities;

public class SerializedDataWriter
{
    private List<byte[]> bytes = new();

    /// <summary>
    /// 文字列を書き込みます。
    /// <see cref="SerializedDataReader.ReadString"/>がこれに対応します。
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public SerializedDataWriter Write(string str)
    {
        var strByte =Encoding.UTF8.GetBytes(str);
        bytes.Add(BitConverter.GetBytes(strByte.Length));
        bytes.Add(strByte);
        return this;
    }

    public SerializedDataWriter Write(float val)
    {
        bytes.Add(BitConverter.GetBytes(val));
        return this;
    }

    public SerializedDataWriter Write(int val)
    {
        bytes.Add(BitConverter.GetBytes(val));
        return this;
    }

    public SerializedDataWriter Write(byte val)
    {
        bytes.Add([val]);
        return this;
    }

    internal SerializedDataWriter Write(UnityEngine.Color32 val)
    {
        bytes.Add([val.r, val.g, val.b, val.a]);
        return this;
    }

    public byte[] ToData()
    {
        byte[] data = new byte[bytes.Sum(b => b.Length)];
        int written = 0;
        foreach(var ary in bytes)
        {
            Array.Copy(ary, 0, data, written, ary.Length);
            written += ary.Length;
        }
        return data;
    }
}

public class SerializedDataReader : IDisposable
{
    public SerializedDataReader(Stream stream)
    {
        myStream = stream;
    }

    Stream myStream;

    public string ReadString()
    {
        byte[] lengthByte = new byte[4];
        myStream.Read(lengthByte, 0, 4);
        int length = BitConverter.ToInt32(lengthByte);
        byte[] strByte = new byte[length];
        myStream.Read(strByte, 0, length);
        return Encoding.UTF8.GetString(strByte);
    }

    public byte ReadByte()
    {
        return (byte)myStream.ReadByte();
    }

    public int ReadInt32()
    {
        byte[] dataByte = new byte[4];
        myStream.Read(dataByte, 0, 4);
        return BitConverter.ToInt32(dataByte);
    }

    public float ReadSingle()
    {
        byte[] dataByte = new byte[4];
        myStream.Read(dataByte, 0, 4);
        return BitConverter.ToSingle(dataByte);
    }

    internal Color32 ReadColor32()
    {
        return new Color32((byte)myStream.ReadByte(), (byte)myStream.ReadByte(), (byte)myStream.ReadByte(), (byte)myStream.ReadByte());
    }

    public void Dispose()
    {
        myStream.Dispose();
    }
}
