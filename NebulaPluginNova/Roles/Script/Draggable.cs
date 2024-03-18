using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Roles.Scripts;

public class Draggable : ComponentHolder
{
    static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DragAndDropButton.png", 115f);

    public Action<DeadBody>? OnHoldingDeadBody { get; set; } = null;

    public void OnActivated(RoleInstance role)
    {
        if (role.MyPlayer.AmOwner)
        {
            ObjectTracker<DeadBody> deadBodyTracker = Bind(ObjectTrackers.ForDeadBody(null, role.MyPlayer.MyControl, (d) => d.GetHolder() == null));

            var dragButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
            dragButton.SetSprite(buttonSprite.GetSprite());
            dragButton.Availability = (button) =>
            {
                return (deadBodyTracker.CurrentTarget != null || role.MyPlayer.HoldingDeadBodyId.HasValue) && role.MyPlayer.MyControl.CanMove;
            };
            dragButton.Visibility = (button) => !role.MyPlayer.MyControl.Data.IsDead;
            dragButton.OnClick = (button) =>
            {
                if (!role.MyPlayer.HoldingDeadBodyId.HasValue)
                {
                    role.MyPlayer.HoldDeadBody(deadBodyTracker.CurrentTarget!);
                    OnHoldingDeadBody?.Invoke(deadBodyTracker.CurrentTarget!);
                }
                else
                    role.MyPlayer.ReleaseDeadBody();
            };
            dragButton.OnUpdate = (button) => dragButton.SetLabel(role.MyPlayer.HoldingDeadBodyId.HasValue ? "release" : "drag");
            dragButton.SetLabel("drag");
        }
    }

    public void OnDead(RoleInstance role)
    {
        role.MyPlayer.ReleaseDeadBody();
    }

    public void OnInactivated(RoleInstance role)
    {
        role.MyPlayer.ReleaseDeadBody();
    }
}
