using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Runtime;

namespace Nebula.Behaviour;

public class CustomConsoleProperty
{
    public float UsableDistance { get; set; } = 0.8f;
    public Func<Vector2, bool>? CanUse { get; set; } = null;
    public Action<CustomConsole>? Use { get; set; } = null;
    public UnityEngine.Color OutlineColor { get; set; } = Color.white;

    static public Action<CustomConsole> MinigameAction<T>(GameObject prefab, Action<T, CustomConsole>? onInstantiated = null)where T:Minigame
    {
        return console =>
        {
            T minigame = GameObject.Instantiate(prefab).AddComponent<T>();
            minigame.transform.SetParent(Camera.main.transform, false);
            minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
            onInstantiated?.Invoke(minigame, console);
            minigame.Begin(null);
        };
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
        if (this.Renderer)
        {
            this.Renderer.material.SetFloat("_Outline", (float)(on ? 1 : 0));
            this.Renderer.material.SetColor("_OutlineColor", Property?.OutlineColor ?? Color.white);
            this.Renderer.material.SetColor("_AddColor", mainTarget ? (Property?.OutlineColor ?? Color.white) : Color.clear);
        }
    }


    public float CanUse(NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
    {
        float num = float.MaxValue;
        PlayerControl @object = pc.Object;
        Vector2 truePosition = @object.GetTruePosition();
        Vector3 position = base.transform.position;
        couldUse = Property?.CanUse?.Invoke(position) ?? true;
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
        bool flag;
        bool flag2;
        this.CanUse(PlayerControl.LocalPlayer.Data, out flag, out flag2);
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
