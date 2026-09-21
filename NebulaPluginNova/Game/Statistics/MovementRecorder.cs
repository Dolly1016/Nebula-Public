using Nebula.Modules.Cosmetics;
using System.Collections.Generic;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Game.Statistics;

/// <summary>
/// タスクフェイズ中のプレイヤーの居場所を、一定の間隔で記録する。
/// </summary>
/// <remarks>
/// <para>
/// 位置は各クライアントが自分の見ている値をそのまま控える。全プレイヤーの座標はローカルで参照できるので、
/// 記録のための通信は要らない。
/// </para>
/// <para>
/// 区切りは <see cref="GameStatistics.RecordEvent"/> から知らされる出来事で決める。
/// ゲーム開始と会議終了で計測を始め、会議開始とゲーム終了で止める。
/// 出来事の記録と同じ瞬間に区切るので、足取りの境目と出来事の時刻が食い違わない。
/// </para>
/// <para>生死は問わず全員を記録する。</para>
/// </remarks>
[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class MovementRecorder : AbstractModule<Virial.Game.Game>, IGameOperator
{
    /// <summary>記録の間隔(秒)。</summary>
    public const float Interval = 0.25f;

    /// <summary>座標を丸める桁数。10 cm 刻み。</summary>
    private const int Digits = 1;

    static public void Preprocess(NebulaPreprocessor preprocess) =>
        DIManager.Instance.RegisterModule(() => new MovementRecorder());

    private MovementRecorder()
    {
        ModSingleton<MovementRecorder>.Instance = this;
        this.RegisterPermanently();
    }

    private readonly List<ArchivedMovementPhase> phases = [];

    /// <summary>記録し終えたタスクフェイズ。古い順。</summary>
    public IReadOnlyList<ArchivedMovementPhase> Phases => phases;

    /// <summary>記録中のタスクフェイズ。計測していなければ null。</summary>
    private Recording? recording = null;

    private sealed class Recording
    {
        /// <summary>計測を始めたときのゲーム内時刻。点の時刻はここからの経過で決まる。</summary>
        public float StartTime { get; }

        /// <summary>これまでに記録した点の数。</summary>
        public int Count { get; set; }

        public GamePlayer[] Players { get; }
        public List<float>[] Points { get; }
        public List<int>[] States { get; }

        /// <summary>ニセモノの足取り。湧いた順に増える。</summary>
        public Dictionary<int, FakeRecording> Fakes { get; } = [];

        public Recording(float startTime, GamePlayer[] players)
        {
            StartTime = startTime;
            Players = players;
            Points = new List<float>[players.Length];
            States = new List<int>[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                Points[i] = [];
                States[i] = [];
            }
        }

        public ArchivedMovementPhase ToArchive() => new(StartTime, Interval,
            [.. System.Linq.Enumerable.Range(0, Players.Length)
                .Select(i => new ArchivedMovementTrack(Players[i].PlayerId, Points[i], States[i]))],
            [.. Fakes.Values.Select(fake => fake.ToArchive())]);
    }

    /// <summary>
    /// ニセモノ1体ぶんの記録。途中から始まるので、始まりの位置を覚えておく。
    /// </summary>
    private sealed class FakeRecording(int fakeId, byte? ownerId, string reason, ArchivedPlayerColor? color, int startIndex)
    {
        public List<float> Points { get; } = [];
        public List<int> States { get; } = [];

        public ArchivedFakeTrack ToArchive() => new(fakeId, ownerId, reason, color, startIndex, Points, States);
    }

    /// <summary>
    /// 出来事が記録されたときに呼ばれる。その種類で区切りを決める。
    /// </summary>
    internal void OnEventRecorded(GameStatistics.Event recorded)
    {
        var variation = recorded.EventVariation.Id;

        if (variation == GameStatistics.EventVariation.GameStart.Id
            || variation == GameStatistics.EventVariation.MeetingEnd.Id)
            Begin(recorded.Time);

        else if (variation == GameStatistics.EventVariation.Report.Id
            || variation == GameStatistics.EventVariation.EmergencyButton.Id
            || variation == GameStatistics.EventVariation.GameEnd.Id)
            Finish();
    }

    private void Begin(float time)
    {
        //会議を挟まずに始まり直した場合に備えて、前の分を閉じてから始める。
        Finish();

        recording = new Recording(time, [.. GamePlayer.AllPlayers]);
    }

    private void Finish()
    {
        if (recording == null) return;

        //一点も取れていないフェイズは残しても意味がない。
        if (recording.Count > 0) phases.Add(recording.ToArchive());
        recording = null;
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        if (recording == null) return;

        //添字と時刻の対応を崩さないため、経過時間ぶんの目盛りは必ず埋める。
        //フレームが飛んだときは直前と同じ場所を並べることになる。
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
    }

    /// <summary>
    /// 今いるニセモノを記録する。
    /// </summary>
    /// <remarks>
    /// 本物と違って湧き消えするので、初めて見たものはその時点から記録を始める。
    /// 消えたものは追記が止まるだけで、それまでの分はそのまま残る。
    /// </remarks>
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

    /// <summary>ニセモノの見た目の配色。取れなければ null。</summary>
    static private ArchivedPlayerColor? ColorOf(IFakePlayer fake)
    {
        var colorId = fake.CurrentOutfit.outfit.ColorId;
        if (colorId < 0 || colorId >= DynamicPalette.PlayerColors.Length) return null;

        return ArchivedPlayerColor.From(new(
            DynamicPalette.PlayerColors[colorId],
            DynamicPalette.ShadowColors[colorId],
            DynamicPalette.VisorColors[colorId]));
    }

    /// <summary>
    /// その瞬間のプレイヤーの状態。
    /// </summary>
    /// <remarks>
    /// 座標だけでは「なぜそこへ移ったのか」が分からない。
    /// ベント・地中・吹き飛ばし・はしごや動く床は、いずれも歩いて動いたのではないので、
    /// 経路を読み返すときに欠かせない。
    /// </remarks>
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
