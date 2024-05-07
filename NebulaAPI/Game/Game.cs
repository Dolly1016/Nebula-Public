using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Game;

/// <summary>
/// 現在プレイ中のゲームを表します。
/// </summary>
public interface Game : IModuleContainer<Game>, ILifespan
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

    internal void RegisterEntity(IGameEntity entity, ILifespan lifespan);

    /// <summary>
    /// 現在のHUD。
    /// </summary>
    HUD CurrentHud { get; }
}
