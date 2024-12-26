using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;
public class PlayerCheckRoleInfoVisibilityLocalEvent : AbstractPlayerEvent
{
    private bool canSeeTask;
    private bool canSeeRole;
    public bool CanSeeAll { get => canSeeRole && canSeeTask; set { canSeeTask = value; canSeeRole = value; } }
    public bool CanSeeTask { get => canSeeTask; set => canSeeTask = value; }
    public bool CanSeeRole { get => canSeeRole; set => canSeeRole = value; }
    public PlayerCheckRoleInfoVisibilityLocalEvent(Virial.Game.Player player) : base(player)
    {
        canSeeRole = false;
        canSeeTask = false;
    }
}

