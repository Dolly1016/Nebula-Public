using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTryVanillaKillLocalEventAbstractPlayerEvent : AbstractPlayerEvent, ICancelableEvent
{
    public Virial.Game.Player Target { get; private init; }
    internal PlayerTryVanillaKillLocalEventAbstractPlayerEvent(Virial.Game.Player killer, Virial.Game.Player target) : base(killer)
    {
        this.Target = target;
    }
    private bool isCanceled = false;
    public bool IsCanceled => isCanceled;
    private bool resetCooldown = true;
    public bool ResetCooldown => resetCooldown;
    public void Cancel(bool resetCooldown = false)
    {
        isCanceled = true;
        this.resetCooldown = resetCooldown;
    }
    
}