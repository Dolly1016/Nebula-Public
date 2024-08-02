using System.Diagnostics.CodeAnalysis;

namespace Virial;

/// <summary>
/// 寿命を持つオブジェクトです
/// </summary>
public interface ILifespan
{
    /// <summary>
    /// 寿命の尽きたオブジェクトはtrueを返します
    /// </summary>
    bool IsDeadObject { get; }

    /// <summary>
    /// 寿命の尽きたオブジェクトはfalseを返します
    /// </summary>
    bool IsAliveObject { get => !IsDeadObject; }
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
    /// 生存している間はtrueを返す関数から作られる寿命付きオブジェクト
    /// </summary>
    /// <param name="predicate">生存している間はtrueを返す関数</param>
    public FunctionalLifespan(Func<bool> predicate)
    {
        this.predicate = predicate;
    }
}

/// <summary>
/// 解放可能なオブジェクトを表します。
/// </summary>
public interface IReleasable
{
    internal void Release();
}

public static class IReleasableExtension
{
    public static void ReleaseIt(this IReleasable releasable) => releasable.Release();
}
/// <summary>
/// 解放可能なオブジェクトを束縛できるオブジェクトです。
/// </summary>
public interface IBinder
{
    [return: NotNullIfNotNull("obj")]
    T? Bind<T>(T? obj) where T : class, IReleasable;
}

internal interface IBinderLifespan : ILifespan, IBinder { }

public class ComponentHolder : IBinder, IReleasable, ILifespan
{
    public bool IsDeadObject { get; private set; } = false;
    private List<IReleasable> myComponent { get; init; } = new();


    [return: NotNullIfNotNull("component")]
    public T? Bind<T>(T? component) where T : class, IReleasable
    {
        if (component == null) return null;

        BindComponent(component);
        return component;
    }
    public void BindComponent(IReleasable component) => myComponent.Add(component);

    protected void ReleaseComponents()
    {
        foreach (var component in myComponent) component.Release();
        myComponent.Clear();
    }

    void IReleasable.Release()
    {
        ReleaseComponents();
        IsDeadObject = true;
    }
}

public class SimpleReleasable : IReleasable, ILifespan
{
    public bool IsDeadObject { get; private set; } = false;

    void IReleasable.Release()
    {
        IsDeadObject = true;
    }
}