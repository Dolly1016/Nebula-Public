using Virial.Compat;

namespace Virial.Configuration;

public interface ISharableEntry
{
    int RawValue { get; }
}

public interface ISharableVariable<T> : ISharableEntry, Reference<T>
{
    T CurrentValue { get; set; }
}