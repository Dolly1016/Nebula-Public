using Virial.Compat;

namespace Virial.Configuration;

public interface ISharableEntry
{
    /// <summary>
    /// オプションの内部名称です。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// RPC上でEntryを識別するためのID
    /// </summary>
    internal int Id { get; set; }

    /// <summary>
    /// RPC上に載せる際の生の値。
    /// RPCを介した更新ではローカルの値は更新せず、現在の値のみ更新します。
    /// </summary>
    internal int RpcValue { get; set; }

    /// <summary>
    /// ローカルに保存されている値を復元します。
    /// </summary>
    internal void RestoreSavedValue();
}

/// <summary>
/// 共有可能な変数を表します。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ISharableVariable<T> : ISharableEntry, Reference<T>
{
    /// <summary>
    /// 現在の値を取得します。
    /// セッターはローカルの値を上書きします。
    /// </summary>
    T CurrentValue { get; set; }

    /// <summary>
    /// 現在の値をローカルの値の保存なしで書き換えます。
    /// ローカルの値の保存に際してかかるオーバーヘッドを削減するためのメソッドです。
    /// データの不整合を回避するため、適切なタイミングで値を保存する必要があります。
    /// このメソッドでは書き換え可能なプレイヤーのチェックをスキップします。適切なプレイヤーであることを確認してから呼び出してください。
    /// </summary>
    /// <param name="value"></param>
    internal void SetValueWithoutSaveUnsafe(T value);
}

/// <summary>
/// 共有可能で、とりうる値に順序のある変数を表します。
/// </summary>
public interface IOrderedSharableEntry : ISharableEntry
{
    /// <summary>
    /// 順序に沿って値を変化させます。
    /// </summary>
    /// <param name="increase">上昇する方向に変化させる場合、true</param>
    /// <param name="allowLoop">ループした値の変化を許す場合、true</param>
    void ChangeValue(bool increase, bool allowLoop = true);
}

/// <summary>
/// 共有可能で、とりうる値に順序のある変数を表します。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IOrderedSharableVariable<T> : ISharableVariable<T>, IOrderedSharableEntry
{
}