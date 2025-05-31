using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Virial.DI;

public interface IModuleContainer
{
    internal void AddModule(object module);
    T? GetModule<T>() where T : class, IModule;
}

/// <summary>
/// モジュールを表します。
/// </summary>
public interface IModule
{
}

/// <summary>
/// コンテナに属するモジュールを表します。
/// </summary>
/// <typeparam name="Container">注入先のコンテナ</typeparam>
public interface IGenericModule<Container> : IModule
{
    Container MyContainer { get; }
}

internal interface IInjectable
{
    void OnInjectTo(object container);
}

public abstract class AbstractModule<Container> : IGenericModule<Container>, IInjectable where Container : class
{
    private Container container = null!;
    public Container MyContainer => container;

    void IInjectable.OnInjectTo(object container)
    {
        this.container = (container as Container)!;
        OnInjected(MyContainer);
    }

    virtual protected void OnInjected(Container container) { }
}

internal abstract class AbstractModuleContainer : IModuleContainer
{
    private List<object> allModules = new();
    private Dictionary<Type, object> fastModulesMap = new();

    T? IModuleContainer.GetModule<T>() where T : class
    {
        var type = typeof(T);
        
        //既に登録されていればそのまま返す。
        if (fastModulesMap.TryGetValue(type, out var mod))
            return (mod as T)!;

        //全モジュールから条件に当てはまるものを探して返す。次に備えてキャッシュしておく。
        foreach(var module in allModules)
        {
            var casted = module as T;
            if (casted is null) continue;
            fastModulesMap[type] = casted;
            return casted;
        }

        return null;
    }

    void IModuleContainer.AddModule(object module)
    {
        allModules.Add(module);
        (module as IInjectable)?.OnInjectTo(this);
    }
}

/// <summary>
/// モジュールコンテナとモジュールに関する管理を担います。
/// </summary>
public class DIManager
{
    static internal DIManager Instance { get; private set; } = new();

    private record ContainerDefinition(Func<object> Supplier, List<Func<object>> ModuleSuppliers);

    private Dictionary<Type, Func<object>> allContainers = new();
    private Dictionary<Type, List<Func<object>>> allInterfaces = new();

    public ContainerImpl? Instantiate<ContainerImpl>(Action<ContainerImpl>? preprocess = null) where ContainerImpl : class
        => Instantiate(typeof(ContainerImpl), container => preprocess?.Invoke((container as ContainerImpl)!)) as ContainerImpl;

    internal object? Instantiate(Type type, Action<object>? preprocess = null)
    {
        if (!allContainers.TryGetValue(type, out var def))
        {
            var cand = allContainers.Where(e => type.IsAssignableFrom(e.Key)).ToArray();
            if (cand.Length == 0) return null;

            // TODO: 複数の実装がある場合の処理を考える。
            def = cand[0].Value;
        }

        var impl = def.Invoke();
        preprocess?.Invoke(impl);

        var implAsContainer = (impl as IModuleContainer)!;

        HashSet<Type> types = new();
        void InjectFromInterface(Type myType)
        {
            foreach (var t in myType.GetInterfaces())
            {
                if (types.Contains(t) || t.GetGenericArguments().Length > 0) continue;
                types.Add(t);
                InjectFromInterface(t);

                if (allInterfaces.TryGetValue(t, out var modules))
                {
                    foreach (var m in modules) implAsContainer.AddModule(m.Invoke());
                }
            }
        }

        InjectFromInterface(impl.GetType());
        return impl;
    }

    public bool RegisterModule<Container>(Func<IGenericModule<Container>> supplier) => RegisterGeneralModule<Container>(supplier);

    public bool RegisterGeneralModule<Container>(Func<IModule> supplier)
    {
        var type = typeof(Container);

        if (!allInterfaces.ContainsKey(type)) allInterfaces[type] = new();
        allInterfaces[type].Add(supplier);
        return true;
    }

    public void RegisterContainer<ContainerImpl>(Func<ContainerImpl> supplier) where ContainerImpl : class, IModuleContainer
    {
        var type = typeof(ContainerImpl);

        allContainers[type] = supplier;
    }
}