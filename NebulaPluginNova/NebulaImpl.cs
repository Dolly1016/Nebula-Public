using Nebula.Compat;
using Nebula.Events;
using Nebula.Modules.MetaWidget;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Media;
using Virial.Text;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    public NebulaImpl()
    {
        Instance = this;
    }

    public List<AbstractRoleDef> AllRoles = new();

    public string APIVersion => typeof(NebulaAPI).Assembly.GetName().Version?.ToString() ?? "Undefined";

    public Virial.Assets.INameSpace NebulaAsset => NameSpaceManager.DefaultNameSpace;

    public Virial.Assets.INameSpace InnerslothAsset => NameSpaceManager.InnerslothNameSpace;

    public Virial.Media.GUI GUILibrary => NebulaGUIWidgetEngine.Instance;

    public CommunicableTextTag RegisterCommunicableText(string translationKey)
    {
        return new TranslatableTag(translationKey);
    }

    public void RegisterRole(AbstractRoleDef roleDef)
    {
        AddonScriptManager.GetScriptingByAssembly(roleDef.GetType().Assembly)?.Addon.MarkAsNeedingHandshake();
        AllRoles.Add(roleDef);
    }

    public void RegisterPreset(string id, string name, string? detail, string? relatedHolder, Action onLoad)
    {
        new ScriptPreset(id, name, detail, relatedHolder, onLoad);
    }

    public RoleTeam CreateTeam(string translationKey, Virial.Color color, TeamRevealType revealType)
    {
        return new Team(translationKey,color.ToUnityColor(), revealType);
    }

    public void RegisterEventHandler(ILifespan lifespan,object handler)
    {
        EventManager.RegisterEvent(lifespan, handler);
    }

    public Virial.Components.AbilityButton CreateAbilityButton()
    {
        return new ModAbilityButton();
    }

    public Virial.Components.GameTimer CreateTimer(float max, float min)
    {
        return new Timer(min, max);
    }

    public Virial.Assets.INameSpace GetAddon(string addonId)
    {
        return NameSpaceManager.ResolveOrGetDefault(addonId);
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

public static class UnboxExtension
{
    public static PlayerModInfo Unbox(this Virial.Game.Player player) => (PlayerModInfo)player;
    public static RoleInstance Unbox(this RuntimeRole role) => (RoleInstance)role;
    public static ModifierInstance Unbox(this RuntimeModifier modifier) => (ModifierInstance)modifier;
    public static AbstractRole Unbox(this DefinedRole role) => (AbstractRole)role;
    public static AbstractModifier Unbox(this DefinedModifier role) => (AbstractModifier)role;
    public static ModAbilityButton Unbox(this Virial.Components.AbilityButton button) => (ModAbilityButton)button;
    public static CustomEndCondition Unbox(this Virial.Game.GameEnd end) => (CustomEndCondition)end;
}

public static class ComponentHolderHelper
{
    static public GameObject Bind(this ComponentHolder holder, GameObject gameObject)
    {
        holder.BindComponent(new GameObjectBinding(gameObject));
        return gameObject;
    }
}