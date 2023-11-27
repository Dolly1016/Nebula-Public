using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Components;

public interface GameTimer : IReleasable
{
    GameTimer SetAsKillCoolTimer();
    GameTimer SetAsAbilityTimer();
    GameTimer Start(float? time = null);
    GameTimer Pause();
    GameTimer Resume();
    GameTimer SetRange(float min, float max);
    GameTimer SetTime(float time);
    GameTimer Expand(float time);
    float CurrentTime { get; }
    float Percentage { get; }
    bool IsInProcess { get; }
}
