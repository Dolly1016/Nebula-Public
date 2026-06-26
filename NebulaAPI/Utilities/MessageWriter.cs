using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public class MessageWriter
{
    const int MessageLength = 2048;

    private Hazel.MessageWriter origWriter;

    private byte[] message;
    private int position;

    public int Length => position;
    public Span<byte> Message => message.AsSpan(0, position);

    static private List<MessageWriter> unused = [];
    private MessageWriter()
    {
        message = new byte[MessageLength];
    }

    internal MessageWriter(int length)
    {
        message = new byte[length];
    }

    internal void Initialize()
    {
        position = 0;
    }

    static internal MessageWriter Get()
    {
        MessageWriter writer;
        if (unused.Count > 0)
        {
            writer = unused[^1];
            unused.RemoveAt(unused.Count - 1);
        }
        else
        {
            writer = new();
        }

        writer.Initialize();

        return writer;
    }

    internal void End(Hazel.MessageWriter writeTo)
    {
        if (position == 0) return;
        var lastPos = writeTo.Position;
        var lastLength = writeTo.Length;
        var span = writeTo.Buffer.AsSpan().Slice(lastPos);

        message.AsSpan(0, position).CopyTo(span);
        if (position + lastPos > lastLength) writeTo.Length = position + lastPos;
        writeTo.Position += position;

        unused.Add(this);
    }

    public void Write(bool value)
    {
        this.message[this.position++] = (byte)(value ? 1 : 0);
    }

    public void Write(sbyte value)
    {
        this.message[this.position++] = (byte)value;
    }

    public void Write(byte value)
    {
        this.message[this.position++] = value;
    }

    public void Write(short value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
    }

    public void Write(ushort value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
    }

    public void Write(uint value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
        this.message[this.position++] = (byte)(value >> 16);
        this.message[this.position++] = (byte)(value >> 24);
    }

    public void Write(int value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
        this.message[this.position++] = (byte)(value >> 16);
        this.message[this.position++] = (byte)(value >> 24);
    }

    public void Write(ulong value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
        this.message[this.position++] = (byte)(value >> 16);
        this.message[this.position++] = (byte)(value >> 24);
        this.message[this.position++] = (byte)(value >> 32);
        this.message[this.position++] = (byte)(value >> 40);
        this.message[this.position++] = (byte)(value >> 48);
        this.message[this.position++] = (byte)(value >> 56);
    }

    public void Write(long value)
    {
        this.message[this.position++] = (byte)value;
        this.message[this.position++] = (byte)(value >> 8);
        this.message[this.position++] = (byte)(value >> 16);
        this.message[this.position++] = (byte)(value >> 24);
        this.message[this.position++] = (byte)(value >> 32);
        this.message[this.position++] = (byte)(value >> 40);
        this.message[this.position++] = (byte)(value >> 48);
        this.message[this.position++] = (byte)(value >> 56);
    }

    public unsafe void Write(float value)
    {
        fixed (byte* ptr = &this.message[this.position])
        {
            byte* valuePtr = (byte*)&value;

            *ptr = *valuePtr;
            *(ptr + 1) = *(valuePtr + 1);
            *(ptr + 2) = *(valuePtr + 2);
            *(ptr + 3) = *(valuePtr + 3);
        }

        this.position += 4;
    }

    public void Write(string value)
    {
        var bytes = UTF8Encoding.UTF8.GetBytes(value);
        this.WritePacked(bytes.Length);
        this.Write(bytes);
    }

    public void WriteBytesAndSize(byte[] bytes)
    {
        this.WritePacked((uint)bytes.Length);
        this.Write(bytes);
    }

    public void WriteBytesAndSize(byte[] bytes, int length)
    {
        this.WritePacked((uint)length);
        this.Write(bytes, length);
    }

    public void WriteBytesAndSize(byte[] bytes, int offset, int length)
    {
        this.WritePacked((uint)length);
        this.Write(bytes, offset, length);
    }

    public void Write(byte[] bytes)
    {
        Array.Copy(bytes, 0, this.message, this.position, bytes.Length);
        this.position += bytes.Length;
    }

    public void Write(byte[] bytes, int offset, int length)
    {
        Array.Copy(bytes, offset, this.message, this.position, length);
        this.position += length;
    }

    public void Write(byte[] bytes, int length)
    {
        Array.Copy(bytes, 0, this.message, this.position, length);
        this.position += length;
    }

    public void WritePacked(int value) => this.WritePacked((uint)value);
    
    public void WritePacked(uint value)
    {
        do
        {
            byte b = (byte)(value & 0xFF);
            if (value >= 0x80)
            {
                b |= 0x80;
            }

            this.Write(b);
            value >>= 7;
        } while (value > 0);
    }
}
