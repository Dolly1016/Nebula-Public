using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class ResetKillCooldownLocalEvent : AbstractPlayerEvent
{
    private float? cooldown { get; set; }
    public bool UseDefaultCooldown => !cooldown.HasValue;
    public void SetFixedCooldown(float cooldown) => this.cooldown = cooldown;
    public float? FixedCooldown => this.cooldown;
    internal ResetKillCooldownLocalEvent(Virial.Game.Player player) : base(player)
    {
        this.cooldown = null;
    }
}