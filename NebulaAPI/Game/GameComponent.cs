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
    /// <returns></returns>
    public static Entity Register<Entity>(this Entity gameEntity, ILifespan lifespan) where Entity : IGameOperator
    {
        NebulaAPI.CurrentGame?.RegisterEntity(gameEntity, lifespan);
        return gameEntity;
    }

    /*
    /// <summary>
    /// バインド済みEntityを現在のゲームに追加します。
    /// </summary>
    /// <typeparam name="Entity"></typeparam>
    /// <param name="gameEntity"></param>
    /// <returns></returns>
    public static Entity Register<Entity>(this Entity gameEntity) where Entity : IGameOperator, ILifespan
    {
        NebulaAPI.CurrentGame?.RegisterEntity(gameEntity, gameEntity);
        return gameEntity;
    }
    */
}