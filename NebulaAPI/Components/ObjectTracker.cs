using Virial.Game;

namespace Virial.Components;

/// <summary>
/// 近くのオブジェクトを追跡するトラッカーを表します。
/// オブジェクトに対してインタラクトするボタンなどで使用します。
/// </summary>
/// <typeparam name="T">追跡対象のオブジェクトの型</typeparam>
public interface ObjectTracker<T> : IGameOperator
{
    /// <summary>
    /// 現在のターゲットを返します。
    /// </summary>
    public T? CurrentTarget { get; }
    /// <summary>
    /// 現在追跡しているターゲットをロックして変更させない場合は<c>true</c>にしてください。
    /// </summary>
    public bool IsLocked { get; set; }
    internal void SetColor(UnityEngine.Color color);

    /// <summary>
    /// アウトラインの色を設定します。
    /// </summary>
    /// <param name="color">アウトラインの色</param>
    public void SetColor(Virial.Color color) => SetColor(color.ToUnityColor());
}
