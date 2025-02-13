using Hazel;

namespace Nebula.Extensions;

public static class MessageReaderExtention
{
    public static int PeekInt32(this MessageReader reader,int offset = 0)
    {
        return
            reader.Buffer[reader.readHead + 0 + offset] << 0x00
          | reader.Buffer[reader.readHead + 1 + offset] << 0x08
          | reader.Buffer[reader.readHead + 2 + offset] << 0x10
          | reader.Buffer[reader.readHead + 3 + offset] << 0x18;
    }

    public static short PeekInt16(this MessageReader reader, int offset = 0)
    {
        return (short)(
            reader.Buffer[reader.readHead + 0 + offset] << 0x00
          | reader.Buffer[reader.readHead + 1 + offset] << 0x08
          );
    }

    public static byte PeekByte(this MessageReader reader, int offset = 0)
    {
        return reader.Buffer[reader.readHead + offset];
    }

    public static unsafe float PeekSingle(this MessageReader reader, int offset = 0)
    {
        float output = 0;
        fixed (byte* bufPtr = &(((byte[])reader.Buffer)[reader.readHead + offset]))
        {
            byte* outPtr = (byte*)&output;

            *outPtr = *bufPtr;
            *(outPtr + 1) = *(bufPtr + 1);
            *(outPtr + 2) = *(bufPtr + 2);
            *(outPtr + 3) = *(bufPtr + 3);
        }

        return output;
    }

    public static byte GetPrevByte(this MessageReader reader) => reader.PeekByte(-1);
    public static int GetPrevInt32(this MessageReader reader) => reader.PeekInt32(-4);

}
