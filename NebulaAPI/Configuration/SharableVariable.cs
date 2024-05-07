using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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