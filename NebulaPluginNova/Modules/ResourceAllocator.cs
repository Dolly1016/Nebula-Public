using Cpp2IL.Core.Extensions;
using System.Reflection;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class NebulaResourceManager
{
    static IVariableNamespaceAllocator baseAllocator = new NamespaceAllocator();
    static public IResourceAllocator NebulaNamespace = null!;
    static public IResourceAllocator InnerslothNamespace = null!;

    static NebulaResourceManager()
    {
        NebulaNamespace = new NebulaDefaultNamespace();
        InnerslothNamespace = new InnerslothDefaultNamespace();

        RegisterNamespace("Nebula", NebulaNamespace);
        RegisterNamespace("Innersloth", InnerslothNamespace);
    }

    public static void RegisterNamespace(string name, IResourceAllocator allocator) => baseAllocator.Register(name, allocator);
    public static bool RegisterResource(string name, INebulaResource resource)
    {
        string[] splitted = name.Split("::");
        if (splitted.Length == 1) return false;

        IResourceAllocator? allocator = GetAllocator(splitted.Take(splitted.Length - 1), true);
        if (allocator == null) return false;

        if(allocator is IVariableResourceAllocator vra)
        {
            vra.Register(splitted[^1], resource);
            return true;
        }

        return false;
    }

    static public INebulaResource? GetResource(IReadOnlyArray<string> namespaceArray, string name, IResourceAllocator? defaultAllocator)
    {
        return defaultAllocator?.GetResource(namespaceArray, name) ?? baseAllocator.GetResource(namespaceArray, name);
    }

    static public INebulaResource? GetResource(string fullAddress, IResourceAllocator? defaultAllocator = null)
    {
        string[] splitted = fullAddress.Split("::");
        return GetResource(new ReadOnlyArray<string>(splitted, 0, splitted.Length - 1), splitted[^1], defaultAllocator);
    }

    static public INebulaResource? GetResource(string fullAddress, string defaultAllocator) => GetResource(fullAddress, GetAllocator(defaultAllocator, false));

    static public IResourceAllocator? GetAllocator(string fullNamespace, bool generateSpaceIfNotExisted = false) => GetAllocator(fullNamespace.Split("::"), generateSpaceIfNotExisted);

    static public IResourceAllocator? GetAllocator(IEnumerable<string> splitted, bool generateSpaceIfNotExisted = false)
    {
        IResourceAllocator? alloc = baseAllocator;
        foreach(var s in splitted)
        {
            var next = alloc?.GetChildAllocator(s);
            if (next == null)
            {
                if (generateSpaceIfNotExisted && alloc is IVariableNamespaceAllocator vna)
                {
                    next = new VariableResourceAllocator();
                    vna.Register(s, next);
                }
                else
                {
                    return null;
                }
            }

            alloc = next;
        }

        return alloc;
    }

}

public class NebulaDefaultNamespace : VariableResourceAllocator, IResourceAllocator
{
    Dictionary<string, string> allResources;
    public NebulaDefaultNamespace() {
        allResources = [];
        foreach(var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            allResources[name.ToLower()] = name;
        }
    }
    INebulaResource? IResourceAllocator.GetResource(IReadOnlyArray<string> namespaceArray, string name)
    {
        var result = base.GetResource(namespaceArray, name);
        if (result != null) return result;

        if (namespaceArray.Count > 0) return null;

        if(allResources.TryGetValue("nebula.resources." + name.ToLower(), out var path))
            return new StreamResource(() => Assembly.GetExecutingAssembly().GetManifestResourceStream(path));
        return null;
    }
}

public class InnerslothDefaultNamespace : IResourceAllocator
{
    Dictionary<string, INebulaResource> resources = new()
    {
        { "button.vitalsbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.VitalsButton].Image : null!)) },
        { "button.skeldadminbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.AdminMapButton].Image : null!)) },
        { "button.miraadminbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.MIRAAdminButton].Image : null!)) },
        { "button.polusadminbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.PolusAdminButton].Image : null!)) },
        { "button.airshipadminbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.AirshipAdminButton].Image : null!)) },
        { "button.camerabutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.CamsButton].Image : null!)) },
        { "button.doorlogsbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.DoorLogsButton].Image : null!)) },
        { "button.usebutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.UseButton].Image : null!)) },
        { "button.petbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.PetButton].Image : null!)) },
        { "button.killbutton", new VanillaImageResource(new WrapSpriteLoader(() => HudManager.InstanceExists ? HudManager.Instance.KillButton.graphic.sprite : null!)) }
    };
    INebulaResource? IResourceAllocator.GetResource(IReadOnlyArray<string> namespaceArray, string name)
    {
        if (namespaceArray.Count > 0) return null;

        if(resources.TryGetValue(name.ToLower(), out var resource)) return resource;
        return null;
    }
}

public class StreamResource : INebulaResource {
    Func<Stream?> streamGetter;

    public StreamResource(Func<Stream?> streamGetter)
    {
        this.streamGetter = streamGetter;
    }

    Stream? INebulaResource.AsStream()
    {
        return streamGetter.Invoke();
    }

    Image? INebulaResource.AsImage(float defaultPixsPerUnit)
    {
        Stream? stream = streamGetter.Invoke();
        if (stream == null) return null;
        return new SpriteLoader(new UnloadTextureLoader(stream.ReadBytes()), defaultPixsPerUnit);
    }

    MultiImage? INebulaResource.AsMultiImage(int x, int y, float defaultPixsPerUnit)
    {
        Stream? stream = streamGetter.Invoke();
        if (stream == null) return null;
        return new DividedSpriteLoader(new UnloadTextureLoader(stream.ReadBytes()), defaultPixsPerUnit, x, y);
    }
}

public class VanillaImageResource : INebulaResource
{
    WrapSpriteLoader imageGetter;

    public VanillaImageResource(WrapSpriteLoader imageGetter)
    {
        this.imageGetter = imageGetter;
    }

    Image? INebulaResource.AsImage(float defaultPixsPerUnit = 100f)
    {
        return imageGetter;
    }
}