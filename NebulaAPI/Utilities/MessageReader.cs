using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public class MessageReader
{
    const int MessageLength = 2048;

    private Hazel.MessageReader origReader;
    private byte[] message;
    private int length;
    private int offset;

    private int Position
    {
        get { return this.position; }
        set
        {
            this.position = value;
            this.readHead = value + offset;
        }
    }

    private int position;
    private int readHead;

    static private List<MessageReader> unused = [];
    internal MessageReader()
    {
        message = new byte[MessageLength];
    }

    internal void InitializeWith(byte[] message)
    {
        this.offset = 0;
        this.length = message.Length;
        this.position = 0;
        this.readHead = 0;
    }

    static internal MessageReader Get(Hazel.MessageReader orig)
    {
        MessageReader reader;
        if (unused.Count > 0)
        {
            reader = unused[^1];
            unused.RemoveAt(unused.Count - 1);
        }
        else
        {
            reader = new();
        }

        reader.origReader = orig;
        reader.offset = orig.Offset;
        reader.length = orig.Length;
        reader.position = orig._position;
        reader.readHead = orig.readHead;

        orig.Buffer.AsSpan().CopyTo(reader.message);
        return reader;
    }

    internal void End()
    {
        origReader._position = this.position;
        origReader.readHead = this.readHead;
        unused.Add(this);
    }

    public bool ReadBoolean()
    {
        byte val = this.FastByte();
        return val != 0;
    }

    public sbyte ReadSByte()
    {
        return (sbyte)this.FastByte();
    }

    public byte ReadByte()
    {
        return this.FastByte();
    }

    public ushort ReadUInt16()
    {
        return (ushort)(
              this.FastByte()
            | this.FastByte() << 8);
    }

    public short ReadInt16()
    {
        return (short)(
              this.FastByte()
            | this.FastByte() << 8);
    }

    public uint ReadUInt32()
    {
        return
                    this.FastByte()
            | (uint)this.FastByte() << 8
            | (uint)this.FastByte() << 16
            | (uint)this.FastByte() << 24;
    }

    public int ReadInt32()
    {
        return 
              this.FastByte()
            | this.FastByte() << 8
            | this.FastByte() << 16
            | this.FastByte() << 24;
    }

    public ulong ReadUInt64()
    {
        return
              (ulong)this.FastByte()
            | (ulong)this.FastByte() << 8
            | (ulong)this.FastByte() << 16
            | (ulong)this.FastByte() << 24
            | (ulong)this.FastByte() << 32
            | (ulong)this.FastByte() << 40
            | (ulong)this.FastByte() << 48
            | (ulong)this.FastByte() << 56;
    }

    public long ReadInt64()
    {
        return(long)this.FastByte()
            | (long)this.FastByte() << 8
            | (long)this.FastByte() << 16
            | (long)this.FastByte() << 24
            | (long)this.FastByte() << 32
            | (long)this.FastByte() << 40
            | (long)this.FastByte() << 48
            | (long)this.FastByte() << 56;
    }

    public unsafe float ReadSingle()
    {
        float output = 0;
        fixed (byte* bufPtr = &this.message[this.readHead])
        {
            byte* outPtr = (byte*)&output;

            *outPtr = *bufPtr;
            *(outPtr + 1) = *(bufPtr + 1);
            *(outPtr + 2) = *(bufPtr + 2);
            *(outPtr + 3) = *(bufPtr + 3);
        }

        this.Position += 4;
        return output;
    }

    public string ReadString()
    {
        int len = this.ReadPackedInt32();
        if (this.BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {this.BytesRemaining}");

        string output = UTF8Encoding.UTF8.GetString(this.message, this.readHead, len);

        this.Position += len;
        return output;
    }


    public int ReadBytesAndSize(byte[] destination)
    {
        int length = ReadPackedInt32();
        if (BytesRemaining < length)
        {
            throw new InvalidDataException($"Read length is longer than message length: {length} of {BytesRemaining}");
        }

        if (destination.Length < length)
        {
            throw new OverflowException($"Read length is longer than buffer length: {length} > {destination.Length}");
        }

        ReadBytes(length, destination);
        return length;
    }

    public byte[] ReadBytesAndSize()
    {
        int len = this.ReadPackedInt32();
        if (this.BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {this.BytesRemaining}");

        return this.ReadBytes(len);
    }

    public byte[] ReadBytes(int length)
    {
        if (this.BytesRemaining < length) throw new InvalidDataException($"Read length is longer than message length: {length} of {this.BytesRemaining}");

        byte[] output = new byte[length];
        ReadBytes(length, output);
        return output;
    }

    public void ReadBytes(int length, byte[] destination)
    {
        if (this.BytesRemaining < length) throw new InvalidDataException($"Read length is longer than message length: {length} of {this.BytesRemaining}");

        Array.Copy(this.message, this.readHead, destination, 0, length);
        this.Position += length;
    }

    public int ReadPackedInt32()
    {
        return (int)this.ReadPackedUInt32();
    }

    public uint ReadPackedUInt32()
    {
        bool readMore = true;
        int shift = 0;
        uint output = 0;

        while (readMore)
        {
            if (this.BytesRemaining < 1) throw new InvalidDataException($"Read length is longer than message length.");

            byte b = this.ReadByte();
            if (b >= 0x80)
            {
                readMore = true;
                b ^= 0x80;
            }
            else
            {
                readMore = false;
            }

            output |= (uint)(b << shift);
            shift += 7;
        }

        return output;
    }


    public int BytesRemaining => this.length - this.Position;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte FastByte()
    {
        this.position++;
        return this.message[this.readHead++];
    }
}
