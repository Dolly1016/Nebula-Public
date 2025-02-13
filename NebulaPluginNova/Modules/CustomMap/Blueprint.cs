namespace Nebula.Modules.CustomMap;

public record FloatVectorRange
{
    public FloatVectorRange(Vector2 from, Vector2 to)
    {
        Min = new Vector2(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y));
        Max = new Vector2(Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y));
    }
    public Vector2 Min { get; private init; }
    public Vector2 Max { get; private init; }

    static public FloatVectorRange zero = new(Vector2.zero, Vector2.zero);

    /// <summary>
    /// 与えられた範囲との重複部分を返します。
    /// 重複部分が無い場合はnullを返します。
    /// </summary>
    /// <param name="range"></param>
    /// <returns></returns>
    public FloatVectorRange? GetOverlappedRange(FloatVectorRange range)
    {
        Vector2 min = new(Mathf.Max(range.Min.x, Min.x), Mathf.Max(range.Min.y, Min.y));
        Vector2 max = new(Mathf.Min(range.Max.x, Max.x), Mathf.Min(range.Max.y, Max.y));
        if (min.x > max.x || min.y > max.y) return null;
        return new(min, max);
    }

    public FloatVectorRange Add(FloatVectorRange range) => new(Min + range.Min, Max + range.Max);
    public FloatVectorRange Sub(FloatVectorRange range) => new(Min - range.Max, Max - range.Min);

    public bool Approximately(FloatVectorRange range) => Mathf.Approximately(Min.x, range.Min.x) && Mathf.Approximately(Min.y, range.Min.y) && Mathf.Approximately(Max.x, range.Max.x) && Mathf.Approximately(Max.y, range.Max.y);
    public override string ToString() => $"[{Min.x} to {Max.x}, {Min.y} to {Max.y}]";
}

public enum Direction
{
    UP = 0,
    DOWN = 1,
    LEFT = 2,
    RIGHT = 3
}
public static class DirectionHelpers
{
    static public Direction Reverse(this Direction direction) => (Direction)(((int)direction / 2) + (1 - ((int)direction % 2)));
}

public record RoomConnection(FloatVectorRange Position, Direction Direction) {
    static public RoomConnection Generate(float pos, float posMin, float posMax, Direction direction)
    {
        if (direction is Direction.UP or Direction.DOWN)
            return new RoomConnection(new FloatVectorRange(new(posMin, pos), new(posMax, pos)), direction);
        else
            return new RoomConnection(new FloatVectorRange(new(pos, posMin), new(pos, posMax)), direction);
    }
}


public class BlueprintRoom
{
    public RoomConnection[] Connection;

    public BlueprintRoom(RoomConnection[] connection)
    {
        Connection = connection;
    }
}

