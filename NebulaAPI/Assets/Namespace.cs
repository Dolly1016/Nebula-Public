using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Media;

namespace Virial.Assets;

/// <summary>
/// InnerslothやNebula,その他各種アドオンが占有する名前空間を表します。
/// </summary>
public interface INameSpace
{
    /// <summary>
    /// ファイルやストリームを開きます。
    /// </summary>
    /// <param name="innerAddress">ファイルのアドレス</param>
    /// <returns></returns>
    Stream? OpenRead(string innerAddress);

    /// <summary>
    /// 画像を読み込みます。
    /// </summary>
    /// <param name="innerAddress">画像データのアドレス</param>
    /// <param name="pixelsPerUnit">単位当たりのピクセル数 この値が大きいほど表示される大きさは小さくなります</param>
    /// <returns></returns>
    Image? GetImage(string innerAddress, float pixelsPerUnit = 100f);
}
