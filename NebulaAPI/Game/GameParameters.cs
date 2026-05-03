using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

/// <summary>
/// ゲームの情報を表します。
/// </summary>
/// <param name="MapId"></param>
/// <param name="Impostors"></param>
/// <param name="Players"></param>
public record GameParameters(byte MapId, int Impostors, int Players)
{
    public bool IsSkeld => MapId == 0;
    public bool IsMIRA => MapId == 1;
    public bool IsPolus => MapId == 2;
    public bool IsAirship => MapId == 4;
    public bool IsFungle => MapId == 5;
}
