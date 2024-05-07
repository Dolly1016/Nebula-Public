using Newtonsoft.Json.Bson;
using Virial.Components;
using Virial.Game;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Modules.ScriptComponents;


public static class ObjectTrackers
{
    static public Predicate<GamePlayer> StandardPredicate = p => !p.AmOwner && !p.IsDead;
    static public Predicate<GamePlayer> ImpostorKillPredicate = p => !p.AmOwner && !p.IsDead && !p.IsImpostor;

    /*
    public static ObjectTrackerUnityImplOld<PlayerControl> ForPlayer(float? distance, PlayerControl tracker, Predicate<PlayerControl>? candidatePredicate, bool canTrackInVent = false) => ForPlayer(distance,tracker,candidatePredicate,null, canTrackInVent);
    public static ObjectTrackerUnityImplOld<PlayerControl> ForPlayer(float? distance, PlayerControl tracker, Predicate<PlayerControl>? candidatePredicate, Predicate<PlayerControl>? candidateHeavyPredicate, bool canTrackInVent = false)
    {
        distance ??= AmongUsUtil.VanillaKillDistance;
        return new ObjectTrackerUnityImplOld<PlayerControl>(distance.Value, tracker, PlayerSupplier,
            (p) => (canTrackInVent || !p.inVent) && (candidatePredicate?.Invoke(p) ?? true) && !(p.GetModInfo()?.IsInvisible ?? false) && !p.Data.IsDead, candidateHeavyPredicate,
            DefaultPlayerPosConverter, DefaultPlayerRendererConverter);
    }
    
    public static ObjectTrackerUnityImplOld<DeadBody> ForDeadBody(float? distance, PlayerControl tracker, Predicate<DeadBody>? candidatePredicate, Predicate<DeadBody>? candidateHeavyPredicate = null)
    {
        distance ??= AmongUsUtil.VanillaKillDistance;
        return new ObjectTrackerUnityImplOld<DeadBody>(distance.Value, tracker, DeadBodySupplier, candidatePredicate, candidateHeavyPredicate, DefaultDeadBodyPosConverter, DefaultDeadBodyRendererConverter);
    }
    */


    public static ObjectTracker<GamePlayer> ForPlayer(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, UnityEngine.Color? color = null, bool canTrackInVent = false, bool ignoreCollider = false) => ForPlayer(distance, tracker, predicate, null, color ?? UnityEngine.Color.yellow, canTrackInVent, ignoreCollider);
    public static ObjectTracker<GamePlayer> ForPlayer(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, Predicate<GamePlayer>? predicateHeavier, UnityEngine.Color? color, bool canTrackInVent = false, bool ignoreCollider = false)
    {
        if (!canTrackInVent)
        {
            var lastPredicate = predicate;
            predicate = p => !p.VanillaPlayer.inVent && lastPredicate(p);
        }
        IEnumerable<PlayerControl> FastPlayers()
        {
            foreach(var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) yield return p;
        }

        return new ObjectTrackerUnityImpl<GamePlayer, PlayerControl>(tracker.VanillaPlayer, distance ?? AmongUsUtil.VanillaKillDistance, FastPlayers(), predicate, predicateHeavier, p => p.GetModInfo(), p => p.GetTruePosition(), p => p.cosmetics.currentBodySprite.BodySprite, color, ignoreCollider);
    }

    public static ObjectTracker<GamePlayer> ForDeadBody(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, Predicate<GamePlayer>? predicateHeavier = null, UnityEngine.Color? color = null, bool ignoreCollider = false)
    {
        return new ObjectTrackerUnityImpl<GamePlayer, DeadBody>(tracker.VanillaPlayer, distance ?? AmongUsUtil.VanillaKillDistance, Helpers.AllDeadBodies().Where(d => d.bodyRenderers.Any(r => r.enabled)), predicate, predicateHeavier, d => NebulaGameManager.Instance.GetModPlayerInfo(d.ParentId), d => d.TruePosition, d => d.bodyRenderers[0], color, ignoreCollider);
    }
}



public class ObjectTrackerUnityImpl<V,T> : INebulaScriptComponent, ObjectTracker<V>, IGameEntity where T : MonoBehaviour where V : class
{
    V? ObjectTracker<V>.CurrentTarget => currentTarget?.Item2;
    bool ObjectTracker<V>.IsLocked { get => isLocked; set => isLocked = value; }

    private Tuple<T,V>? currentTarget = null;

    PlayerControl tracker;
    IEnumerable<T> allTargets;
    Predicate<V> predicate;
    Predicate<V>? predicateHeavier = null;
    Func<T, V> converter;
    Func<T, UnityEngine.Vector2> positionConverter;
    Func<T, SpriteRenderer> rendererConverter;
    UnityEngine.Color color = UnityEngine.Color.yellow;
    bool ignoreColliders;
    float maxDistance;
    private bool isLocked = false;

    public ObjectTrackerUnityImpl(PlayerControl tracker, float maxDistance, IEnumerable<T> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, Vector2> positionConverter, Func<T, SpriteRenderer> rendererConverter, Color? color = null, bool ignoreColliders = false)
    {
        this.tracker = tracker;
        this.allTargets = allTargets;
        this.predicate = predicate;
        this.predicateHeavier = predicateHeavier;
        this.converter = converter;
        this.positionConverter = positionConverter;
        this.rendererConverter = rendererConverter;
        this.maxDistance = maxDistance;
        this.ignoreColliders = ignoreColliders;
        if(color.HasValue) this.color = color.Value;
    }

    private void ShowTarget()
    {
        if (currentTarget == null) return;

        var renderer = rendererConverter.Invoke(currentTarget!.Item1);
        renderer.material.SetFloat("_Outline", 1f);
        renderer.material.SetColor("_OutlineColor", color);
    }

    void IGameEntity.HudUpdate()
    {
        if (isLocked)
        {
            ShowTarget();
            return;
        }

        if (!tracker)
        {
            currentTarget = null;
            return;
        }

        Vector2 myPos = tracker.GetTruePosition();

        float distance = float.MaxValue;
        Tuple<T,V>? candidate = null;

        foreach (var t in allTargets)
        {
            if (!t) continue;

            var v = converter(t);

            if (!predicate(v)) continue;

            Vector2 pos = positionConverter(t);
            Vector2 dVec = pos - myPos;
            float magnitude = dVec.magnitude;
            if (maxDistance < magnitude) continue;
            if (candidate != null && distance < magnitude) continue;

            if (!ignoreColliders && PhysicsHelpers.AnyNonTriggersBetween(myPos, dVec.normalized, magnitude, Constants.ShipAndObjectsMask)) continue;
            if (!(predicateHeavier?.Invoke(v) ?? false)) continue;

            candidate = new(t,v);
            distance = magnitude;
        }

        currentTarget = candidate;
        ShowTarget();
    }
}


public class ObjectTrackerUnityImplOld<T> : INebulaScriptComponent, ObjectTracker<T>, IGameEntity where T : MonoBehaviour 
{
    public T? CurrentTarget { get; private set; }
    bool ObjectTracker<T>.IsLocked { get => !UpdateTarget; set => UpdateTarget = !value; }

    private PlayerControl tracker;
    private Func<IEnumerable<T>> enumerableSupplier;
    //毎ティック、全対象に対してチェックします。
    private Predicate<T>? candidatePredicate;
    //毎ティック、距離の関係で候補に入りそうな対象に対してのみチェックします。
    private Predicate<T>? candidateHeavyPredicate;
    private Func<T, Vector2> positionConverter;
    private Func<T, SpriteRenderer> rendererConverter;
    public Color? Color = UnityEngine.Color.yellow;
    private bool UpdateTarget { get; set; } = true;
    private float MaxDistance { get; set; } = 1f;
    public bool IgnoreColliders { get; set; } = false;

    public ObjectTrackerUnityImplOld(float distance, PlayerControl tracker, Func<IEnumerable<T>> enumerableSupplier, Predicate<T>? candidatePredicate, Predicate<T>? candidateHeavyPredicate, Func<T, Vector2> positionConverter, Func<T, SpriteRenderer> rendererConverter)
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
