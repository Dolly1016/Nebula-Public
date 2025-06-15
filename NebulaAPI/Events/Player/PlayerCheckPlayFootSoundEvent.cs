using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using Virial.Text;

namespace Virial.Events.Player;

/// <summary>
/// 足跡を再生しようとするときに発火します。足跡の再生を拒否できます。
/// </summary>
/// <remarks>
/// v3.2.0で追加。<br />
/// </remarks>
public class PlayerCheckPlayFootSoundEvent : AbstractPlayerEvent
{
    public bool PlayFootSound { get; set; } = true;

    internal PlayerCheckPlayFootSoundEvent(Virial.Game.Player player) : base(player)
    {
    }
}

