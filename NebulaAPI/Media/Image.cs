using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Media;

/// <summary>
/// 画像メディアを表します。
/// </summary>
public interface Image
{
    internal UnityEngine.Sprite GetSprite();
}

/// <summary>
/// 複数の画像メディアを表します。
/// </summary>
public interface MultiImage
{
    internal UnityEngine.Sprite GetSprite(int index);
}
