using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial;

/// <summary>
/// 部分集合を表すマスクです。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface BitMask<T>
{
    bool Test(T? value);

    bool TestAll(params T?[] values);

    bool TestAll(IEnumerable<T?> values);

    internal uint AsRawPattern { get; }
    internal ulong AsRawPatternLong { get; }

    IEnumerable<T> ForEach(IEnumerable<T> all) => all.Where(t => Test(t));
}

/// <summary>
/// 編集可能なマスクです。
/// 要素を追加する操作が可能です。
/// </summary>
/// <typeparam name="T"></typeparam>
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

/// <summary>
/// ビットマスクを生成するAPI群です。
/// </summary>
public static class BitMasks
{
    private static Func<Game.Player, uint> gamePlayerConverter = p => 1u << p.PlayerId;
    public static EditableBitMask<T> CreateMask<T>(Func<T, uint> converter) => new BitMask32<T>(converter, 0u);
    public static BitMask<T> CreateFunctionalMask<T>(Predicate<T> predicate) => new FunctionalMask<T>(predicate);
    public static EditableBitMask<Game.Player> AsPlayer(uint bitMask = 0) => new BitMask32<Game.Player>(gamePlayerConverter, bitMask);
    internal static EditableBitMask<PlayerControl> AsPlayerControl(uint bitMask = 0) => new BitMask32<PlayerControl>(p => 1u << p.PlayerId, bitMask);

    public static BitMask<B> Bits<B>(params B[] elements) where B : IBit32 => new BitMask32<B>(t => t.AsBit, elements.Aggregate(0u, (val, t) => val | t.AsBit));
}


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


internal class FunctionalMask<T> : BitMask<T>
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

internal class EmptyMask<T> : BitMask<T>
{

    public EmptyMask()
    {
    }

    bool BitMask<T>.Test(T? value) => false;

    bool BitMask<T>.TestAll(params T?[] values) => false;

    bool BitMask<T>.TestAll(IEnumerable<T?> values) => false;

    uint BitMask<T>.AsRawPattern => 0u;
    ulong BitMask<T>.AsRawPatternLong => 0ul;
}

internal class HashSetMask<T> : EditableBitMask<T>
{
    HashSet<T> set = new();

    public HashSetMask() { }

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