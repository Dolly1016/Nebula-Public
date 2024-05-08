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
            //不可視の死体はつかめる対象から外す
            var deadBodyTracker = Bind(ObjectTrackers.ForDeadBody(null, role.MyPlayer, d => d.RelatedDeadBody?.GetHolder() == null));

            var dragButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
            dragButton.SetSprite(buttonSprite.GetSprite());
            dragButton.Availability = (button) =>
            {
                return (deadBodyTracker.CurrentTarget != null || role.MyPlayer.HoldingAnyDeadBody) && role.MyPlayer.CanMove;
            };
            dragButton.Visibility = (button) => !role.MyPlayer.IsDead;
            dragButton.OnClick = (button) =>
            {
                if (!role.MyPlayer.HoldingAnyDeadBody)
                {
                    role.MyPlayer.HoldDeadBody(deadBodyTracker.CurrentTarget!);
                    OnHoldingDeadBody?.Invoke(deadBodyTracker.CurrentTarget!.RelatedDeadBody!);
                }
                else
                    role.MyPlayer.ReleaseDeadBody();
            };
            dragButton.OnUpdate = (button) => dragButton.SetLabel(role.MyPlayer.HoldingAnyDeadBody ? "release" : "drag");
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
