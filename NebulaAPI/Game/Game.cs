using Virial.Components;
using Virial.DI;

namespace Virial.Game;

/// <summary>
/// 現在プレイ中のゲームを表します。
/// </summary>
public interface Game : IModuleContainer, ILifespan, IArchivedGame
{
    /// <summary>
    /// プレイヤーを取得します。
    /// </summary>
    /// <param name="playerId">プレイヤーのID</param>
    /// <returns>IDが一致するプレイヤー 存在しない場合はnull</returns>
    Player? GetPlayer(byte playerId);

    /// <summary>
    /// 自身が操作するプレイヤー。
    /// 即ちこのプレイヤーの<see cref="Player.AmOwner"/>がtrueであることを意味します。
    /// </summary>
    Player LocalPlayer { get; }

    /// <summary>
    /// ゲーム中の全プレイヤーを取得します。
    /// </summary>
    /// <returns>ゲーム中の全プレイヤー</returns>
    IEnumerable<Player> GetAllPlayers();
    IReadOnlyList<Player> GetAllOrderedPlayers();

    KillButtonLikeHandler KillButtonLikeHandler { get; }

    internal void RegisterEntity(IGameOperator entity, ILifespan lifespan);

    /// <summary>
    /// ゲーム終了をトリガーします。
    /// この操作はホストのみ有効です。
    /// </summary>
    /// <param name="gameEnd"></param>
    /// <param name="reason"></param>
    /// <param name="additionalWinners"></param>
    void TriggerGameEnd(GameEnd gameEnd, Virial.Game.GameEndReason reason, EditableBitMask<Virial.Game.Player>? additionalWinners = null);

    /// <summary>
    /// ゲーム終了のトリガーをホストに依頼します。
    /// 終了理由は<see cref="GameEndReason.Special"/>として扱われます。
    /// </summary>
    /// <param name="gameEnd"></param>
    /// <param name="additionalWinners"></param>
    void RequestGameEnd(GameEnd gameEnd, BitMask<Virial.Game.Player> additionalWinners);
}
