using Virial;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Scripts;

public class Draggable : FlexibleLifespan, IGameOperator, IBindPlayer
{
    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DragAndDropButton.png", 115f);

    public Action<DeadBody>? OnHoldingDeadBody { get; set; } = null;
    private GamePlayer myPlayer;
    GamePlayer IBindPlayer.MyPlayer => myPlayer;

    public Draggable(GamePlayer player)
    {
        this.myPlayer = player;

        if (player.AmOwner)
        {
            //不可視の死体はつかめる対象から外す
            var deadBodyTracker = ObjectTrackers.ForDeadBody(null, player, d => d.RelatedDeadBody?.GetHolder() == null).Register(this);

            var dragButton = NebulaAPI.Modules.AbilityButton(this, myPlayer, Virial.Compat.VirtualKeyInput.Ability,
                0f, "drag", buttonSprite,
                _ => deadBodyTracker.CurrentTarget != null || player.HoldingAnyDeadBody, null);
            dragButton.OnClick = (button) =>
            {
                if (!player.HoldingAnyDeadBody)
                {
                    player.HoldDeadBody(deadBodyTracker.CurrentTarget!);
                    OnHoldingDeadBody?.Invoke(deadBodyTracker.CurrentTarget!.RelatedDeadBody!);
                }
                else
                    player.ReleaseDeadBody();
            };
            dragButton.OnUpdate = (button) => dragButton.SetLabel(player.HoldingAnyDeadBody ? "release" : "drag");
        }
    }

    [OnlyMyPlayer, Local]
    void OnDead(PlayerDieEvent ev)
    {
        myPlayer.ReleaseDeadBody();
    }

    void IGameOperator.OnReleased() => myPlayer.ReleaseDeadBody();
}
