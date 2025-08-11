using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;


static internal class ImageHelpers
{
    /// <summary>
    /// pngにテキストを埋め込む
    /// https://edom18.hateblo.jp/entry/2022/02/14/082055#C%E3%81%A7%E5%9F%8B%E3%82%81%E8%BE%BC%E3%81%BF%E3%81%AE%E5%AE%9F%E8%A3%85より
    /// </summary>
    
    private static class Crc32
    {
        private static uint[] _crcTable = MakeCrcTable();

        private static uint[] MakeCrcTable()
        {
            uint[] a = new uint[256];

            for (uint i = 0; i < a.Length; ++i)
            {
                uint c = i;
                for (int j = 0; j < 8; ++j)
                {
                    c = ((c & 1) != 0) ? (0xedb88320 ^ (c >> 1)) : (c >> 1);
                }

                a[i] = c;
            }

            return a;
        }

        private static uint Calculate(uint crc, byte[] buffer)
        {
            uint c = crc;

            for (int i = 0; i < buffer.Length; ++i)
            {
                c = _crcTable[(c ^ buffer[i]) & 0xff] ^ (c >> 8);
            }

            return c;
        }

        public static uint Hash(uint crc, byte[] buffer)
        {
            crc ^= 0xffffffff;

            return Calculate(crc, buffer) ^ 0xffffffff;
        }
    }

    
    static private Encoding _latin1 = Encoding.GetEncoding(28591);

    static private byte[] CreateTextChunkData(string keyword, string embeddedText)
    {
        // `tEXt` はASCIIエンコーディング
        byte[] chunkTypeData = Encoding.ASCII.GetBytes("tEXt");

        // keywordはLatin1エンコーディング
        byte[] keywordData = _latin1.GetBytes(keyword);

        // 区切り用の `0` を配列で確保
        byte[] separatorData = new byte[] { 0 };

        // data部分はLatin1エンコーディング
        byte[] textData = _latin1.GetBytes(embeddedText);

        int headerSize = sizeof(byte) * (chunkTypeData.Length + sizeof(int));
        int footerSize = sizeof(byte) * 4; // CRC
        int chunkDataSize = keywordData.Length + separatorData.Length + textData.Length;

        // チャンクデータ部分を生成
        byte[] chunkData = new byte[chunkDataSize];
        Array.Copy(keywordData, 0, chunkData, 0, keywordData.Length);
        Array.Copy(separatorData, 0, chunkData, keywordData.Length, separatorData.Length);
        Array.Copy(textData, 0, chunkData, keywordData.Length + separatorData.Length, textData.Length);

        // Length用データ
        byte[] lengthData = BitConverter.GetBytes(chunkDataSize);

        // CRCを計算（※）
        uint crc = Crc32.Hash(0, chunkTypeData);
        crc = Crc32.Hash(crc, chunkData);
        byte[] crcData = BitConverter.GetBytes(crc);

        // 全体のデータを確保
        byte[] data = new byte[headerSize + chunkDataSize + footerSize];

        // LengthとCRCはビッグエンディアンにする必要があるのかReverseする必要がある（※）
        Array.Reverse(lengthData);
        Array.Reverse(crcData);

        Array.Copy(lengthData, 0, data, 0, lengthData.Length);
        Array.Copy(chunkTypeData, 0, data, lengthData.Length, chunkTypeData.Length);
        Array.Copy(chunkData, 0, data, lengthData.Length + chunkTypeData.Length, chunkData.Length);
        Array.Copy(crcData, 0, data, lengthData.Length + chunkTypeData.Length + chunkData.Length, crcData.Length);

        return data;
    }
    public const int PngSignatureSize = 8;
    public const int PngHeaderSize = 33;
    static public byte[] EmbedTextMetadata(byte[] image, string keyword, string text)
    {
        byte[] chunkData = CreateTextChunkData(keyword, text);

        int embeddedDataSize = image.Length + chunkData.Length;
        byte[] embeddedData = new byte[embeddedDataSize];

        // Copy the PNG header to the result.
        Array.Copy(image, 0, embeddedData, 0, PngHeaderSize);

        // Add a tEXT chunk.
        Array.Copy(chunkData, 0, embeddedData, PngHeaderSize, chunkData.Length);

        // Join the data chunks to the result.
        Array.Copy(image, PngHeaderSize, embeddedData, PngHeaderSize + chunkData.Length, image.Length - PngHeaderSize);

        return embeddedData;
    }
}
