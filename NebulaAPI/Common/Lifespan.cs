using System.Diagnostics.CodeAnalysis;

namespace Virial;

/// <summary>
/// 寿命オブジェクトを表します。
/// </summary>
public interface ILifespan
{
    /// <summary>
    /// 寿命の尽きたオブジェクトは<c>true</c>を返します。
    /// </summary>
    bool IsDeadObject { get; }

    /// <summary>
    /// 存命中のオブジェクトは<c>true</c>を返します。
    /// </summary>
    bool IsAliveObject { get => !IsDeadObject; }
}

/// <summary>
/// 入れ子状になった寿命オブジェクトを表します。
/// </summary>
public interface INestedLifespan : ILifespan
{
    bool Bind(ILifespan lifespan);
}

internal class GameObjectLifespan : ILifespan
{
    UnityEngine.GameObject obj;
    public GameObjectLifespan(UnityEngine.GameObject obj) { this.obj = obj; }
    public bool IsDeadObject => !obj;
}

/// <summary>
/// 関数によって定義される寿命付きオブジェクトです。
/// </summary>
public class FunctionalLifespan : ILifespan
{
    Func<bool> predicate;
    bool isDeadObject = false;

    /// <summary>
    /// 一度寿命が尽きたオブジェクトは復活しません。
    /// </summary>
    public bool IsDeadObject
    {
        get { 
            if(!isDeadObject) isDeadObject = !predicate.Invoke();
            return isDeadObject;
        }
    }

    /// <summary>
    /// 生存している間はtrueを返す関数から作られる寿命付きオブジェクトを生成します。
    /// </summary>
    /// <param name="predicate">生存している間はtrueを返す関数。</param>
    public FunctionalLifespan(Func<bool> predicate)
    {
        this.predicate = predicate;
    }

    /// <summary>
    /// 指定の時間だけ生存する寿命オブジェクトを生成します。
    /// </summary>
    /// <param name="duration"></param>
    /// <returns></returns>
    public static ILifespan GetTimeLifespan(float duration)
    {
        float end = UnityEngine.Time.time + duration;
        return new FunctionalLifespan(() => UnityEngine.Time.time < end);

    }
}

/// <summary>
/// 任意のタイミングで解放できるオブジェクトを表します。
/// 能動的に寿命が切れる寿命オブジェクトが実装すべきインターフェースです。
/// </summary>
public interface IReleasable
{
    void Release();
}

/// <summary>
/// ごく単純な寿命オブジェクトを表します。
/// Release操作によってのみキルできます。
/// </summary>
public class SimpleLifespan : ILifespan, IReleasable
{
    private bool isDeadObject = false;
    public bool IsDeadObject => isDeadObject;
    public void Release()
    {
        if (isDeadObject) return;
        isDeadObject = true;
        OnReleased();
    }

    /// <summary>
    /// 解放されたときに呼び出されます。
    /// 一度だけ呼び出されます。
    /// </summary>
    virtual protected void OnReleased() { }
}

/// <summary>
/// 他の寿命オブジェクトにキルのタイミングを委ねる寿命オブジェクトです。
/// 委ねる寿命オブジェクトを注入されるまでは少なくとも生存します。
/// </summary>
public class DependentLifespan : INestedLifespan
{
    private ILifespan? parentLifespan = null;
    public bool IsDeadObject => parentLifespan?.IsDeadObject ?? false;
    public bool Bind(ILifespan lifespan)
    {
        if (IsDeadObject) return false;
        this.parentLifespan = lifespan;
        return true;
    }
}

/// <summary>
/// 注入された寿命オブジェクトの寿命が尽きるか、Release操作によってキルできます。
/// </summary>
public class FlexibleLifespan : DependentLifespan, INestedLifespan, IReleasable
{
    private bool isDead = false;
    public bool IsDeadObject => isDead || base.IsDeadObject;
    public void Release()
    {
        isDead = true;
    }
}