using Nebula.Compat;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Virial;
using Virial.Assignable;
using Virial.Text;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    public NebulaImpl()
    {
        Instance = this;
    }

    public string APIVersion => typeof(NebulaAPI).Assembly.GetName().Version?.ToString() ?? "Undefined";

    public Virial.Media.IResourceAllocator NebulaAsset => NebulaResourceManager.NebulaNamespace;

    public Virial.Media.IResourceAllocator InnerslothAsset => NebulaResourceManager.InnerslothNamespace;

    public Virial.Media.GUI GUILibrary => NebulaGUIWidgetEngine.Instance;

    public CommunicableTextTag RegisterCommunicableText(string translationKey)
    {
        return new TranslatableTag(translationKey);
    }

    public void RegisterPreset(string id, string name, string? detail, string? relatedHolder, Action onLoad)
    {
        new ScriptPreset(id, name, detail, relatedHolder, onLoad);
    }

    public RoleTeam CreateTeam(string translationKey, Virial.Color color, TeamRevealType revealType)
    {
        return new Team(translationKey,color.ToUnityColor(), revealType);
    }

    public Virial.Components.AbilityButton CreateAbilityButton()
    {
        return new ModAbilityButton();
    }

    public Virial.Components.GameTimer CreateTimer(float max, float min)
    {
        return new Timer(min, max);
    }

    public Virial.Media.IResourceAllocator? GetAddonResource(string addonId)
    {
        return NebulaAddon.GetAddon(addonId);
    }

    public IEnumerable<Virial.Game.Player> GetPlayers()
    {
        if (NebulaGameManager.Instance == null) yield break;
        foreach (var p in NebulaGameManager.Instance.AllPlayerInfo()) yield return p;
    }

    public Virial.Game.Player? LocalPlayer => PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.GetModInfo() : null;

    Virial.Game.Game? INebula.CurrentGame => NebulaGameManager.Instance;

    public DefinedRole? GetRole(string roleId) => Roles.Roles.AllRoles.FirstOrDefault(r => r.LocalizedName == roleId);
    
    public DefinedModifier? GetModifier(string modifierId) => Roles.Roles.AllModifiers.FirstOrDefault(m => m.LocalizedName == modifierId);
    
}

internal static class UnboxExtension
{
    internal static PlayerModInfo Unbox(this Virial.Game.Player player) => (PlayerModInfo)player;
    internal static RoleInstance Unbox(this RuntimeRole role) => (RoleInstance)role;
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