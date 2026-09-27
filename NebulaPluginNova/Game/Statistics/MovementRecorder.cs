using Nebula.Modules.Cosmetics;
using System.Collections.Generic;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Game.Statistics;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class MovementRecorder : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public const float Interval = GameInfrequentUpdateEvent.Interval;

    private const int Digits = 1;

    private const float PreallocatedSeconds = 60f;

    private const int PreallocatedCount = (int)(PreallocatedSeconds / Interval);

    static public void Preprocess(NebulaPreprocessor preprocess) =>
        DIManager.Instance.RegisterModule(() => new MovementRecorder());

    private MovementRecorder()
    {
        ModSingleton<MovementRecorder>.Instance = this;
        this.RegisterPermanently();
    }

    private readonly List<ArchivedMovementPhase> phases = [];

    public IReadOnlyList<ArchivedMovementPhase> Phases => phases;

    private Recording? recording = null;

    private sealed class Recording
    {
        public float StartTime { get; }
        public int Count { get; set; }
        public IReadOnlyList<int> InitialObjectIds { get; } = ModSingleton<MapObjectRecorder>.Instance?.AliveIds ?? [];

        public GamePlayer[] Players { get; }
        public List<float>[] Points { get; }
        public List<int>[] States { get; }
        public Dictionary<int, FakeRecording> Fakes { get; } = [];

        public Recording(float startTime, GamePlayer[] players)
        {
            StartTime = startTime;
            Players = players;
            Points = new List<float>[players.Length];
            States = new List<int>[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                //座標は1点につきx,yの2つ。
                Points[i] = new List<float>(PreallocatedCount * 2);
                States[i] = new List<int>(PreallocatedCount);
            }
        }

        public ArchivedMovementPhase ToArchive() => new(StartTime, Interval,
            [.. System.Linq.Enumerable.Range(0, Players.Length)
                .Select(i => new ArchivedMovementTrack(Players[i].PlayerId, Points[i], States[i]))],
            [.. Fakes.Values.Select(fake => fake.ToArchive())],
            InitialObjectIds);
    }

    private sealed class FakeRecording(int fakeId, byte? ownerId, string reason, ArchivedPlayerColor? color, int startIndex)
    {
        public List<float> Points { get; } = new(PreallocatedCount * 2);
        public List<int> States { get; } = new(PreallocatedCount);

        public ArchivedFakeTrack ToArchive() => new(fakeId, ownerId, reason, color, startIndex, Points, States);
    }

    internal void OnEventRecorded(GameStatistics.Event recorded)
    {
        var segment = (recorded.EventVariation as GameStatistics.EventVariation)?.Segment ?? EventSegment.None;

        if (segment == EventSegment.TurnStart)
            Begin(recorded.Time);

        else if (segment is EventSegment.MeetingStart or EventSegment.GameEnd)
        {
            ModSingleton<MapObjectRecorder>.Instance?.DropTransients();
            Finish();
        }
    }

    private void Begin(float time)
    {
        Finish();

        recording = new Recording(time, [.. GamePlayer.AllPlayers]);
    }

    private void Finish()
    {
        if (recording == null) return;

        if (recording.Count > 0) phases.Add(recording.ToArchive());
        recording = null;
    }

    void OnInfrequentUpdate(GameInfrequentUpdateEvent ev)
    {
        if (recording == null) return;

        int wanted = (int)((ev.GameTime - recording.StartTime) / Interval) + 1;
        while (recording.Count < wanted)
        {
            Sample();
            recording.Count++;
        }
    }

    private void Sample()
    {
        var current = recording!;

        for (int i = 0; i < current.Players.Length; i++)
        {
            var player = current.Players[i];
            var position = player.Position;

            current.Points[i].Add(Round(position.x));
            current.Points[i].Add(Round(position.y));

            current.States[i].Add((int)StateOf(player));
        }

        SampleFakes(current);

        ModSingleton<MapObjectRecorder>.Instance?.Sample(current.StartTime + current.Count * Interval);
    }

    private void SampleFakes(Recording current)
    {
        foreach (var playerlike in NebulaAPI.CurrentGame?.GetAllPlayerlikes() ?? [])
        {
            if (playerlike is not IFakePlayer fake) continue;
            if (!fake.IsActive) continue;

            if (!current.Fakes.TryGetValue(fake.PlayerlikeId, out var track))
            {
                track = new FakeRecording(
                    fake.PlayerlikeId,
                    fake.Owner?.PlayerId,
                    fake.SpawnReason?.TranslationKey ?? "",
                    ColorOf(fake),
                    current.Count);
                current.Fakes[fake.PlayerlikeId] = track;
            }

            var position = fake.Position;
            track.Points.Add(Round(position.x));
            track.Points.Add(Round(position.y));
            track.States.Add((int)StateOf(fake));
        }
    }

    static private ArchivedPlayerColor? ColorOf(IFakePlayer fake)
    {
        var colorId = fake.CurrentOutfit.outfit.ColorId;
        if (colorId < 0 || colorId >= DynamicPalette.PlayerColors.Length) return null;

        return ArchivedPlayerColor.From(new(
            DynamicPalette.PlayerColors[colorId],
            DynamicPalette.ShadowColors[colorId],
            DynamicPalette.VisorColors[colorId]));
    }

    static private ArchivedMovementState StateOf(IPlayerlike player)
    {
        var state = ArchivedMovementState.None;

        if (player.IsDead) state |= ArchivedMovementState.Dead;
        if (player.IsInvisible) state |= ArchivedMovementState.Invisible;
        if (player.IsDived) state |= ArchivedMovementState.Dived;
        if (player.IsBlown) state |= ArchivedMovementState.Blown;

        var logic = player.Logic;
        if (logic != null)
        {
            if (logic.InVent) state |= ArchivedMovementState.InVent;
            if (logic.OnLadder || logic.InMovingPlat) state |= ArchivedMovementState.Riding;
        }

        return state;
    }

    static private float Round(float value) => (float)System.Math.Round(value, Digits);
}
