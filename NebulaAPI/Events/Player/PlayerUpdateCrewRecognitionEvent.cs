using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーの陣営認識が更新されうるときに発火します。
/// まったく変化していない場合もあります。
/// </summary>
public class PlayerUpdateCrewRecognitionEvent : AbstractPlayerEvent
{
    internal PlayerUpdateCrewRecognitionEvent(Virial.Game.Player player) : base(player)
    {
    }
}
