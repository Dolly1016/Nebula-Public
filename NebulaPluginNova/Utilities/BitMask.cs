namespace Nebula.Utilities;

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

file class BitMask32<T> : EditableBitMask<T>
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

public static class BitMasks
{
    private static Func<GamePlayer, int> gamePlayerConverter = p => 1 << p.PlayerId;
    public static EditableBitMask<GamePlayer> AsPlayer(int bitMask = 0) => new BitMask32<GamePlayer>(gamePlayerConverter, bitMask);
    public static EditableBitMask<PlayerControl> AsPlayerControl(int bitMask = 0) => new BitMask32<PlayerControl>(p => 1 << p.PlayerId, bitMask);
}