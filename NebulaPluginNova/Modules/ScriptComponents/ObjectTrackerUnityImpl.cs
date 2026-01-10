
using Virial;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;

public static class HighlightHelpers
{
    public static void SetHighlight(Renderer renderer, UnityEngine.Color color)
    {
        renderer.material.SetFloat("_Outline", 1f);
        renderer.material.SetColor("_OutlineColor", color);
    }

    internal static void SetHighlight(GamePlayer player, UnityEngine.Color color) => SetHighlight(player.VanillaPlayer.cosmetics.currentBodySprite.BodySprite, color);
    internal static void SetHighlight(DeadBody? player, UnityEngine.Color color)
    {
        if (player != null) SetHighlight(player.bodyRenderers[0], color);
    }

    public static void SetHighlight(GamePlayer player, Virial.Color color) => SetHighlight(player, color.ToUnityColor());
}

public static class ObjectTrackers
{
    static public Predicate<GamePlayer> StandardPredicateIgnoreOwner = p => p.CanBeTarget && !p.IsDead && !p.WillDie && !p.IsInvisible && !p.IsDived && !p.IsBlown;
    static public Predicate<GamePlayer> StandardPredicate = p => !p.AmOwner && StandardPredicateIgnoreOwner.Invoke(p);
    static public Predicate<GamePlayer> KillablePredicate(GamePlayer myPlayer) => p => StandardPredicate(p) && myPlayer.CanKill(p);
    static public Predicate<GamePlayer> LocalKillablePredicate = p => StandardPredicate(p) && (GamePlayer.LocalPlayer?.CanKill(p) ?? true);

    static public Predicate<IPlayerlike> PlayerlikeStandardPredicateIgnoreOwner = p => p.CanBeTarget && !p.IsDead && (p is not GamePlayer gp || (!gp.WillDie && !gp.IsInvisible && !gp.IsDived && !gp.IsBlown));
    static public Predicate<IPlayerlike> PlayerlikeStandardPredicate = p => !p.RealPlayer.AmOwner && PlayerlikeStandardPredicateIgnoreOwner.Invoke(p);
    static public Predicate<IPlayerlike> PlayerlikeKillablePredicate(GamePlayer myPlayer) => p => PlayerlikeStandardPredicate(p) && myPlayer.CanKill(p.RealPlayer);
    static public Predicate<IPlayerlike> PlayerlikeLocalKillablePredicate = p => PlayerlikeStandardPredicate(p) && (GamePlayer.LocalPlayer?.CanKill(p.RealPlayer) ?? true);

    public static ObjectTracker<GamePlayer> ForPlayer(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, UnityEngine.Color? color = null, bool canTrackInVent = false, bool ignoreCollider = false) => ForPlayer(lifespan, distance, tracker, predicate, null, color ?? UnityEngine.Color.yellow, canTrackInVent, ignoreCollider);
    public static ObjectTracker<GamePlayer> ForPlayer(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, Predicate<GamePlayer>? predicateHeavier, UnityEngine.Color? color, bool canTrackInVent = false, bool ignoreCollider = false)
    {
        if (!canTrackInVent)
        {
            var lastPredicate = predicate;
            predicate = p => !p.VanillaPlayer.inVent && lastPredicate(p);
        }
        IEnumerable<PlayerControl> FastPlayers() => PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => p);

        return new ObjectTrackerUnityImpl<GamePlayer, PlayerControl>(tracker.VanillaPlayer, distance ?? AmongUsLLImpl.Instance.VanillaKillDistance, FastPlayers, predicate, predicateHeavier, p => p.GetModInfo(), p => [p.GetTruePosition()], p => p.cosmetics.currentBodySprite.BodySprite, color, ignoreCollider).Register(lifespan);
    }

    public static ObjectTracker<IPlayerlike> ForPlayerlike(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<IPlayerlike> predicate, UnityEngine.Color? color = null, bool canTrackInVent = false, bool ignoreCollider = false) => ForPlayerlike(lifespan, distance, tracker, predicate, null, color ?? UnityEngine.Color.yellow, canTrackInVent, ignoreCollider);
    public static ObjectTracker<IPlayerlike> ForPlayerlike(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<IPlayerlike> predicate, Predicate<IPlayerlike>? predicateHeavier, UnityEngine.Color? color, bool canTrackInVent = false, bool ignoreCollider = false) => ForPlayerlike(lifespan, distance, () => tracker.TruePosition.ToUnityVector(), predicate, predicateHeavier, color, canTrackInVent, ignoreCollider);
    public static ObjectTracker<IPlayerlike> ForPlayerlike(ILifespan? lifespan, float? distance, Func<Vector2?> tracker, Predicate<IPlayerlike> predicate, Predicate<IPlayerlike>? predicateHeavier, UnityEngine.Color? color, bool canTrackInVent = false, bool ignoreCollider = false)
    {
        if (!canTrackInVent)
        {
            var lastPredicate = predicate;
            predicate = p => !p.Logic.InVent && lastPredicate(p);
        }

        return new ObjectTrackerUnityImpl<IPlayerlike, IPlayerlike>(tracker, distance ?? AmongUsLLImpl.Instance.VanillaKillDistance, () => NebulaGameManager.Instance!.AllPlayerlike, predicate, predicateHeavier, p => p, p => [p.TruePosition], p => p.VanillaCosmetics.currentBodySprite.BodySprite, color, ignoreCollider).Register(lifespan);
    }

    public static ObjectTracker<Virial.Game.DeadBody> ForDeadBody(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<Virial.Game.DeadBody>? predicate = null, Predicate<Virial.Game.DeadBody>? predicateHeavier = null, UnityEngine.Color? color = null, bool ignoreCollider = false)
    {
        return new ObjectTrackerUnityImpl<Virial.Game.DeadBody, Virial.Game.DeadBody>(tracker.VanillaPlayer, distance ?? AmongUsLLImpl.Instance.VanillaKillDistance, () => ModSingleton<DeadBodyManager>.Instance.AllDeadBodies.Where(d => d.IsActive), predicate ?? (_ => true), predicateHeavier, d => d, d => [d.TruePosition], d => d.VanillaDeadBody.bodyRenderers[0], color, ignoreCollider).Register(lifespan);
    }

    public static ObjectTracker<Vent> ForVents(ILifespan? lifespan, float? distance, GamePlayer tracker, Predicate<Vent> predicate, UnityEngine.Color color, bool ignoreColliders = false)
    {
        return new ObjectTrackerUnityImpl<Vent, Vent>(tracker.VanillaPlayer, distance ?? AmongUsLLImpl.Instance.VanillaKillDistance, () => ShipStatus.Instance.AllVents, predicate, _ => true, v => v, v => [v.transform.position], v => v.myRend, color, ignoreColliders).Register(lifespan);
    }
}



public class ObjectTrackerUnityImpl<V,T> : FlexibleLifespan, ObjectTracker<V>, IGameOperator where V : class
{
    V? ObjectTracker<V>.CurrentTarget => currentTarget?.Item2;
    bool ObjectTracker<V>.IsLocked { get => isLocked; set => isLocked = value; }
    bool ObjectTracker<V>.KeepAsLongAsPossible { get => isSoftLocked; set => isSoftLocked = value; }

    private Tuple<T,V>? currentTarget = null;

    Func<Vector2?> tracker;
    Func<IEnumerable<T>> allTargets;
    Predicate<V> predicate;
    Predicate<V>? predicateHeavier = null;
    Func<T, V> converter;
    Func<T, IEnumerable<UnityEngine.Vector2>> positionConverter;
    Action<T, bool, Color> highlightSetter;
    UnityEngine.Color color = UnityEngine.Color.yellow;
    bool ignoreColliders;
    bool ignoreShadows;
    float maxDistance;
    private bool isLocked = false;
    private bool isSoftLocked = false;
    public bool MoreHighlight = false;

    public ObjectTrackerUnityImpl(PlayerControl tracker, float maxDistance, Func<IEnumerable<T>> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, IEnumerable<Vector2>> positionConverter, Func<T, SpriteRenderer> rendererConverter, Color? color = null, bool ignoreColliders = false, bool ignoreShadows = true)
    : this(() => tracker ? tracker.GetTruePosition() : null, maxDistance, allTargets, predicate, predicateHeavier, converter, positionConverter, rendererConverter, color, ignoreColliders, ignoreShadows) { }

    internal ObjectTrackerUnityImpl(Func<Vector2?> tracker, float maxDistance, Func<IEnumerable<T>> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, IEnumerable<Vector2>> positionConverter, Func<T, SpriteRenderer> rendererConverter, Color? color = null, bool ignoreColliders = false, bool ignoreShadows = true)
        : this(tracker, maxDistance, allTargets, predicate, predicateHeavier, converter, positionConverter, (target, more, color) =>
        {
            var renderer = rendererConverter.Invoke(target);
            if (renderer)
            {
                if (more)
                    AmongUsUtil.SetHighlight(renderer, true, color);
                else
                    HighlightHelpers.SetHighlight(renderer, color);
            }
        }, color, ignoreColliders, ignoreShadows) { }

    private ObjectTrackerUnityImpl(Func<Vector2?> tracker, float maxDistance, Func<IEnumerable<T>> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, IEnumerable<Vector2>> positionConverter, Action<T, bool, Color> highlightSetter, Color? color = null, bool ignoreColliders = false, bool ignoreShadows = true)
    {
        this.tracker = tracker;
        this.allTargets = allTargets;
        this.predicate = predicate;
        this.predicateHeavier = predicateHeavier;
        this.converter = converter;
        this.positionConverter = positionConverter;
        this.highlightSetter = highlightSetter;
        this.maxDistance = maxDistance;
        this.ignoreColliders = ignoreColliders;
        this.ignoreShadows = ignoreShadows;
        if (color.HasValue) this.color = color.Value;
        this.ignoreShadows = ignoreShadows;
    }

    public void SetColor(UnityEngine.Color color) => this.color = color;
    private void ShowTarget()
    {
        if (currentTarget == null) return;

        highlightSetter?.Invoke(currentTarget!.Item1, MoreHighlight, color);
    }

    void HudUpdate(GameHudUpdateEvent ev)
    {
        if (isLocked)
        {
            ShowTarget();
            return;
        }

        Vector2? calced = tracker.Invoke();
        if (!calced.HasValue)
        {
            currentTarget = null;
            return;

        }
        Vector2 myPos = calced.Value;

        float distance = maxDistance;
        Tuple<T,V>? candidate = null;

        foreach (var t in allTargets())
        {
            var v = converter(t);
            if (v == null) continue;

            if (!predicate(v)) continue;

            Vector2[] pos = positionConverter(t).ToArray();

            if (pos.Length == 0) continue;//点を持たないオブジェクトは無視する

            Vector2 dVec = pos[0] - myPos; //先頭を最たる中心として扱う
            float magnitude = dVec.magnitude;

            if (MoreHighlight && magnitude < 4.5f) highlightSetter?.Invoke(t, false, color);

            if (distance < magnitude) continue;

            if (!ignoreColliders && pos.All(p => PhysicsHelpers.AnyNonTriggersBetween(myPos, (p - myPos).normalized, magnitude, Constants.ShipAndObjectsMask))) continue;
            if (!ignoreShadows && pos.All(p => NebulaPhysicsHelpers.AnyShadowBetween(p, myPos, out _))) continue;
            if (!(predicateHeavier?.Invoke(v) ?? true)) continue;

            if (candidate != null && currentTarget != null && isSoftLocked && candidate.Item2 == currentTarget.Item2) continue;
            candidate = new(t,v);
            distance = magnitude;
        }

        currentTarget = candidate;
        ShowTarget();
    }
}