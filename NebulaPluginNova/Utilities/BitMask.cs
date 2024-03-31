using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace Nebula.Utilities;

public interface BitMask<T>
{
    BitMask<T> Add(T value);

    bool Test(T? value);

    bool TestAll(params T?[] values);

    bool TestAll(IEnumerable<T?> values);
}

file class BitMask32<T> : BitMask<T>
{
    private Func<T, int> converter;
    private int bitMask = 0;

    public BitMask32(Func<T, int> converter, int bitMask)
    {
        this.converter = converter;
        this.bitMask = bitMask;
    }

    BitMask<T> BitMask<T>.Add(T? value)
    {
        if (value == null) return this;
        bitMask |= converter.Invoke(value);
        return this;
    }

    private bool Test(T? value) => value != null ? (bitMask & converter.Invoke(value)) != 0 : false;
    bool BitMask<T>.Test(T? value) => Test(value);

    bool BitMask<T>.TestAll(params T?[] values)
    {
        return values.All(v => Test(v));
    }

    bool BitMask<T>.TestAll(IEnumerable<T?> values)
    {
        return values.All(v => Test(v));
    }

    
}

public static class BitMasks
{
    private static Func<GamePlayer, int> gamePlayerConverter = p => 1 << p.PlayerId;
    public static BitMask<GamePlayer> AsPlayer(int bitMask = 0) => new BitMask32<GamePlayer>(gamePlayerConverter, bitMask);
}