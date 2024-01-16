using System.Runtime.CompilerServices;
using UnityEngine;
using Virial.Assets;
using Virial.Assignable;
using Virial.Components;
using Virial.Game;
using Virial.Media;
using Virial.Text;

[assembly: InternalsVisibleTo("Nebula")]

namespace Virial;

internal interface INebula
{
    void RegisterRole(AbstractRoleDef roleDef);
    void RegisterPreset(string id, string name,string? detail, string? relatedHolder, Action onLoad);
    RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType);
    void RegisterEventHandler(ILifespan lifespan, object handler);
    CommunicableTextTag RegisterCommunicableText(string translationKey);
    Virial.Components.AbilityButton CreateAbilityButton();
    Virial.Components.GameTimer CreateTimer(float max, float min);
    string APIVersion { get; }
    INameSpace NebulaAsset { get; }
    INameSpace InnerslothAsset { get; }
    INameSpace GetAddon(string addonId);
    IEnumerable<Player> GetPlayers();
    Player? LocalPlayer { get; }
    Media.GUI GUILibrary { get; }

    DefinedRole? GetRole(string roleId);
    DefinedModifier? GetModifier(string modifierId);

}

public static class NebulaAPI
{
    static internal INebula instance = null!;

    static public void RegisterRole(AbstractRoleDef roleDef) => instance.RegisterRole(roleDef);
    static public void RegisterPreset(string id, string displayName, string? detail, string? relatedHolder, Action onLoad) => instance.RegisterPreset(id,displayName,detail,relatedHolder,onLoad);
    static public RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType) => instance.CreateTeam(translationKey, color, revealType);

    public static CommunicableTextTag RegisterCommunicableText(string translationKey) => instance.RegisterCommunicableText(translationKey);

    public static string APIVersion => instance.APIVersion;

    static public Virial.Components.AbilityButton CreateAbilityButton() => instance.CreateAbilityButton();
    static public Virial.Components.GameTimer CreateTimer(float max, float min = 0f) => instance.CreateTimer(max, min);

    static public INameSpace NebulaAsset => instance.NebulaAsset;
    static public INameSpace InnerslothAsset => instance.InnerslothAsset;
    static public INameSpace GetAddon(string addonId) => instance.GetAddon(addonId);
    static public void RegisterEventHandler(ILifespan lifespan, object handler) => instance.RegisterEventHandler(lifespan, handler); 
    static public IEnumerable<Player> GetPlayers() => instance.GetPlayers();
    static public Player? LocalPlayer => instance.LocalPlayer;
    static public Media.GUI GUI => instance.GUILibrary;

    static public DefinedRole? GetRole(string roleId) => instance.GetRole(roleId);
    static public DefinedModifier? GetModifier(string modifierId) => instance.GetModifier(modifierId);
}