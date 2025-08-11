using System.Collections;

namespace Virial.Utilities;

public class Variable<T>
{
    public T? Value { get; set; } = default(T);

    public Variable<T> Set(T value)
    {
        Value = value;
        return this;
    }

    public Variable<T> Update(Func<T?, T?> update)
    {
        Value = update.Invoke(Value);
        return this;
    }

    public IEnumerator Wait()
    {
        while (Value == null) yield return null;
        yield break;
    }
}