using Il2CppInterop.Runtime.Injection;
using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Runtime;

namespace Nebula.Behavior;

public class CustomConsoleProperty
{
    public float UsableDistance { get; set; } = 0.8f;
    public Func<VVector2, bool>? CanUse { get; set; } = null;
    public Action<CustomConsole>? Use { get; set; } = null;
    public UnityEngine.Color OutlineColor { get; set; } = Color.white;

    static public Action<CustomConsole> MinigameAction<T>(GameObject prefab, Action<T, CustomConsole>? onInstantiated = null)where T:Minigame
    {
        return console =>
        {
            GameObject instantiated = prefab.AsBoolFast() ? GameObject.Instantiate(prefab) : UnityHelper.CreateObject("Minigame", null, Vector3.zero);
            instantiated.layer = LayerExpansion.GetUILayer();
            T minigame = instantiated.AddComponent<T>();
            minigame.transform.SetParent(Camera.main.transform, false);
            minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
            onInstantiated?.Invoke(minigame, console);
            minigame.Begin(null);
        };
    }
}

public static class MinigameHelper
{
    static public void BeginInternal(this Minigame minigame, PlayerTask task)
    {
        Minigame.Instance = minigame;
        minigame.MyTask = task;
        if (task && task.TryGetComponent<NormalPlayerTask>(out var normalTask)) minigame.MyNormTask = normalTask;
        minigame.timeOpened = Time.realtimeSinceStartup;
        if (AmongUsLLImpl.LocalPlayer.AsBoolFast())
        {
            if (MapBehaviour.Instance.AsBoolFast()) MapBehaviour.Instance.Close();
            AmongUsLLImpl.LocalPlayer.MyPhysics.SetNormalizedVelocity(Vector2.zero);
        }
        minigame.StartCoroutine(minigame.CoAnimateOpen());
        minigame.CloseSound = AmongUsLLImpl.ShipStatusInstance.ShortTasks.First().MinigamePrefab.CloseSound;
        minigame.TransType = TransitionType.SlideBottom;
    }

    static public void CloseInternal(this Minigame minigame)
    {
        if (minigame.amClosing != Minigame.CloseState.Closing)
        {
            if (minigame.CloseSound && Constants.ShouldPlaySfx()) AmongUsLLImpl.SoundManagerInstance.PlaySound(minigame.CloseSound, false, 1f, null);
            if (AmongUsLLImpl.LocalPlayer.AsBoolFast()) PlayerControl.HideCursorTemporarily();

            minigame.amClosing = Minigame.CloseState.Closing;
            minigame.StartCoroutine(minigame.CoDestroySelf());
        }
        else
        {
            GameObject.Destroy(minigame.gameObject);
        }
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class CustomConsoleManager : AbstractModule<Virial.Game.Game>
{
    static public void Preprocess(NebulaPreprocessor preprocess)
    {
        DIManager.Instance.RegisterModule(() => new CustomConsoleManager());
    }

    private CustomConsoleManager()
    {
        ModSingleton<CustomConsoleManager>.Instance = this;
    }
    public List<CustomConsole> AllCustomConsoles = [];
}

public class CustomConsole : MonoBehaviour
{
    static CustomConsole()
    {
        ClassInjector.RegisterTypeInIl2Cpp<CustomConsole>(new RegisterTypeOptions()
        {
            Interfaces = new[] { typeof(IUsable) }
        });
    }

    public CustomConsoleProperty? Property { get; set; } = null;
    public SpriteRenderer Renderer { get; set; } = null!;

    public float UsableDistance => Property?.UsableDistance ?? 0.8f;
    public float PercentCool { get => 0f; }

    public ImageNames UseIcon { get => ImageNames.UseButton; }

    public void SetOutline(bool on, bool mainTarget)
    {
        if (this.Renderer.AsBoolFast())
        {
            var mat = this.Renderer.material;
            mat.SetFloat("_Outline", (float)(on ? 1 : 0));
            mat.SetColor("_OutlineColor", Property?.OutlineColor ?? Color.white);
            mat.SetColor("_AddColor", mainTarget ? (Property?.OutlineColor ?? Color.white) : Color.clear);
            if(on) HighlightManager.AddHighlightedRenderer(this.Renderer);
        }
    }


    public float CanUse(NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
    {
        float num = float.MaxValue;
        PlayerControl @object = pc.Object;
        VVector2 truePosition = @object.GetTruePosition();
        VVector2 position = base.transform.position;
        couldUse = Property?.CanUse?.Invoke(position) ?? true && !GamePlayer.LocalPlayer!.AllAbilities.All(a => a.BlockUsingUtility);
        canUse = couldUse;
        if (canUse)
        {
            num = Vector2.Distance(truePosition, position);
            canUse &= (num <= this.UsableDistance);
            canUse &= !NebulaPhysicsHelpers.AnyShadowBetween(truePosition, position, out _);
        }
        return num;
    }


    public void Use()
    {
        this.CanUse(AmongUsLLImpl.LocalPlayer.Data, out var flag, out var flag2);
        if (!flag)
        {
            return;
        }
        Property?.Use?.Invoke(this);
    }

    void Awake()
    {
        Property = null;
        Renderer = null!;

        var collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.4f;
        collider.isTrigger = true;

        ModSingleton<CustomConsoleManager>.Instance.AllCustomConsoles.Add(this);
    }
}
