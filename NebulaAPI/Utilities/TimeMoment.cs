using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Virial.Utilities;

/// <summary>
/// 現在のゲームで得たTimeMomentのみ正しく機能します。
/// </summary>
public struct TimeMoment
{
    private float time;
    internal TimeMoment(float time)
    {
        this.time = time;
    }

    public TimeMoment()
    {
        this.time = -1000f;
    }

    internal bool ElapsedLessThanInternal(float sec) => time < 0f ? false : time + sec < (NebulaAPI.CurrentGame?.CurrentRawTime ?? 0f);
    internal bool ElapsedMoreThanInternal(float sec) => time < 0f ? false : time + sec > (NebulaAPI.CurrentGame?.CurrentRawTime ?? 0f);

    static public implicit operator float(TimeMoment? moment) => moment?.time ?? 0f;
}

public static class TimeMomentExtensions
{
    public static bool ElapsedLessThan(this TimeMoment? timeMoment, float sec) => timeMoment.HasValue ? timeMoment.Value.ElapsedLessThanInternal(sec) : false;
    public static bool ElapsedMoreThan(this TimeMoment? timeMoment, float sec) => timeMoment.HasValue ? timeMoment.Value.ElapsedMoreThanInternal(sec) : false;

    public static bool ElapsedLessThan(this TimeMoment timeMoment, float sec) => timeMoment.ElapsedLessThanInternal(sec);
    public static bool ElapsedMoreThan(this TimeMoment timeMoment, float sec) => timeMoment.ElapsedMoreThanInternal(sec);
}