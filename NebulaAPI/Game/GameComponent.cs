using Virial.Assignable;

namespace Virial.Game;



/// <summary>
/// ゲームに作用するEntityを表します。
/// </summary>
public interface IGameOperator
{
    /// <summary>
    /// 紐づけられたLifespanの寿命が尽きたときに呼び出されます。この後Entityは削除されます。
    /// </summary>
    public void OnReleased(){}
}

/// <summary>
/// プレイヤーに紐づけられたEntityを表します。
/// </summary>
public interface IBindPlayer
{
    /// <summary>
    /// このEntityを所有するプレイヤーを表します。
    /// 所有者は途中で変更しないでください。
    /// </summary>
    public Player MyPlayer { get; }

    /// <summary>
    /// 自身が所有者であることを表します。
    /// </summary>
    public bool AmOwner => MyPlayer.AmOwner;
}

public static class GameEntityExtension
{
    /// <summary>
    /// Entityを現在のゲームに追加します。
    /// </summary>
    /// <typeparam name="Entity"></typeparam>
    /// <param name="gameEntity"></param>
    /// <param name="lifespan"></param>
    /// <param name="onSubscribed">ゲーム作用素は、実際にはこの関数の呼び出しから僅かに後のタイミングで追加されます。実際に追加したタイミングに処理を差し込む場合はこのデリゲートを使用します。</param>
    /// <returns></returns>
    public static Entity Register<Entity>(this Entity gameEntity, ILifespan lifespan, Action<Entity>? onSubscribed = null) where Entity : IGameOperator
    {
        NebulaAPI.CurrentGame?.RegisterEntity(gameEntity, lifespan, onSubscribed != null ? () => onSubscribed.Invoke(gameEntity) : null);
        return gameEntity;
    }
}