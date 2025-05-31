namespace Virial.Media;

/// <summary>
/// 画像メディアを表します。
/// </summary>
public interface Image : IManageableAsset
{
    internal UnityEngine.Sprite GetSprite();
}

/// <summary>
/// 複数の画像メディアを表します。
/// </summary>
public interface MultiImage : IManageableAsset
{
    internal UnityEngine.Sprite GetSprite(int index);
    Image AsLoader(int index);
}

public interface IManageableAsset
{
    void UnloadAsset();
    System.Collections.IEnumerator LoadAsset();
    void MarkAsUnloadAsset();
}