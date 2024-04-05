using System.Runtime.CompilerServices;
using UnityEngine;
using Virial.Assignable;
using Virial.Attributes;
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
    IResourceAllocator NebulaAsset { get; }
    IResourceAllocator InnerslothAsset { get; }
    IResourceAllocator? GetAddonResource(string addonId);
    IEnumerable<Player> GetPlayers();
    Player? LocalPlayer { get; }
    Media.GUI GUILibrary { get; }
    Game.Game? CurrentGame { get; }

    DefinedRole? GetRole(string roleId);
    DefinedModifier? GetModifier(string modifierId);

}

public static class NebulaAPI
{
    static internal INebula instance = null!;

    [RequiringHandshake]
    static public void RegisterRole(AbstractRoleDef roleDef) => instance.RegisterRole(roleDef);
    static public void RegisterPreset(string id, string displayName, string? detail, string? relatedHolder, Action onLoad) => instance.RegisterPreset(id,displayName,detail,relatedHolder,onLoad);
    static public RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType) => instance.CreateTeam(translationKey, color, revealType);

    public static CommunicableTextTag RegisterCommunicableText(string translationKey) => instance.RegisterCommunicableText(translationKey);

    public static string APIVersion => instance.APIVersion;

    static public Virial.Components.AbilityButton CreateAbilityButton() => instance.CreateAbilityButton();
    static public Virial.Components.GameTimer CreateTimer(float max, float min = 0f) => instance.CreateTimer(max, min);

    static public IResourceAllocator NebulaAsset => instance.NebulaAsset;
    static public IResourceAllocator InnerslothAsset => instance.InnerslothAsset;
    static public IResourceAllocator? GetAddon(string addonId) => instance.GetAddonResource(addonId);
    static public void RegisterEventHandler(ILifespan lifespan, object handler) => instance.RegisterEventHandler(lifespan, handler);

    /// <summary>
    /// ゲーム中の全プレイヤーを取得します。
    /// 代わりに<see cref="Game.Game.GetAllPlayers"/>を使用してください。
    /// </summary>
    /// <returns>ゲーム中の全プレイヤー</returns>
    [Obsolete]
    static public IEnumerable<Player> GetPlayers() => instance.GetPlayers();

    /// <summary>
    /// 自身の操作しているプレイヤーを取得します。
    /// 代わりに<see cref="Game.Game.LocalPlayer"/>を使用してください。
    /// </summary>
    [Obsolete]
    static public Player? LocalPlayer => instance.LocalPlayer;

    static public Media.GUI GUI => instance.GUILibrary;

    static public DefinedRole? GetRole(string roleId) => instance.GetRole(roleId);
    static public DefinedModifier? GetModifier(string modifierId) => instance.GetModifier(modifierId);

    /// <summary>
    /// 現在のゲームを取得します。
    /// </summary>
    static public Game.Game? CurrentGame => instance.CurrentGame;
}