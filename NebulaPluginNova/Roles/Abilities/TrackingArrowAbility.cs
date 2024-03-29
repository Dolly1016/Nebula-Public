﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using Virial;

namespace Nebula.Roles.Abilities;

public class TrackingArrowAbility : ComponentHolder, IGameEntity
{
    public GamePlayer MyPlayer => target;

    PlayerModInfo target;
    float interval;
    float timer;
    Arrow arrow = null!;
    Color color;

    public TrackingArrowAbility(PlayerModInfo target, float interval, Color color)
    {
        this.target = target;
        this.interval = interval;
        this.timer = -1f;
        this.color = color;
    }

    void IGameEntity.Update()
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

                arrow.TargetPos = target.MyControl.transform.position;

                timer = interval;
            }
        }

        if (arrow != null) arrow.IsActive = !target.IsDead && !MeetingHud.Instance && !ExileController.Instance;
    }
}
