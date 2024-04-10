using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Player;


public class TimeLimitedModulator
{
    public float Timer { get; private set; }
    public float MaxTime { get; private set; }
    public bool CanPassMeeting { get; private set; }
    public int Priority { get; private set; }
    public int DuplicateTag { get; private set; }

    public void Update()
    {
        if (Timer < 9999f) Timer -= Time.deltaTime;
    }

    public void OnMeetingStart()
    {
        if (!CanPassMeeting) Timer = -1f;
    }

    public bool IsBroken => Timer < 0f;

    public TimeLimitedModulator(float timer, bool canPassMeeting, int priority, int? duplicateTag)
    {
        this.MaxTime = this.Timer = timer;
        this.CanPassMeeting = canPassMeeting;
        this.Priority = priority;
        this.DuplicateTag = duplicateTag ?? 0;
    }
}

public class SpeedModulator : TimeLimitedModulator
{
    public float Num { get; private set; }
    public bool IsMultiplier { get; private set; }

    public void Calc(ref float speed)
    {
        if (IsMultiplier)
            speed *= Num;
        else
            speed += Num;
    }


    public SpeedModulator(float? num, bool isMultiplier, float timer, bool canPassMeeting, int priority, int duplicateTag = 0) : base(timer, canPassMeeting, priority, duplicateTag)
    {
        this.Num = num ?? 10000f;
        this.IsMultiplier = isMultiplier;
    }

    public bool IsAccelModulator => IsMultiplier ? Num > 1f : Num > 0f;
    public bool IsDecelModulator => IsMultiplier ? Num < 1f : Num < 0f;
}

public class AttributeModulator : TimeLimitedModulator
{
    public IPlayerAttribute Attribute;
    public bool CanBeAware;
    public bool IsPermanent => Timer > 10000f;

    public AttributeModulator(IPlayerAttribute attribute, float timer, bool canPassMeeting, int priority, int duplicateTag = 0, bool canBeAware = true) : base(timer, canPassMeeting, priority, duplicateTag)
    {
        Attribute = attribute;
        CanBeAware = canBeAware;
    }
}
