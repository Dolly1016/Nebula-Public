using System;
using System.Collections.Generic;
using NVector2 = System.Numerics.Vector2;

namespace Nebula.Player.PositionSync;

// このファイルはUnity/Nebulaに依存しない純粋なロジックのみを置く。
// 比較用シミュレーション(C:/AmongUsModding/verify)からも同じファイルをリンクして使う。

/// <summary>
/// 位置同期パケット。Positionsは連続したtickの位置で、末尾がNewestTickの位置。
/// FlipFlagsのiビット目がPositions[i]の時点での向き(左向きなら1)。
/// Idleなら送信側は停止しており、次のパケットまで位置は変わらない。
/// </summary>
public readonly struct PositionSyncPacket
{
    public readonly byte Epoch;
    public readonly int NewestTick;
    public readonly NVector2[] Positions;
    public readonly byte FlipFlags;
    public readonly bool Idle;

    public PositionSyncPacket(byte epoch, int newestTick, NVector2[] positions, byte flipFlags, bool idle)
    {
        Idle = idle;
        Epoch = epoch;
        NewestTick = newestTick;
        Positions = positions;
        FlipFlags = flipFlags;
    }

    public bool GetFlip(int index) => (FlipFlags & (1 << index)) != 0;

    public int OldestTick => NewestTick - Positions.Length + 1;
}

/// <summary>
/// 送信側。FixedUpdateごとに<see cref="Tick"/>を呼び出す。
/// </summary>
public sealed class PositionSyncSender
{
    /// <summary>何tickごとに送信するか。</summary>
    public const int SendIntervalTicks = 2;
    /// <summary>1パケットに含める直近の位置の数。送信間隔の3回分を重ねて送り、パケットロスに備える。(向きを1バイトに詰めるので8以下)</summary>
    public const int PositionsPerPacket = 6;
    /// <summary>停止後もしばらく送り続けるtick数。</summary>
    public const int TrailingTicksAfterStop = 10;
    /// <summary>停止中に生存確認として送る間隔。</summary>
    public const int IdleKeepAliveTicks = 10;

    const float MoveThresholdSqr = 0.0001f * 0.0001f;

    readonly NVector2[] history = new NVector2[PositionsPerPacket];
    readonly bool[] flipHistory = new bool[PositionsPerPacket];
    int historyCount = 0;
    int historyHead = 0; // 次に書き込む位置
    int lastRecordedTick = int.MinValue;
    NVector2 lastRecordedPos;
    bool lastRecordedFlip;
    int lastMovedTick = int.MinValue / 2;
    int lastSentTick = int.MinValue / 2;
    byte epoch = 0;

    public byte Epoch => epoch;

    /// <summary>
    /// 位置が不連続になったことを通知する。(スナップ、ポーズ解除など)
    /// これより前の位置は以降のパケットに含まれない。
    /// </summary>
    public void MarkDiscontinuity()
    {
        unchecked { epoch++; }
        historyCount = 0;
        historyHead = 0;
        lastRecordedTick = int.MinValue;
        lastMovedTick = int.MinValue / 2;
        lastSentTick = int.MinValue / 2; //次のtickで即座に送らせる
    }

    /// <summary>
    /// 現在tickの位置を記録し、送信すべきならパケットを返す。
    /// </summary>
    public bool Tick(int tick, NVector2 position, bool flipX, out PositionSyncPacket packet)
    {
        packet = default;

        if (historyCount > 0 && tick != lastRecordedTick + 1)
        {
            //tickが飛んだ(FixedUpdateが呼ばれなかった)場合は連続性が保証できないので履歴を捨てる。epochは変えない。
            historyCount = 0;
            historyHead = 0;
        }

        bool firstRecord = historyCount == 0;
        //向きだけが変わった場合も動いたとみなして送る
        if (firstRecord || (position - lastRecordedPos).LengthSquared() > MoveThresholdSqr || flipX != lastRecordedFlip) lastMovedTick = tick;

        history[historyHead] = position;
        flipHistory[historyHead] = flipX;
        historyHead = (historyHead + 1) % PositionsPerPacket;
        if (historyCount < PositionsPerPacket) historyCount++;
        lastRecordedTick = tick;
        lastRecordedPos = position;
        lastRecordedFlip = flipX;

        bool moving = tick - lastMovedTick <= TrailingTicksAfterStop;
        bool shouldSend = moving ? (tick - lastSentTick >= SendIntervalTicks) : (tick - lastSentTick >= IdleKeepAliveTicks);
        if (!shouldSend) return false;

        //停止中は最新の1点だけで十分
        int count = moving ? historyCount : 1;
        var positions = new NVector2[count];
        byte flipFlags = 0;
        for (int i = 0; i < count; i++)
        {
            int index = (historyHead - count + i + PositionsPerPacket * 2) % PositionsPerPacket;
            positions[i] = history[index];
            if (flipHistory[index]) flipFlags |= (byte)(1 << i);
        }
        packet = new PositionSyncPacket(epoch, tick, positions, flipFlags, !moving);
        lastSentTick = tick;
        return true;
    }
}

/// <summary>
/// 受信側で、求めた位置に向けて体をどう動かすかを決める。
/// </summary>
public static class PositionSyncMotion
{
    /// <summary>これ以上ずれていれば移動ではなく位置を直接合わせる。</summary>
    public const float TeleportDistance = 0.5f;
    /// <summary>ずれを解消するのにかける時間(秒)。</summary>
    public const float CorrectionTime = 0.06f;

    /// <param name="bodyPosition">現在の体の位置</param>
    /// <param name="current">いま表示すべき位置</param>
    /// <param name="ahead">物理ステップ後に表示すべき位置</param>
    /// <param name="dt">物理ステップの時間</param>
    /// <param name="teleportTo">位置を直接合わせる場合はその位置</param>
    /// <returns>体に与える速度</returns>
    public static NVector2 Compute(NVector2 bodyPosition, NVector2 current, NVector2 ahead, float dt, out NVector2? teleportTo)
    {
        teleportTo = null;
        var error = current - bodyPosition;
        if (error.LengthSquared() > TeleportDistance * TeleportDistance)
        {
            teleportTo = current;
            error = NVector2.Zero;
        }

        //本来の動き + ずれの一部を解消する動き。ずれが無ければ物理ステップ後にちょうどaheadに着く。
        var velocity = (ahead - current) / dt + error * Math.Min(1f, dt / CorrectionTime) / dt;
        if (velocity.LengthSquared() < 0.0001f) velocity = NVector2.Zero;
        return velocity;
    }
}

/// <summary>
/// 値の揺らぎを監視する。一定時間の窓の中で、指定したパーセンタイルと最小値の差を揺らぎとする。
/// 高いパーセンタイルを使うことで、平均ではなく「稀なスパイクを除いた平常時の最悪値」を見積もる。
/// </summary>
public sealed class JitterMonitor
{
    readonly float window;
    readonly float percentile;
    readonly Queue<(float time, float value)> values = new();
    readonly List<float> sortBuffer = new();

    public JitterMonitor(float window, float percentile)
    {
        this.window = window;
        this.percentile = percentile;
    }

    /// <summary>窓の中の最小値。</summary>
    public float Min { get; private set; } = float.NaN;
    /// <summary>揺らぎ。(パーセンタイル値 - 最小値)</summary>
    public float Jitter { get; private set; } = 0f;
    public bool HasValue => values.Count > 0;

    public void Clear()
    {
        values.Clear();
        Min = float.NaN;
        Jitter = 0f;
    }

    public void Add(float time, float value)
    {
        values.Enqueue((time, value));
        while (values.Count > 1 && values.Peek().time < time - window) values.Dequeue();

        float min = float.PositiveInfinity;
        foreach (var v in values) if (v.value < min) min = v.value;
        Min = min;

        sortBuffer.Clear();
        foreach (var v in values) sortBuffer.Add(v.value - min);
        sortBuffer.Sort();
        Jitter = sortBuffer[Math.Min(sortBuffer.Count - 1, (int)(sortBuffer.Count * percentile))];
    }
}

/// <summary>
/// 受信側。届いた点を送信側のtick時刻で並べ、「最新時刻 - 目標遅延」の位置を補間して返す。
/// 点の消化速度に上限がないため、溜まった遅延が解消されないという問題が起きない。
/// </summary>
/// <remarks>
/// 再生時刻は時計から直接求めず、再生速度を調整しながら進める。
/// 届いた最新の点に近づいたら減速して待つ。(先回りの予測はしない)
/// 減速を始める位置と目標遅延は、揺らぎの大きさに合わせて決める。
/// 表示が止まっている間は、見た目を変えずに再生時刻を目標まで進めておく。
/// </remarks>
public sealed class PositionSyncReceiver
{
    /// <summary>目標遅延の最大値(秒)。</summary>
    public const float MaxDelay = 0.25f;
    /// <summary>到着時刻の揺らぎを見積もる窓幅(秒)。短いと、静かな時間が少し続いただけで見積もりが下がってしまう。</summary>
    public const float OffsetWindow = 5f;
    /// <summary>揺らぎとみなすパーセンタイル。これより上は稀なスパイクとして扱う。</summary>
    public const float JitterPercentile = 0.97f;
    /// <summary>見積もった揺らぎに上乗せする割合。</summary>
    public const float JitterMarginRatio = 0.1f;
    /// <summary>見積もった揺らぎに上乗せする量(秒)。</summary>
    public const float JitterMarginSeconds = 0.005f;
    /// <summary>再生速度を1からずらす最大量。遅れを取り戻すときは最大1+この値の速さで再生する。</summary>
    public const float MaxRateDeviation = 0.25f;
    /// <summary>目標の再生時刻とのずれ(tick)に対する再生速度の変化量。</summary>
    public const float RateGainPerTick = 0.05f;
    /// <summary>目標の再生時刻とのずれがこの量(tick)以内なら、再生速度を変えない。</summary>
    public const float RateDeadbandTicks = 1f;
    /// <summary>減速域の幅を、揺らぎ(tick)の何倍にするか。揺らぎは平常時の最悪値で見積もっているので、減速が要るのはそれを超えるスパイクのときだけ。</summary>
    public const float DecelZonePerJitter = 0.25f;
    /// <summary>減速域の最小幅(tick)。揺らぎが小さくても、最新の点の手前で急ブレーキにならないようにする。</summary>
    public const float MinDecelZoneTicks = 1f;
    /// <summary>減速域の最大幅(tick)。</summary>
    public const float MaxDecelZoneTicks = 3f;
    /// <summary>目標の再生時刻とこれ以上(秒)ずれたら、取り戻さずに合わせ直す。(長時間途切れた後など)</summary>
    public const float ResyncThreshold = 0.6f;
    /// <summary>ローカルでスナップした直後、同じepochの古いパケットを無視する時間(秒)。</summary>
    public const float IgnoreAfterLocalSnap = 0.3f;
    /// <summary>この距離以下しか離れていない点どうしは、止まっているとみなす。</summary>
    const float StationaryDistanceSqr = 0.0001f * 0.0001f;

    struct Sample
    {
        public int Tick;
        public byte Epoch;
        public NVector2 Position;
        public bool Flip;
    }

    readonly float tickDuration;
    readonly List<Sample> samples = new(); //tick昇順
    readonly JitterMonitor arrivalOffsets = new(OffsetWindow, JitterPercentile);

    bool hasEpoch = false;
    byte epoch;
    float epochChangedAt = float.NegativeInfinity;
    bool ignoringCurrentEpoch = false;
    float ignoreUntil = 0f;

    float externalJitter = 0f;
    float jitter = 0f;
    float decelZoneTicks = MinDecelZoneTicks;
    float targetDelay;

    bool hasRenderTick = false;
    double renderTick;
    float lastNow;
    int newestTick = int.MinValue;
    bool newestIsIdle = false;

    public PositionSyncReceiver(float tickDuration)
    {
        this.tickDuration = tickDuration;
        UpdateTargetDelay();
    }

    public bool HasSamples => samples.Count > 0;
    /// <summary>現在の揺らぎの見積もり(秒)。余裕を含む。</summary>
    public float Jitter => jitter;
    /// <summary>目標遅延(秒)。通信の最小遅延は含まない。</summary>
    public float TargetDelay => targetDelay;
    /// <summary>実際の遅延(秒)。通信の最小遅延は含まない。</summary>
    public float CurrentDelay => hasRenderTick && arrivalOffsets.HasValue ? (float)(lastNow - arrivalOffsets.Min - renderTick * tickDuration) : targetDelay;

    static bool EpochNewer(byte a, byte b) => (sbyte)(a - b) > 0;

    /// <summary>
    /// 到着時刻以外から見積もった片道の揺らぎ(秒)を設定する。(自身のpingの揺らぎなど)
    /// 到着時刻から見積もった揺らぎと大きいほうを使う。
    /// </summary>
    public void SetExternalJitter(float oneWayJitter)
    {
        externalJitter = Math.Max(0f, oneWayJitter);
        UpdateTargetDelay();
    }

    void UpdateTargetDelay()
    {
        //平常時の最悪値に、ほんの少し余裕を持たせる
        jitter = Math.Max(arrivalOffsets.Jitter, externalJitter) * (1f + JitterMarginRatio) + JitterMarginSeconds;
        //揺らぎが大きいほど、手前から緩やかに減速する
        decelZoneTicks = Math.Clamp(jitter / tickDuration * DecelZonePerJitter, MinDecelZoneTicks, MaxDecelZoneTicks);
        //送信間隔ぶんは次のパケットが届くまでのつなぎとして、減速域のぶんは平常時に減速しないために必要
        targetDelay = Math.Clamp(jitter + tickDuration * (PositionSyncSender.SendIntervalTicks + decelZoneTicks), tickDuration, MaxDelay);
    }

    /// <summary>位置の点をすべて捨てる。(時刻の推定は保持する)</summary>
    public void ClearSamples()
    {
        samples.Clear();
        hasRenderTick = false;
        newestTick = int.MinValue;
        newestIsIdle = false;
    }

    /// <summary>受信側でスナップが起きたときに呼ぶ。</summary>
    public void OnLocalSnap(float now)
    {
        ClearSamples();
        //直前にepochが変わっていたなら、そのスナップに対応する新しい点がすでに届いているとみなす
        if (now - epochChangedAt > 0.5f)
        {
            ignoringCurrentEpoch = true;
            ignoreUntil = now + IgnoreAfterLocalSnap;
        }
    }

    public void OnPacket(in PositionSyncPacket packet, float now)
    {
        if (packet.Positions.Length == 0) return;

        //時刻の推定は、epochに関係なく全パケットで行う
        arrivalOffsets.Add(now, now - packet.NewestTick * tickDuration);
        UpdateTargetDelay();

        if (!hasEpoch || EpochNewer(packet.Epoch, epoch))
        {
            hasEpoch = true;
            epoch = packet.Epoch;
            epochChangedAt = now;
            ignoringCurrentEpoch = false;
            //新しいepochの最初の点以降にある古いepochの点は無効
            int oldest = packet.OldestTick;
            samples.RemoveAll(s => s.Tick >= oldest);
            if (newestTick >= oldest)
            {
                newestTick = samples.Count > 0 ? samples[samples.Count - 1].Tick : int.MinValue;
                newestIsIdle = false;
            }
        }
        else if (packet.Epoch != epoch)
        {
            return; //古いepoch
        }
        else if (ignoringCurrentEpoch)
        {
            if (now < ignoreUntil) return;
            ignoringCurrentEpoch = false;
        }

        for (int i = 0; i < packet.Positions.Length; i++) Insert(packet.OldestTick + i, packet.Epoch, packet.Positions[i], packet.GetFlip(i));
        if (packet.NewestTick >= newestTick)
        {
            newestTick = packet.NewestTick;
            newestIsIdle = packet.Idle;
        }
    }

    void Insert(int tick, byte sampleEpoch, NVector2 pos, bool flip)
    {
        //多くの場合は末尾への追加になる
        int index = samples.Count;
        while (index > 0 && samples[index - 1].Tick >= tick)
        {
            if (samples[index - 1].Tick == tick) return; //重複
            index--;
        }
        //すでに再生し終えた時刻の点は不要
        if (index == 0 && samples.Count > 0 && hasRenderTick && tick < renderTick - 1) return;
        samples.Insert(index, new Sample { Tick = tick, Epoch = sampleEpoch, Position = pos, Flip = flip });
    }

    /// <summary>
    /// 再生時刻からtoTickまでの間、表示が止まったままかどうか。
    /// </summary>
    bool IsStationaryUntil(double toTick)
    {
        var current = Evaluate(renderTick);
        foreach (var s in samples)
        {
            if (s.Tick <= renderTick) continue;
            if (s.Tick > toTick) break;
            if ((s.Position - current).LengthSquared() > StationaryDistanceSqr) return false;
        }
        //範囲の外の点との補間で動いている場合もあるので、行き先の位置そのものも確認する
        if ((Evaluate(toTick) - current).LengthSquared() > StationaryDistanceSqr) return false;
        //toTickが最新の点より先なら、停止中のパケットで保証されている範囲に限る
        return toTick <= newestTick || newestIsIdle;
    }

    /// <summary>
    /// 時刻nowにおいて表示すべき位置を求める。
    /// </summary>
    /// <param name="now">受信側の現在時刻。</param>
    /// <param name="deltaTime">前回の呼び出しからの経過時間。</param>
    /// <param name="lookAhead">nowからさらに先の時刻を求める秒数。(物理ステップ後の位置を求める用途)</param>
    /// <param name="position">時刻nowの位置。</param>
    /// <param name="aheadPosition">時刻now+lookAheadの位置。</param>
    /// <param name="flipX">時刻nowの向き。</param>
    public bool TryGetPosition(float now, float deltaTime, float lookAhead, out NVector2 position, out NVector2 aheadPosition, out bool flipX)
    {
        position = default;
        aheadPosition = default;
        flipX = false;
        if (!arrivalOffsets.HasValue || samples.Count == 0) return false;
        lastNow = now;

        double targetTick = (now - arrivalOffsets.Min - targetDelay) / tickDuration;
        //停止中なら次のパケットまで位置は変わらないので、次の生存確認が届くはずの時刻までは進んでよい
        //(それ以上進めると、動き出しのパケットが遅れたときに動き出しを飛ばしてしまう)
        double limitTick = newestIsIdle ? newestTick + PositionSyncSender.IdleKeepAliveTicks : newestTick;

        if (!hasRenderTick || Math.Abs(targetTick - renderTick) * tickDuration > ResyncThreshold)
        {
            //初回や、長く途切れた後は合わせ直す
            renderTick = Math.Min(targetTick, limitTick);
            hasRenderTick = true;
        }
        else if (targetTick > renderTick && IsStationaryUntil(Math.Min(targetTick, limitTick)))
        {
            //表示が止まっている間は、見た目を変えずに目標まで進める (動き出しで遅れを持ち越さない)
            renderTick = Math.Min(targetTick, limitTick);
        }
        else
        {
            //目標より遅れていれば少し速く、進んでいれば少し遅く再生する
            //小さなずれ(到着時刻がフレーム単位でしか分からないことによる揺れなど)には反応しない
            double error = targetTick - renderTick;
            double outside = Math.Max(0.0, Math.Abs(error) - RateDeadbandTicks) * Math.Sign(error);
            double rate = 1.0 + Math.Clamp(outside * RateGainPerTick, -MaxRateDeviation, MaxRateDeviation);
            double next = renderTick + deltaTime / tickDuration * rate;

            //最新の点に近づいたら減速し、追い越さない (届いていない区間を予測しない)
            //減速域の幅は揺らぎに合わせてあるので、揺らぎが大きいほど手前から緩やかに減速する
            double margin = limitTick - renderTick;
            if (margin < decelZoneTicks)
            {
                double allowed = Math.Max(0.0, margin) / decelZoneTicks;
                next = renderTick + (next - renderTick) * allowed;
            }
            if (next > limitTick) next = limitTick;
            if (next > renderTick) renderTick = next;
        }

        //再生し終えた点を捨てる(補間のために直前の1点は残す)
        int keepFrom = 0;
        while (keepFrom + 1 < samples.Count && samples[keepFrom + 1].Tick <= renderTick) keepFrom++;
        if (keepFrom > 0) samples.RemoveRange(0, keepFrom);

        position = Evaluate(renderTick);
        //物理ステップ後の位置も、届いている範囲を超えない
        aheadPosition = Evaluate(Math.Min(renderTick + lookAhead / tickDuration, limitTick));
        //向きは補間できないので、表示時刻の直前の点のものを使う (枝刈り後は先頭が直前の点)
        flipX = samples[0].Flip;
        return true;
    }

    NVector2 Evaluate(double tick)
    {
        var first = samples[0];
        if (tick <= first.Tick) return first.Position;

        for (int i = 0; i + 1 < samples.Count; i++)
        {
            var a = samples[i];
            var b = samples[i + 1];
            if (tick > b.Tick) continue;

            //epochをまたぐ区間は補間せず、次の点に達した時点で切り替える
            if (a.Epoch != b.Epoch) return a.Position;
            float t = (float)((tick - a.Tick) / (b.Tick - a.Tick));
            return NVector2.Lerp(a.Position, b.Position, t);
        }

        //最新の点より先は予測しない
        return samples[samples.Count - 1].Position;
    }
}
