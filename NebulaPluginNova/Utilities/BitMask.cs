using UnityEngine.UI;
using Virial;
using Virial.Configuration;

namespace Nebula.Utilities;


internal class BitMask32<T> : EditableBitMask<T>
{
    private Func<T, int> converter;
    private int bitMask = 0;

    public BitMask32(Func<T, int> converter, int bitMask)
    {
        this.converter = converter;
        this.bitMask = bitMask;
    }

    EditableBitMask<T> EditableBitMask<T>.Add(T? value)
    {
        if (value == null) return this;
        bitMask |= converter.Invoke(value);
        return this;
    }

    EditableBitMask<T> EditableBitMask<T>.Clear()
    {
        bitMask = 0;
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


public class FunctionalMask<T> : BitMask<T>
{
    Predicate<T?> predicate;

    public FunctionalMask(Predicate<T?> predicate)
    {
        this.predicate = predicate;
    }

    bool BitMask<T>.Test(T? value)
    {
        return predicate(value);
    }

    bool BitMask<T>.TestAll(params T?[] values)
    {
        return values.All(p => predicate(p));
    }

    bool BitMask<T>.TestAll(IEnumerable<T?> values)
    {
        return values.All(p => predicate(p));
    }
}

public class HashSetMask<T> : EditableBitMask<T>
{
    HashSet<T> set = new();

    public HashSetMask(){}

    bool BitMask<T>.Test(T? value)
    {
        return set.Contains(value!);
    }

    bool BitMask<T>.TestAll(params T?[] values)
    {
        return values.All(p => set.Contains(p!));
    }

    bool BitMask<T>.TestAll(IEnumerable<T?> values)
    {
        return values.All(p => set.Contains(p!));
    }

    EditableBitMask<T> EditableBitMask<T>.Add(T? value)
    {
        if (value == null) return this;
        set.Add(value);
        return this;
    }

    EditableBitMask<T> EditableBitMask<T>.Clear()
    {
        set.Clear();
        return this;
    }
}

public static class BitMasks
{
    private static Func<GamePlayer, int> gamePlayerConverter = p => 1 << p.PlayerId;
    public static EditableBitMask<GamePlayer> AsPlayer(int bitMask = 0) => new BitMask32<GamePlayer>(gamePlayerConverter, bitMask);
    public static EditableBitMask<PlayerControl> AsPlayerControl(int bitMask = 0) => new BitMask32<PlayerControl>(p => 1 << p.PlayerId, bitMask);

    public static BitMask<ConfigurationTab> Bits(params ConfigurationTab[] tabs)
    {
        return new BitMask32<ConfigurationTab>(t => t.AsBit, tabs.Aggregate(0, (val, t) => val | t.AsBit));
    }
}