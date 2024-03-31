using Newtonsoft.Json.Bson;
using Virial.Game;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Modules.ScriptComponents;


public static class ObjectTrackers
{
    static private Func<IEnumerable<PlayerControl>> PlayerSupplier = () => PlayerControl.AllPlayerControls.GetFastEnumerator();
    static private Func<PlayerControl, Vector2> DefaultPlayerPosConverter = (p) => p.GetTruePosition();
    static private Func<PlayerControl, SpriteRenderer> DefaultPlayerRendererConverter = (p) => p.cosmetics.currentBodySprite.BodySprite;

    static private Func<IEnumerable<DeadBody>> DeadBodySupplier = () => Helpers.AllDeadBodies();
    static private Func<DeadBody, Vector2> DefaultDeadBodyPosConverter = (d) => d.TruePosition;
    static private Func<DeadBody, SpriteRenderer> DefaultDeadBodyRendererConverter = (d) => d.bodyRenderers[0];

    static public Predicate<PlayerControl> StandardPredicate = p => !p.AmOwner && !p.Data.IsDead;
    static public Predicate<PlayerControl> ImpostorKillPredicate = p => !p.AmOwner && !p.Data.IsDead && !p.Data.Role.IsImpostor;
    public static ObjectTracker<PlayerControl> ForPlayer(float? distance, PlayerControl tracker, Predicate<PlayerControl>? candidatePredicate, bool canTrackInVent = false) => ForPlayer(distance,tracker,candidatePredicate,null, canTrackInVent);
    public static ObjectTracker<PlayerControl> ForPlayer(float? distance, PlayerControl tracker, Predicate<PlayerControl>? candidatePredicate, Predicate<PlayerControl>? candidateHeavyPredicate, bool canTrackInVent = false)
    {
        distance ??= AmongUsUtil.VanillaKillDistance;
        return new ObjectTracker<PlayerControl>(distance.Value, tracker, PlayerSupplier,
            (p) => (canTrackInVent || !p.inVent) && (candidatePredicate?.Invoke(p) ?? true) && !(p.GetModInfo()?.IsInvisible ?? false) && !p.Data.IsDead, candidateHeavyPredicate,
            DefaultPlayerPosConverter, DefaultPlayerRendererConverter);
    }

    public static ObjectTracker<DeadBody> ForDeadBody(float? distance, PlayerControl tracker, Predicate<DeadBody>? candidatePredicate, Predicate<DeadBody>? candidateHeavyPredicate = null)
    {
        distance ??= AmongUsUtil.VanillaKillDistance;
        return new ObjectTracker<DeadBody>(distance.Value, tracker, DeadBodySupplier, candidatePredicate, candidateHeavyPredicate, DefaultDeadBodyPosConverter, DefaultDeadBodyRendererConverter);
    }
}

public class ObjectTracker<T> : INebulaScriptComponent, IGameEntity where T : MonoBehaviour 
{
    public T? CurrentTarget { get; private set; }
    private PlayerControl tracker;
    private Func<IEnumerable<T>> enumerableSupplier;
    //毎ティック、全対象に対してチェックします。
    private Predicate<T>? candidatePredicate;
    //毎ティックではありますが、距離の関係で候補に入りそうな対象に対してのみチェックします。
    private Predicate<T>? candidateHeavyPredicate;
    private Func<T, Vector2> positionConverter;
    private Func<T, SpriteRenderer> rendererConverter;
    public Color? Color = UnityEngine.Color.yellow;
    private bool UpdateTarget { get; set; } = true;
    private float MaxDistance { get; set; } = 1f;
    public bool IgnoreColliders { get; set; } = false;

    public ObjectTracker(float distance, PlayerControl tracker, Func<IEnumerable<T>> enumerableSupplier, Predicate<T>? candidatePredicate, Predicate<T>? candidateHeavyPredicate, Func<T, Vector2> positionConverter, Func<T, SpriteRenderer> rendererConverter)
    {
        CurrentTarget = null;
        this.tracker = tracker;
        this.candidatePredicate = candidatePredicate;
        this.candidateHeavyPredicate = candidateHeavyPredicate;
        this.positionConverter = positionConverter;
        MaxDistance = distance;
        this.rendererConverter = rendererConverter;
        this.enumerableSupplier = enumerableSupplier;
    }

    private void ShowTarget()
    {
        if (!CurrentTarget) return;

        if (Color.HasValue)
        {
            var renderer = rendererConverter.Invoke(CurrentTarget!);
            renderer.material.SetFloat("_Outline", 1f);
            renderer.material.SetColor("_OutlineColor", Color.Value);
        }
    }

    void IGameEntity.HudUpdate()
    {
        if (!UpdateTarget)
        {
            ShowTarget();
            return;
        }

        if (!tracker)
        {
            CurrentTarget = null;
            return;
        }

        if (!CurrentTarget) CurrentTarget = null;

        Vector2 myPos = tracker.GetTruePosition();

        float distance = float.MaxValue;
        T? candidate = null;

        foreach (var t in enumerableSupplier.Invoke())
        {
            Vector2 pos = positionConverter(t);
            Vector2 dVec = pos - myPos;
            float magnitude = dVec.magnitude;
            if (MaxDistance < magnitude) continue;
            if (candidate != null && distance < magnitude) continue;
            if (!(candidatePredicate?.Invoke(t) ?? true)) continue;
            if (!IgnoreColliders && PhysicsHelpers.AnyNonTriggersBetween(myPos, dVec.normalized, magnitude, Constants.ShipAndObjectsMask)) continue;
            if (!(candidateHeavyPredicate?.Invoke(t) ?? true)) continue;

            candidate = t;
            distance = magnitude;
        }

        CurrentTarget = candidate;
        ShowTarget();
    }
}
