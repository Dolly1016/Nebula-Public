using Virial.Command;
using Virial.Compat;

namespace Virial.Media;

/// <summary>
/// 名前に紐づけられたリソースを取得します。
/// </summary>
public interface INebulaResource
{
    /// <summary>
    /// ストリームとして取得します。
    /// </summary>
    /// <returns></returns>
    Stream? AsStream() => null;

    /// <summary>
    /// 画像として取得します。
    /// </summary>
    /// <returns></returns>
    Image? AsImage(float defaultPixsPerUnit = 100f) => null;

    /// <summary>
    /// 複数枚からなる画像として取得します。
    /// </summary>
    /// <returns></returns>
    MultiImage? AsMultiImage(int x, int y, float defaultPixsPerUnit = 100f) => null;

    /// <summary>
    /// 文字列として取得します。
    /// </summary>
    /// <returns></returns>
    string? AsString() => null;

    /// <summary>
    /// 実行可能オブジェクトとして取得します。
    /// </summary>
    /// <returns></returns>
    IExecutable? AsExecutable() => null;

    /// <summary>
    /// コマンドトークンとして取得します。
    /// </summary>
    /// <returns></returns>
    ICommandToken? AsCommandToken() => null;

    /// <summary>
    /// コマンドとして取得します。
    /// </summary>
    /// <returns></returns>
    ICommand? AsCommand() => null;
}

/// <summary>
/// リソースのアロケータを表します。
/// </summary>
public interface IResourceAllocator
{
    /// <summary>
    /// アドレスからリソースを取得します。
    /// </summary>
    /// <param name="namespaceArray">名前空間を格納した配列。</param>
    /// <param name="name">名前。</param>
    /// <returns></returns>
    internal INebulaResource? GetResource(IReadOnlyArray<string> namespaceArray, string name);
    /// <summary>
    /// アドレスからリソースを取得します。
    /// </summary>
    /// <param name="name">名前。</param>
    /// <returns></returns>
    INebulaResource? GetResource(string name)
    {
        string[] splitted = name.Split("::");
        return GetResource(new ReadOnlyArray<string>(splitted, 0, splitted.Length - 1), splitted[^1]);
    }
    /// <summary>
    /// 名前から紐づく子のアロケータを取得します。
    /// </summary>
    /// <param name="name">子の名前。</param>
    /// <returns></returns>
    internal IResourceAllocator? GetChildAllocator(string name) => null;
}

public interface IVariableNamespaceAllocator : IResourceAllocator
{
    void Register(string name, IResourceAllocator allocator);
}

public interface IVariableResourceAllocator : IResourceAllocator
{
    void Register(string name, INebulaResource resource);
}

/// <summary>
/// 子空間を持つのみのアロケータ
/// </summary>
public class NamespaceAllocator : IResourceAllocator, IVariableNamespaceAllocator
{
    Dictionary<string, IResourceAllocator> children = new();

    protected virtual INebulaResource? GetResource(IReadOnlyArray<string> namespaceArray, string name)
    {
        if (namespaceArray.Count == 0) return null;
        if (children.TryGetValue(namespaceArray[0].ToLower(), out var allocator)) return allocator.GetResource(namespaceArray.Skip(1), name);
        return null;
    }

    INebulaResource? IResourceAllocator.GetResource(IReadOnlyArray<string> namespaceArray, string name) => GetResource(namespaceArray, name);

    void IVariableNamespaceAllocator.Register(string name, IResourceAllocator allocator) => children.TryAdd(name.ToLower(), allocator);
    IResourceAllocator? IResourceAllocator.GetChildAllocator(string name)
    {
        if (children.TryGetValue(name.ToLower(), out var allocator)) return allocator;
        return null;
    }
}

public class VariableResourceAllocator : NamespaceAllocator, IVariableResourceAllocator, IResourceAllocator
{
    Dictionary<string,  INebulaResource> resources = new();

    protected override INebulaResource? GetResource(IReadOnlyArray<string> namespaceArray, string name)
    {
        var result = base.GetResource(namespaceArray, name);
        if(result != null) return result;

        if (namespaceArray.Count > 0) return null;
        if (resources.TryGetValue(name.ToLower(), out var resource)) return resource;
        return null;
    }

    INebulaResource? IResourceAllocator.GetResource(IReadOnlyArray<string> namespaceArray, string name) => GetResource(namespaceArray, name);
    void IVariableResourceAllocator.Register(string name, Virial.Media.INebulaResource resource)
    {
        resources.TryAdd(name.ToLower(), resource);
    }
}