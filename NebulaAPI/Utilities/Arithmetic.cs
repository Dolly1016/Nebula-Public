using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public interface IFunctionalValue<T>
{
    T Value { get; }
}

file class ConstantValue<T> : IFunctionalValue<T> where T : struct
{
    private T value;
    public ConstantValue(T value)
    {
        this.value = value;
    }
    public T Value => value;
}

file class FunctionalValue<T> : IFunctionalValue<T> where T : struct
{
    private Func<float, T> valueFunc;
    private long startTime = Stopwatch.GetTimestamp();
    private float duration;
    private T goalValue;
    private bool isEnd = false;
    public FunctionalValue(Func<float, T> valueFunc, float duration, T goalValue)
    {
        this.valueFunc = valueFunc;
        this.duration = duration;
        this.goalValue = goalValue;
    }
    public T Value { get {
            if (isEnd) return goalValue;
            var elapsedTicks = Stopwatch.GetTimestamp() - startTime;
            var elapsedSeconds = (float)elapsedTicks / Stopwatch.Frequency;
            if(elapsedSeconds < duration)
            {
                return valueFunc(elapsedSeconds);
            }
            else
            {
                isEnd = true;
                return goalValue;
            }
        }
    }
}

file class SequentialValue<T> : IFunctionalValue<T> where T : struct
{
    private readonly (Func<IFunctionalValue<T>> value, float duration)[] values;
    private IFunctionalValue<T> currentValue;
    private int index;
    private long startTime;
    
    public SequentialValue(params (Func<IFunctionalValue<T>> value,float duration)[] values)
    {
        this.values = values;
        this.index = 0;
        StartCurrentIndex();
    }

    private void StartCurrentIndex()
    {
        if (index < values.Length)
        {
            this.currentValue = values[index].value.Invoke();
            this.startTime = Stopwatch.GetTimestamp();
        }
    }

    public T Value
    {
        get
        {
            if (index < values.Length)
            {
                var elapsedTicks = Stopwatch.GetTimestamp() - startTime;
                var elapsedSeconds = (float)elapsedTicks / Stopwatch.Frequency;
                if (elapsedSeconds > values[index].duration)
                {
                    index++;
                    StartCurrentIndex();
                }
            }
            return currentValue.Value;
            
        }
    }
}

static class Arithmetic
{
    static public IFunctionalValue<float> Decel(float from, float to, float duration) => new FunctionalValue<float>(
        t =>
        {
            float p = 1f - (t / duration);
            p *= p;
            p = 1f - p;
            return from + (to - from) * p;
        },
        duration,
        to
    );

    static public IFunctionalValue<float> Timer(float duration) => new FunctionalValue<float>(
        t =>
        {
            return duration - t;
        },
        duration,
        0
    );

    static public IFunctionalValue<T> Constant<T>(T value) where T : struct => new ConstantValue<T>(value);

    static public IFunctionalValue<T> Sequential<T>(params (Func<IFunctionalValue<T>> value, float duration)[] values) where T : struct => new SequentialValue<T>(values);
    static public readonly IFunctionalValue<float> FloatZero = Constant(0f);
    static public readonly IFunctionalValue<float> FloatOne = Constant(1f);
}
