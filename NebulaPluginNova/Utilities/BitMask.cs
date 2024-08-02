using UnityEngine.UI;
using Virial;
using Virial.Configuration;

namespace Nebula.Utilities;


internal class BitMask32<T> : EditableBitMask<T>
{
    private Func<T, uint> converter;
    private uint bitMask = 0;

    public BitMask32(Func<T, uint> converter, uint bitMask)
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

    uint BitMask<T>.AsRawPattern => bitMask;
    ulong BitMask<T>.AsRawPatternLong => (ulong)bitMask;
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

    uint BitMask<T>.AsRawPattern => throw new NotImplementedException();
    ulong BitMask<T>.AsRawPatternLong => throw new NotImplementedException();
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

    public int Count => set.Count;

    uint BitMask<T>.AsRawPattern => throw new NotImplementedException();
    ulong BitMask<T>.AsRawPatternLong => throw new NotImplementedException();
}

public static class BitMasks
{
    private static Func<GamePlayer, uint> gamePlayerConverter = p => 1u << p.PlayerId;
    public static EditableBitMask<GamePlayer> AsPlayer(uint bitMask = 0) => new BitMask32<GamePlayer>(gamePlayerConverter, bitMask);
    public static EditableBitMask<PlayerControl> AsPlayerControl(uint bitMask = 0) => new BitMask32<PlayerControl>(p => 1u << p.PlayerId, bitMask);

    public static BitMask<B> Bits<B>(params B[] tabs) where B : IBit32 => new BitMask32<B>(t => t.AsBit, tabs.Aggregate(0u, (val, t) => val | t.AsBit));
    
}