using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Dev;

internal class TimeLogger
{
    Stopwatch stopwatch;
    Action<string> logger;
    string name;

    private void Log(string log) => this.logger.Invoke($"[{name}] {log}");
    public TimeLogger(string? name = null, Action<string>? logger = null)
    {
        stopwatch = new();
        this.name = name ?? "Stopwatch";
        this.logger = logger ?? LogUtils.WriteToConsole;
        Log("Start stopwatch");

        stopwatch.Start();
    }

    public void Restart()
    {
        stopwatch.Restart();
    }

    public long Measure(string tag)
    {
        long ticks = stopwatch.ElapsedTicks;
        long millisec = stopwatch.ElapsedMilliseconds;
        Log(tag + " " + ticks + "ticks (" + millisec + "ms)");
        Restart();
        return ticks;
    }

    public bool MeasureIf(string tag, long minMillisec)
    {
        bool returnedValue = false;
        long ticks = stopwatch.ElapsedTicks;
        long millisec = stopwatch.ElapsedMilliseconds;
        if (minMillisec <= millisec) {
            Log(tag + " " + ticks + "ticks (" + millisec + "ms)");
            returnedValue = true;
        }
        Restart();
        return returnedValue;
    }


}
