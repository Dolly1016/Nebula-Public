using Nebula.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.DI;
using Virial.Game;

namespace Nebula.Game;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class GameMapImpl : GameMap
{
    internal GameMapImpl()
    {
        ModSingleton<GameMap>.Instance = this;
    }

    bool ILifespan.IsDeadObject => ModSingleton<GameMap>.Instance != this || !ShipStatus.Instance;

    string GameMap.GetRoomName(Virial.Compat.Vector2 position, bool detail, bool shortName, bool onlyVanillaRoom) => AmongUsUtil.GetRoomName(position, detail, shortName, onlyVanillaRoom);

    bool GameMap.GetRoomName(Virial.Compat.Vector2 position, out string roomName, bool detail, bool shortName, bool onlyVanillaRoom) => AmongUsUtil.GetRoomName(position, out roomName, detail, shortName, onlyVanillaRoom);

    bool GameMap.IsInMap(Virial.Compat.Vector2 position, float radius) => MapData.GetCurrentMapData().CheckMapArea(position, radius);

    bool GameMap.AnyWallsBetween(Virial.Compat.Vector2 position1, Virial.Compat.Vector2 position2) => Helpers.AnyNonTriggersBetween(position1, position2, out _);
    bool GameMap.AnyShadowsBetween(Virial.Compat.Vector2 position1, Virial.Compat.Vector2 position2) => Helpers.AnyNonTriggersBetween(position1, position2, out _, Constants.ShadowMask);
    Virial.Compat.Vector2 GameMap.EmergencyButtonPosition => ShipStatus.Instance.EmergencyButton.transform.position;
}
