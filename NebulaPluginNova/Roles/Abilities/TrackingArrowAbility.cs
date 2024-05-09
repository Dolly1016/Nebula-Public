using Virial.Game;
using Virial;
using Virial.Events.Game;

namespace Nebula.Roles.Abilities;

public class TrackingArrowAbility : ComponentHolder, IGameOperator
{
    public GamePlayer MyPlayer => target;

    GamePlayer target;
    float interval;
    float timer;
    Arrow arrow = null!;
    Color color;

    public TrackingArrowAbility(GamePlayer target, float interval, Color color)
    {
        this.target = target;
        this.interval = interval;
        this.timer = -1f;
        this.color = color;
    }

    void Update(GameUpdateEvent ev)
    {
        if (ExileController.Instance)
        {
            timer = -1f;
        }
        else
        {
            timer -= Time.deltaTime;

            if (timer < 0f)
            {
                if (arrow == null)
                {
                    arrow = Bind(new Arrow());
                    arrow.SetColor(color);
                }

                arrow.TargetPos = target.Position;

                timer = interval;
            }
        }

        if (arrow != null) arrow.IsActive = !target.IsDead && !MeetingHud.Instance && !ExileController.Instance;
    }
}
