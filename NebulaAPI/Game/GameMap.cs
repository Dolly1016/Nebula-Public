using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;

namespace Virial.Game;

public interface GameMap : ILifespan
{
    /// <summary>
    /// 与えられた点のもっともらしい部屋名を返します。
    /// </summary>
    /// <param name="position">部屋名を調べる地点</param>
    /// <param name="detail">より詳細な場所を調べる場合、true</param>
    /// <param name="shortName">省略された部屋名を返させる場合、true</param>
    /// <returns></returns>
    string GetRoomName(Virial.Compat.Vector2 position, bool detail, bool shortName, bool onlyVanillaRoom);

    /// <summary>
    /// 与えられた点のもっともらしい部屋名を返します。
    /// </summary>
    /// <param name="position">部屋名を調べる地点</param>
    /// <param name="detail">より詳細な場所を調べる場合、true</param>
    /// <param name="shortName">省略された部屋名を返させる場合、true</param>
    /// <returns></returns>
    bool GetRoomName(Virial.Compat.Vector2 position, out string roomName, bool detail, bool shortName, bool onlyVanillaRoom);

    /// <summary>
    /// 与えられた点がマップ内(船内)かどうか調べます。
    /// </summary>
    /// <param name="position">マップ内か調べる地点</param>
    /// <param name="radius">点の周囲に必要な空間(円形)の半径</param>
    /// <returns></returns>
    bool IsInMap(Virial.Compat.Vector2 position, float radius = 0.1f);

    /// <summary>
    /// 与えられた2点間に影があるか調べます。
    /// </summary>
    /// <param name="position1"></param>
    /// <param name="position2"></param>
    /// <returns></returns>
    bool AnyShadowsBetween(Virial.Compat.Vector2 position1, Virial.Compat.Vector2 position2);

    /// <summary>
    /// 与えられた2点間に壁があるか調べます。
    /// </summary>
    /// <param name="position1"></param>
    /// <param name="position2"></param>
    /// <returns></returns>
    bool AnyWallsBetween(Virial.Compat.Vector2 position1, Virial.Compat.Vector2 position2);

    Virial.Compat.Vector2 EmergencyButtonPosition { get; }
}
