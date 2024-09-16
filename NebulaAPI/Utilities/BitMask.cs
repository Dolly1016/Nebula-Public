using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial;

public interface BitMask<T>
{
    bool Test(T? value);

    bool TestAll(params T?[] values);

    bool TestAll(IEnumerable<T?> values);

    uint AsRawPattern { get; }
    ulong AsRawPatternLong { get; }

    IEnumerable<T> ForEach(IEnumerable<T> all) => all.Where(t => Test(t));
}

public interface EditableBitMask<T> : BitMask<T>
{
    EditableBitMask<T> Add(T value);
    EditableBitMask<T> Clear();
}

public interface IBit32
{
    uint AsBit { get; }
}

public interface IBit64
{
    ulong AsBitLong { get; }
}