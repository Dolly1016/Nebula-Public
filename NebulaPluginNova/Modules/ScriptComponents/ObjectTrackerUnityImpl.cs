
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
    public static void SetDeadBodyHighlight(GamePlayer player, Virial.Color color) => SetHighlight(player.RelatedDeadBody, color.ToUnityColor());
}

public static class ObjectTrackers
{
    static public Predicate<GamePlayer> StandardPredicateIgnoreOwner = p => !p.IsDead && !p.WillDie && !p.Unbox().IsInvisible && !p.IsDived && !p.IsBlown;
    static public Predicate<GamePlayer> StandardPredicate = p => !p.AmOwner && StandardPredicateIgnoreOwner.Invoke(p);
    static public Predicate<GamePlayer> KillablePredicate(GamePlayer myPlayer) => p => StandardPredicate(p) && myPlayer.CanKill(p);
    static public Predicate<GamePlayer> LocalKillablePredicate = p => StandardPredicate(p) && (GamePlayer.LocalPlayer?.CanKill(p) ?? true);

    public static ObjectTracker<GamePlayer> ForPlayer(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, UnityEngine.Color? color = null, bool canTrackInVent = false, bool ignoreCollider = false) => ForPlayer(distance, tracker, predicate, null, color ?? UnityEngine.Color.yellow, canTrackInVent, ignoreCollider);
    public static ObjectTracker<GamePlayer> ForPlayer(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, Predicate<GamePlayer>? predicateHeavier, UnityEngine.Color? color, bool canTrackInVent = false, bool ignoreCollider = false)
    {
        if (!canTrackInVent)
        {
            var lastPredicate = predicate;
            predicate = p => !p.VanillaPlayer.inVent && lastPredicate(p);
        }
        IEnumerable<PlayerControl> FastPlayers() => PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => p);

        return new ObjectTrackerUnityImpl<GamePlayer, PlayerControl>(tracker.VanillaPlayer, distance ?? AmongUsUtil.VanillaKillDistance, FastPlayers, predicate, predicateHeavier, p => p.GetModInfo(), p => [p.GetTruePosition()], p => p.cosmetics.currentBodySprite.BodySprite, color, ignoreCollider);
    }

    public static ObjectTracker<GamePlayer> ForDeadBody(float? distance, GamePlayer tracker, Predicate<GamePlayer> predicate, Predicate<GamePlayer>? predicateHeavier = null, UnityEngine.Color? color = null, bool ignoreCollider = false)
    {
        return new ObjectTrackerUnityImpl<GamePlayer, DeadBody>(tracker.VanillaPlayer, distance ?? AmongUsUtil.VanillaKillDistance, () => Helpers.AllDeadBodies().Where(d => d.bodyRenderers.Any(r => r.enabled)), predicate, predicateHeavier, d => NebulaGameManager.Instance.GetPlayer(d.ParentId), d => [d.TruePosition], d => d.bodyRenderers[0], color, ignoreCollider);
    }

    public static ObjectTracker<Vent> ForVents(float? distance, GamePlayer tracker, Predicate<Vent> predicate, UnityEngine.Color color, bool ignoreColliders = false)
    {
        return new ObjectTrackerUnityImpl<Vent, Vent>(tracker.VanillaPlayer, distance ?? AmongUsUtil.VanillaKillDistance, () => ShipStatus.Instance.AllVents, predicate, _ => true, v => v, v => [v.transform.position], v => v.myRend, color, ignoreColliders);
    }
}



public class ObjectTrackerUnityImpl<V,T> : FlexibleLifespan, ObjectTracker<V>, IGameOperator where V : class
{
    V? ObjectTracker<V>.CurrentTarget => currentTarget?.Item2;
    bool ObjectTracker<V>.IsLocked { get => isLocked; set => isLocked = value; }

    private Tuple<T,V>? currentTarget = null;

    PlayerControl tracker;
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
    public bool MoreHighlight = false;

    public ObjectTrackerUnityImpl(PlayerControl tracker, float maxDistance, Func<IEnumerable<T>> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, IEnumerable<Vector2>> positionConverter, Func<T, SpriteRenderer> rendererConverter, Color? color = null, bool ignoreColliders = false, bool ignoreShadows = true)
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

    public ObjectTrackerUnityImpl(PlayerControl tracker, float maxDistance, Func<IEnumerable<T>> allTargets, Predicate<V> predicate, Predicate<V>? predicateHeavier, Func<T, V> converter, Func<T, IEnumerable<Vector2>> positionConverter, Action<T, bool, Color> highlightSetter, Color? color = null, bool ignoreColliders = false, bool ignoreShadows = true)
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

        if (!tracker)
        {
            currentTarget = null;
            return;
        }

        Vector2 myPos = tracker.GetTruePosition();

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

            if (MoreHighlight && magnitude < 4.5f) highlightSetter?.Invoke(currentTarget!.Item1, false, color);

            if (distance < magnitude) continue;

            if (!ignoreColliders && pos.All(p => PhysicsHelpers.AnyNonTriggersBetween(myPos, (p - myPos).normalized, magnitude, Constants.ShipAndObjectsMask))) continue;
            if (!ignoreShadows && pos.All(p => NebulaPhysicsHelpers.AnyShadowBetween(p, myPos, out _))) continue;
            if (!(predicateHeavier?.Invoke(v) ?? true)) continue;

            candidate = new(t,v);
            distance = magnitude;
        }

        currentTarget = candidate;
        ShowTarget();
    }
}