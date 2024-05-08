using Virial.Game;

namespace Nebula.Player;


public abstract class TimeLimitedModulator
{
    public float Timer { get; private set; }
    public float MaxTime { get; private set; }
    public bool CanPassMeeting { get; private set; }
    public int Priority { get; private set; }
    public string DuplicateTag { get; private set; }
    public bool IsPermanent => Timer > 10000f;

    public abstract bool CanBeAware { get; }

    /// <summary>
    /// 指定の効果を持っているかどうか調べます。
    /// </summary>
    /// <param name="attribute">効果</param>
    /// <returns></returns>
    abstract public bool HasAttribute(IPlayerAttribute attribute);

    /// <summary>
    /// カテゴリ上の、目に見える形で指定の効果を持っているかどうか調べます。
    /// </summary>
    /// <param name="attribute">効果</param>
    /// <param name="watcher">視点</param>
    /// <returns></returns>
    virtual public bool HasCategorizedAttribute(IPlayerAttribute attribute, GamePlayer watcher) => HasAttribute(attribute);

    public void Update()
    {
        if (Timer < 9999f) Timer -= Time.deltaTime;
    }

    public void OnMeetingStart()
    {
        if (!CanPassMeeting) Timer = -1f;
    }

    public bool IsBroken => Timer < 0f;

    public TimeLimitedModulator(float timer, bool canPassMeeting, int priority, string? duplicateTag)
    {
        this.MaxTime = this.Timer = timer;
        this.CanPassMeeting = canPassMeeting;
        this.Priority = priority;
        this.DuplicateTag = duplicateTag ?? "";
    }
}

public class AttributeModulator : TimeLimitedModulator
{
    public IPlayerAttribute Attribute;
    public override bool CanBeAware => canBeAware;
    private bool canBeAware;

    public AttributeModulator(IPlayerAttribute attribute, float timer, bool canPassMeeting, int priority, string? duplicateTag = null, bool canBeAware = true) : base(timer, canPassMeeting, priority, duplicateTag)
    {
        Attribute = attribute;
        this.canBeAware = canBeAware;
    }

    public override bool HasAttribute(IPlayerAttribute attribute) => Attribute == attribute;
    public override bool HasCategorizedAttribute(IPlayerAttribute attribute, GamePlayer watcher) => Attribute.CategorizedAttribute == attribute && Attribute.CanCognize(watcher) && canBeAware;
}

public class SizeModulator : AttributeModulator
{
    public Vector2 Size;
    public bool Smooth;

    public SizeModulator(Vector2 size, float timer, bool canPassMeeting, int priority, string? duplicateTag = null, bool canBeAware = true, bool smooth = true) : base(PlayerAttributes.Size, timer, canPassMeeting, priority, duplicateTag, canBeAware)
    {
        Size = size;
        Smooth = smooth;
    }
}

public class FloatModulator : AttributeModulator
{
    public float Num;

    public FloatModulator(IPlayerAttribute attribute, float num, float timer, bool canPassMeeting, int priority, string? duplicateTag = null, bool canBeAware = true) : base(attribute, timer, canPassMeeting, priority, duplicateTag, canBeAware)
    {
        Num = num;
    }
}

public class SpeedModulator : TimeLimitedModulator
{
    public float Num { get; private set; }
    public float AbsNum { get; private set; }
    public Vector2 DirectionalNum { get; private set; }
    public Vector2 AbsDirectionalNum { get; private set; }
    public bool IsMultiplier { get; private set; }
    public override bool CanBeAware => true;

    public override bool HasAttribute(IPlayerAttribute attribute)
    {
        if (attribute == PlayerAttributes.Accel)
            return IsAccelModulator;
        if (attribute == PlayerAttributes.Decel)
            return IsDecelModulator;
        if (attribute == PlayerAttributes.Drunk)
            return IsInverseModulator;
        return false;
    }

    public void Calc(ref Vector2 directionalPlayerSpeed, ref float speed)
    {
        if (IsMultiplier)
            speed *= Num;
        else
            speed += Num;

        directionalPlayerSpeed *= this.DirectionalNum;
    }


    public SpeedModulator(float? num, Vector2 dirNum, bool isMultiplier, float timer, bool canPassMeeting, int priority, string? duplicateTag = null) : base(timer, canPassMeeting, priority, duplicateTag)
    {
        this.Num = num ?? 10000f;
        this.AbsNum = Mathf.Abs(this.Num);
        this.DirectionalNum = dirNum;
        this.AbsDirectionalNum = new(Mathf.Abs(dirNum.x), Mathf.Abs(dirNum.y));
        this.IsMultiplier = isMultiplier;
    }

    public bool IsAccelModulator => (IsMultiplier ? AbsNum > 1f : AbsNum > 0f) || AbsDirectionalNum.x > 1f || AbsDirectionalNum.y > 1f;
    public bool IsDecelModulator => (IsMultiplier ? AbsNum < 1f : AbsNum < 0f) || AbsDirectionalNum.x < 1f || AbsDirectionalNum.y < 1f;
    public bool IsInverseModulator => (IsMultiplier ? Num < 0f : false) || DirectionalNum.x < 0f || DirectionalNum.y < 0f;
}