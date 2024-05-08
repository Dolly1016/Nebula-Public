using System.Runtime.CompilerServices;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;

[assembly: InternalsVisibleTo("Nebula")]

namespace Virial;

internal interface INebula
{
    void RegisterPreset(string id, string name,string? detail, string? relatedHolder, Action onLoad);

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
    static internal NebulaPreprocessor? preprocessor = null;

    static public void RegisterPreset(string id, string displayName, string? detail, string? relatedHolder, Action onLoad) => instance.RegisterPreset(id,displayName,detail,relatedHolder,onLoad);
    public static string APIVersion => instance.APIVersion;

    static public Virial.Components.AbilityButton CreateAbilityButton() => instance.CreateAbilityButton();
    static public Virial.Components.GameTimer CreateTimer(float max, float min = 0f) => instance.CreateTimer(max, min);

    static public IResourceAllocator NebulaAsset => instance.NebulaAsset;
    static public IResourceAllocator InnerslothAsset => instance.InnerslothAsset;
    static public IResourceAllocator? GetAddon(string addonId) => instance.GetAddonResource(addonId);

    static public Media.GUI GUI => instance.GUILibrary;

    static public DefinedRole? GetRole(string roleId) => instance.GetRole(roleId);
    static public DefinedModifier? GetModifier(string modifierId) => instance.GetModifier(modifierId);

    //static public bool RegisterGameModule<T>(GameModuleFactory<T> factory, GameModuleInstantiationRule instantiationRule) => instance.RegisterGameModule(factory, instantiationRule);

    /// <summary>
    /// 現在のゲームを取得します。
    /// </summary>
    static public Game.Game? CurrentGame => instance.CurrentGame;

    /// <summary>
    /// プリプロセッサを取得します。
    /// プリプロセス終了後はnullが返ります。
    /// </summary>
    static public NebulaPreprocessor? Preprocessor => preprocessor;
}