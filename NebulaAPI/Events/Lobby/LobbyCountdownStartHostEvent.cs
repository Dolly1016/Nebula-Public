using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Lobby;

/// <summary>
/// ゲーム開始のカウントダウンが始まるときに発火します。
/// </summary>
public class LobbyCountdownStartHostEvent : Event
{
    public GameParameters Parameters { get; }
    internal LobbyCountdownStartHostEvent(GameParameters parameters)
    {
        this.Parameters = parameters;
    }
}
