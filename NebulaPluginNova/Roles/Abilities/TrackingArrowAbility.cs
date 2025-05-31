using Virial.Game;
using Virial;
using Virial.Events.Game;

namespace Nebula.Roles.Abilities;

public class TrackingArrowAbility : FlexibleLifespan, IGameOperator
{
    public GamePlayer MyPlayer => target;

    GamePlayer target;
    float interval;
    float timer;
    Arrow arrow = null!;
    Color color;
    bool showPlayerIcon;

    public TrackingArrowAbility(GamePlayer target, float interval, Color color, bool showPlayerIcon = true)
    {
        this.target = target;
        this.interval = interval;
        this.timer = -1f;
        this.color = color;
        this.showPlayerIcon = showPlayerIcon;
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
                    if (showPlayerIcon)
                    {
                        arrow = new Arrow(null, false, true) { IsAffectedByComms = false }.HideArrowSprite().Register(this);
                        arrow.SetSmallColor(color);
                        AmongUsUtil.GetPlayerIcon(target.DefaultOutfit.outfit, arrow.ArrowObject.transform, new(0f, 0f, -1f), new(0.24f, 0.24f, 1f));
                    }
                    else
                    {
                        arrow = new Arrow(null, true) { IsAffectedByComms = false }.SetColor(color).Register(this);
                    }
                }

                arrow.TargetPos = target.Position;

                timer = interval;
            }
        }

        if (arrow != null) arrow.IsActive = !target.IsDead && !MeetingHud.Instance && !ExileController.Instance;
    }
}
