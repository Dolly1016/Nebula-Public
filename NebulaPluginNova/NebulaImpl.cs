
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Nebula.Scripts;
using System.Reflection;
using Virial;
using Virial.Achievements;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using Virial.Utilities;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    private static List<object> allModules = new();
    private static Dictionary<Type, object> moduleFastMap = new();
    private static NebulaModuleFactory factory = new();
    public NebulaImpl()
    {
        Instance = this;

        allModules.AddRange([Nebula.Modules.Language.API, GUI.API, ConfigurationsAPI.API, NebulaHasher.API, Roles.Roles.API]);
    }

    public Version APIVersion => typeof(NebulaAPI).Assembly.GetName().Version!;

    public Virial.Media.IResourceAllocator NebulaAsset => NebulaResourceManager.NebulaNamespace;

    public Virial.Media.IResourceAllocator InnerslothAsset => NebulaResourceManager.InnerslothNamespace;

    public Virial.Media.IResourceAllocator? GetAddonResource(string addonId) => NebulaAddon.GetAddon(addonId);

    public Virial.Media.IResourceAllocator GetCallingAddonResource(Assembly assembly) => AddonScriptManager.ScriptAssemblies.FirstOrDefault(a => a.Assembly == assembly)?.Addon!;


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

    E INebula.RunEvent<E>(E ev) => GameOperatorManager.Instance?.Run(ev)!;

    GameStatsEntry INebula.CreateStatsEntry(string id, GameStatsCategory category, DefinedAssignable? assignable, TextComponent? displayTitle, int innerPriority) => NebulaAchievementManager.RegisterStats(id, category, assignable, displayTitle, innerPriority);

    void INebula.IncrementStatsEntry(string id, int num) {
        if (num == 1)
            new StaticAchievementToken(id);
        else if(num > 0)
            new AchievementToken<int>(id, num, (num, _) => num); 
    }

    void INebula.RegisterTip(IDocumentTip tip) => DocumentTipManager.Register(tip);

    IDisposable INebula.CreateRPCSection(string? label) => RPCRouter.CreateSection(label);

    //ショートカット
    Virial.Configuration.Configurations INebula.Configurations => ConfigurationsAPI.API;
    Virial.Media.GUI INebula.GUILibrary => GUI.API;
    Virial.Media.Translator INebula.Language => Nebula.Modules.Language.API;
    Virial.Utilities.IHasher INebula.Hasher => NebulaHasher.API;
    Virial.Assignable.Assignables INebula.Assignables => Nebula.Roles.Roles.API;

    IModuleFactory INebula.Modules => factory;

    bool INebula.IsAndroid =>  NebulaPlugin.IsAndroid;

    ITitlesRegister INebula.Titles => TitleRegisterImpl.Instance;
}

internal static class UnboxExtension
{
    internal static PlayerModInfo Unbox(this Virial.Game.Player player) => (PlayerModInfo)player;
    internal static PlayerTaskState Unbox(this Virial.Game.PlayerTasks taskInfo) => (PlayerTaskState)taskInfo;
}

public class NebulaHasher : Virial.Utilities.IHasher
{
    internal static NebulaHasher API = new();
    int IHasher.GetIntegerHash(string text) => text.ComputeConstantHash();

    long IHasher.GetLongHash(string text) => text.ComputeConstantLongHash();
}

internal class NebulaModuleFactory : IModuleFactory
{
    Virial.Components.ModAbilityButton IModuleFactory.AbilityButton(ILifespan lifespan) => new ModAbilityButtonImpl().Register(lifespan);
    Virial.Components.ModAbilityButton IModuleFactory.AbilityButton(ILifespan lifespan, bool isLeftSideButton, bool isArrangedAsKillButton, int priority, bool alwaysShow) => new ModAbilityButtonImpl(isLeftSideButton, isArrangedAsKillButton, priority, alwaysShow).Register(lifespan);
    Virial.Components.GameTimer IModuleFactory.Timer(ILifespan lifespan, float max) => new TimerImpl(max).Register(lifespan);
    Virial.Components.IVisualTimer IModuleFactory.CombinedTimer(IVisualTimer primaryTimer, IVisualTimer secondaryTimer, bool allowPrimaryTimerToStart, bool allowSecondaryTimerToStart) => new CombinedTimer(primaryTimer, secondaryTimer, allowPrimaryTimerToStart, allowSecondaryTimerToStart);

    Virial.Components.IVisualTimer IModuleFactory.VanillaKillTimer() => new VanillaKillTimer();
    Virial.Components.IVisualTimer IModuleFactory.CurrentKillTimer() => new CurrentKillTimer();

    private Virial.Components.ObjectTracker<GamePlayer> GetPlayerTracker(ILifespan lifespan, GamePlayer player, Predicate<GamePlayer> predicate, Func<GamePlayer, bool>? filter, Func<GamePlayer, bool>? filterHeavier, bool canTrackInVentPlayer)
    {
        Predicate<GamePlayer>? predicateHeavier = filterHeavier == null ? null : filterHeavier.Invoke;
        if (filter == null)
            return ObjectTrackers.ForPlayer(lifespan, null, player, predicate, predicateHeavier, null, canTrackInVentPlayer);
        else
        {
            return ObjectTrackers.ForPlayer(lifespan, null, player, (p) => predicate.Invoke(p) && filter(p), predicateHeavier, null, canTrackInVentPlayer);
        }
    }
    Virial.Components.ObjectTracker<GamePlayer> IModuleFactory.KillTracker(ILifespan lifespan, GamePlayer player, Func<GamePlayer, bool>? filter, Func<GamePlayer, bool>? filterHeavier, bool canTrackInVentPlayer)
        => GetPlayerTracker(lifespan, player, ObjectTrackers.KillablePredicate(player), filter, filterHeavier, canTrackInVentPlayer);

    Virial.Components.ObjectTracker<GamePlayer> IModuleFactory.PlayerTracker(ILifespan lifespan, GamePlayer player, Func<GamePlayer, bool>? filter, Func<GamePlayer, bool>? filterHeavier, bool canTrackInVentPlayer)
        => GetPlayerTracker(lifespan, player, ObjectTrackers.StandardPredicate, filter, filterHeavier, canTrackInVentPlayer);

    private Virial.Components.ObjectTracker<IPlayerlike> GetPlayerlikeTracker(ILifespan lifespan, GamePlayer player, Predicate<IPlayerlike> predicate, Func<IPlayerlike, bool>? filter, Func<IPlayerlike, bool>? filterHeavier, bool canTrackInVentPlayer)
    {
        Predicate<IPlayerlike>? predicateHeavier = filterHeavier == null ? null : filterHeavier.Invoke;
        if (filter == null)
            return ObjectTrackers.ForPlayerlike(lifespan, null, player, predicate, predicateHeavier, null, canTrackInVentPlayer);
        else
        {
            return ObjectTrackers.ForPlayerlike(lifespan, null, player, (p) => predicate.Invoke(p) && filter(p), predicateHeavier, null, canTrackInVentPlayer);
        }
    }
    ObjectTracker<IPlayerlike> IModuleFactory.PlayerlikeKillTracker(ILifespan lifespan, GamePlayer player, Func<IPlayerlike, bool>? filter, Func<IPlayerlike, bool>? filterHeavier, bool canTrackInVentPlayer)
        => GetPlayerlikeTracker(lifespan, player, ObjectTrackers.PlayerlikeKillablePredicate(player), filter, filterHeavier, canTrackInVentPlayer);

    ObjectTracker<IPlayerlike> IModuleFactory.PlayerlikeTracker(ILifespan lifespan, GamePlayer player, Func<IPlayerlike, bool>? filter, Func<IPlayerlike, bool>? filterHeavier, bool canTrackInVentPlayer)
        => GetPlayerlikeTracker(lifespan, player, ObjectTrackers.PlayerlikeStandardPredicate, filter, filterHeavier, canTrackInVentPlayer);

    bool IModuleFactory.CheckInteraction(GamePlayer player, IPlayerlike target, PlayerInteractParameter parameters)
    {
        return !(GameOperatorManager.Instance?.Run<PlayerInteractPlayerLocalEvent>(new PlayerInteractPlayerLocalEvent(player, target, parameters)).IsCanceled ?? false);
    }
}