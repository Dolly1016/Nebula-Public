using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Il2CppSystem.Diagnostics.Tracing.TraceLoggingMetadataCollector;

namespace Virial.DI;


/// <summary>
/// モジュールのコンテナを表します。
/// </summary>
/// <typeparam name="Impl">コンテナの公開インターフェース</typeparam>
public interface IModuleContainer<Impl> where Impl : class
{
    T? GetModule<T>() where T : class, IModule<Impl>;

    internal Impl MyImpl => (this as Impl)!;
    internal void AddModule(IModule<Impl> module);


}

/// <summary>
/// コンテナに属するモジュールを表します。
/// </summary>
/// <typeparam name="Container">注入先のコンテナ</typeparam>
public interface IModule<Container>
{
    Container MyContainer { get; }
}

public abstract class AbstractModule<Container> : IModule<Container> where Container : class
{
    private Container container = null!;
    public Container MyContainer => container;

    internal void OnInjected(Container container)
    {
        this.container = container;
    }
}

internal abstract class AbstractModuleContainer<Impl> : IModuleContainer<Impl> where Impl : class
{
    private List<IModule<Impl>> allModules = new();
    private Dictionary<Type, IModule<Impl>> fastModulesMap = new();

    T? IModuleContainer<Impl>.GetModule<T>() where T : class
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

    void IModuleContainer<Impl>.AddModule(IModule<Impl> module)
    {
        allModules.Add(module);
        (module as AbstractModule<Impl>)?.OnInjected((this as IModuleContainer<Impl>).MyImpl);
    }
}

/// <summary>
/// モジュールコンテナとモジュールに関する管理を担います。
/// </summary>
public class DIManager
{
    private record ContainerDefinition(Func<object> Supplier, List<Func<object>> ModuleSuppliers);

    private Dictionary<Type, ContainerDefinition> allDefinitions = new();

    internal ContainerImpl? Instantiate<ContainerImpl>() where ContainerImpl : class
    {
        var type = typeof(ContainerImpl);

        if(allDefinitions.TryGetValue(type, out var def))
        {
            var impl = (def.Supplier.Invoke() as ContainerImpl)!;

            foreach (var m in def.ModuleSuppliers)
                (impl as IModuleContainer<ContainerImpl>)?.AddModule((m.Invoke() as IModule<ContainerImpl>)!);

            return impl;
        }

        return null;
    }

    public bool RegisterModule<ContainerImpl>(Func<IModule<ContainerImpl>> supplier) where ContainerImpl : class
    {
        var type = typeof(ContainerImpl);

        if (allDefinitions.TryGetValue(type, out var def))
        {
            def.ModuleSuppliers.Add(supplier);
            return true;
        }
        return false;
    }

    internal void RegisterContainer<ContainerImpl>(Func<ContainerImpl> supplier) where ContainerImpl : class
    {
        var type = typeof(ContainerImpl);
        allDefinitions[type] = new(supplier, new());
    }
}