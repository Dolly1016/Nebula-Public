using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.VoiceChat;

public class TBCLEvent : Virial.Events.Event
{
    static private int[] playerData = new int[24];
    static private int snapshotData = 0;

    public void UpdatePlayerData(byte playerId, int data) => playerData[playerId] = data;
    public void UpdatePlayerData(Virial.Game.Player player, int data) => playerData[player.PlayerId] = data;
    public void UpdateSnapshotData(int data) => snapshotData = data;
    internal int Read(byte playerId) => playerData[playerId];
    internal int Read() => snapshotData;
    internal TBCLEvent()
    {
        for (int i = 0; i < playerData.Length; i++) playerData[i] = 0;
        snapshotData = 0;
    }
}