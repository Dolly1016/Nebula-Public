using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

public class MeetingMapVotesHostEvent : Event
{
    private Dictionary<byte, byte> map = [];
    internal Dictionary<byte, byte> RawMap => map;
    internal MeetingMapVotesHostEvent() { }

    
    public void Map(Virial.Game.Player targetPlayer, Virial.Game.Player refPlayer)
    {
        map[targetPlayer.PlayerId] = refPlayer.PlayerId; 
    }

    public void Swap(Virial.Game.Player player1, Virial.Game.Player player2)
    {
        if (!map.TryGetValue(player1.PlayerId, out var val1)) val1 = player1.PlayerId;
        if (!map.TryGetValue(player2.PlayerId, out var val2)) val2 = player2.PlayerId;
        map[player1.PlayerId] = val2;
        map[player2.PlayerId] = val1;
    }
}
