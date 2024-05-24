using Nebula.Compat;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Text;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    private static List<object> allModules = new();
    private static Dictionary<Type, object> moduleFastMap = new();

    public NebulaImpl()
    {
        Instance = this;

        allModules.AddRange([Nebula.Modules.Language.API, GUI.API, ConfigurationsAPI.API]);
    }

    public string APIVersion => typeof(NebulaAPI).Assembly.GetName().Version?.ToString() ?? "Undefined";

    public Virial.Media.IResourceAllocator NebulaAsset => NebulaResourceManager.NebulaNamespace;

    public Virial.Media.IResourceAllocator InnerslothAsset => NebulaResourceManager.InnerslothNamespace;

    public Virial.Media.IResourceAllocator? GetAddonResource(string addonId)
    {
        return NebulaAddon.GetAddon(addonId);
    }

    T? INebula.Get<T>() where T : class
    {
        var type = typeof(T);
        if (moduleFastMap.TryGetValue(type, out var module))
            return module as T;
        var result = allModules.FirstOrDefault(m => m.GetType().IsAssignableTo(type));
        if (result != null) moduleFastMap[type] = result;
        return result as T;
    }

    Virial.Game.Game? INebula.CurrentGame => NebulaGameManager.Instance;

    //ショートカット
    Virial.Configuration.Configurations Configurations => ConfigurationsAPI.API;
    Virial.Media.GUI GUILibrary => GUI.API;
    Virial.Media.Translator Language => Nebula.Modules.Language.API;
}

internal static class UnboxExtension
{
    internal static PlayerModInfo Unbox(this Virial.Game.Player player) => (PlayerModInfo)player;
    internal static CustomEndCondition Unbox(this Virial.Game.GameEnd end) => (CustomEndCondition)end;
    internal static PlayerTaskState Unbox(this Virial.Game.PlayerTasks taskInfo) => (PlayerTaskState)taskInfo;
}

public static class ComponentHolderHelper
{
    static public GameObject Bind(this ComponentHolder holder, GameObject gameObject)
    {
        holder.BindComponent(new GameObjectBinding(gameObject));
        return gameObject;
    }
}