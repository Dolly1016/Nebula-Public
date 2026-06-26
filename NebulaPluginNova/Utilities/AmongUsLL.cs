using AmongUs.GameOptions;
using Nebula.Bridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Internal;

namespace Nebula.Utilities;

internal abstract class LLCacheBase
{
    static List<LLCacheBase> caches = [];
    protected LLCacheBase(bool clearOnSceneChanged)
    {
        if (clearOnSceneChanged) caches.Add(this);
    }

    internal protected abstract void Clear();
    static public void OnSceneChanged()
    {
        foreach (var c in caches) c.Clear();
    }
}

internal class LLCache<T> : LLCacheBase
{
    T value;
    bool initialized;
    Func<(T value, bool initialized)> provider;

    public bool HasBeenInitialized => initialized;
    public LLCache(Func<(T value, bool initialized)> provider, bool clearOnSceneChanged = true) : base(clearOnSceneChanged)
    {
        this.provider = provider;
        initialized = false;
    }
    internal protected override void Clear()
    {
        initialized = false;
    }

    public bool Get(out T value)
    {
        if (!initialized)
        {
            var returned = provider.Invoke();
            initialized = returned.initialized;
            this.value = returned.value;
            value = this.value;
            return initialized;
        }
        value = this.value;
        return true;
    }

    public T Get()
    {
        if (!initialized)
        {
            var returned = provider.Invoke();
            initialized = returned.initialized;
            this.value = returned.value;
            return this.value;
        }
        return this.value;
    }
}
internal class AmongUsLLImpl : AmongUsLL
{
    static internal AmongUsLLImpl Instance { get; } = new AmongUsLLImpl();

    private LLCache<GameOptionsManager> gameOptionsManager;
    static public GameOptionsManager GameOptionManagerInstance => Instance.gameOptionsManager.Get();

    private LLCache<IGameOptions> currentGameOptions;
    static public IGameOptions CurrentGameOptionsInstance => Instance.currentGameOptions.Get();

    private LLCache<GameManager> gameManager;
    static public GameManager GameManagerInstance => Instance.gameManager.Get();

    private LLCache<HudManager> hudManager;
    static public HudManager HudManagerInstance => Instance.hudManager.Get();
    private LLCache<BHudManager> hudManagerBridge;
    static public BHudManager HudManagerBridge => Instance.hudManagerBridge.Get();
    static public bool TryGetHudManager(out HudManager val) => Instance.hudManager.Get(out val);
    static public bool TryGetHudManager(out HudManager val, out BHudManager bridge)
    {
        var returnedValue = Instance.hudManager.Get(out val);
        bridge = HudManagerBridge;
        return returnedValue;
    }

    private LLCache<ShipStatus> shipStatus;
    static public ShipStatus ShipStatusInstance => Instance.shipStatus.Get();
    static public bool TryGetShipStatus(out ShipStatus val) => Instance.shipStatus.Get(out val);

    
    private LLCache<AmongUsClient> amongUsClient;
    static public AmongUsClient AmongUsClientInstance => Instance.amongUsClient.Get();
    static public bool TryGetAmongUsClientInstance(out AmongUsClient val) => Instance.amongUsClient.Get(out val);


    private LLCache<SoundManager> soundManager;
    static public SoundManager SoundManagerInstance => Instance.soundManager.Get();
    static public bool TryGetSoundManager(out SoundManager val) => Instance.soundManager.Get(out val);


    private LLCache<LobbyBehaviour> lobbyBehaviour;
    static public LobbyBehaviour LobbyInstance => Instance.lobbyBehaviour.Get();
    static public bool TryGetLobby(out LobbyBehaviour val) => Instance.lobbyBehaviour.Get(out val);

    private LLCache<float> killCooldown;
    private LLCache<float> killDistance;
    private LLCache<byte> mapId;
    private LLCache<PlayerControl> localPlayer;

    private LLCache<int> screenWidth;
    private LLCache<int> screenHeight;

    static public PlayerControl LocalPlayer => Instance.localPlayer.Get();
    PlayerControl AmongUsLL.LocalPlayer => localPlayer.Get();
    static public bool TryGetLocalPlayer(out PlayerControl val) => Instance.localPlayer.Get(out val);
    private AmongUsLLImpl()
    {
        amongUsClient = new(() => { var instance = AmongUsClient.Instance; return (instance, instance != null); }, false);
        soundManager = new(() => { var instance = SoundManager.Instance; return (instance, instance != null); }, false);
        gameOptionsManager = new(() => { var instance = GameOptionsManager.Instance; return (instance, instance != null); });
        gameManager = new(() => { var instance = GameManager.Instance; return (instance, instance != null); });
        shipStatus = new(() => { var instance = ShipStatus.Instance; return (instance, instance); });
        hudManager = new(() => { var instanceExists = HudManager.InstanceExists; return (instanceExists ? HudManager.Instance : null!, instanceExists); });
        hudManagerBridge = new(() => hudManager.Get(out var val) ? (new BHudManager(val), true) : (null!, false));
        lobbyBehaviour = new(() => { var instance = LobbyBehaviour.Instance; return (instance, instance); });
        currentGameOptions = new(() =>
        {
            var exists = gameOptionsManager.Get(out var instance);
            return (exists ? instance.currentGameOptions : null! , exists && shipStatus.HasBeenInitialized);
        });
        killCooldown = new(() => { 
            var val = currentGameOptions.Get(out var instance) ? instance.GetFloat(FloatOptionNames.KillCooldown) : 30f; 
            return (val, shipStatus.HasBeenInitialized); 
        });
        killDistance = new(() => {
            var val = gameManager.Get(out var instance) ? instance.LogicOptions.GetKillDistance() : 1f;
            return (val, shipStatus.HasBeenInitialized);
        });
        mapId = new(() => {
            var val = currentGameOptions.Get().MapId;
            return (val, shipStatus.HasBeenInitialized);
        });
        localPlayer = new(() => { var instance = PlayerControl.LocalPlayer; return (instance, instance); });

        screenWidth = new(() => (Screen.width, true), false);
        screenHeight = new(() => (Screen.height, true), false);
    }

    public float VanillaKillCooldown => killCooldown.Get();
    public float VanillaKillDistance => killDistance.Get();
    public byte MapId => mapId.Get();
    public int ScreenWidth => screenWidth.Get();
    public int ScreenHeight => screenHeight.Get();
    internal void OnSceneChanged() => LLCacheBase.OnSceneChanged();

    public void OnScreenSizeChanged()
    {
        screenWidth.Clear();
        screenHeight.Clear();
        LogUtils.WriteToConsole("OnScreenSizeChanged");
    }
}
