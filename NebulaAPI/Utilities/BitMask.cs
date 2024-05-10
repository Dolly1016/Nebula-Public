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
}

public interface EditableBitMask<T> : BitMask<T>
{
    EditableBitMask<T> Add(T value);
    EditableBitMask<T> Clear();
}
