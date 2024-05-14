using Nebula.Compat;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Text;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    private static List<object> allModules = new();
    private static Dictionary<Type, object> moduleFastMap = new();

    public NebulaImpl()
    {
        Instance = this;

        allModules.AddRange([Language.API, GUI.API, ConfigurationsAPI.API]);
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
}

internal static class UnboxExtension
{
    internal static PlayerModInfo Unbox(this Virial.Game.Player player) => (PlayerModInfo)player;
    internal static ModifierInstance Unbox(this RuntimeModifier modifier) => (ModifierInstance)modifier;
    internal static AbstractRole Unbox(this DefinedRole role) => (AbstractRole)role;
    internal static AbstractModifier Unbox(this DefinedModifier role) => (AbstractModifier)role;
    internal static ModAbilityButton Unbox(this Virial.Components.AbilityButton button) => (ModAbilityButton)button;
    internal static CustomEndCondition Unbox(this Virial.Game.GameEnd end) => (CustomEndCondition)end;
    internal static AssignableInstance Unbox(this Virial.Assignable.RuntimeAssignable assignable) => (AssignableInstance)assignable;
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