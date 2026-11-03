using Nebula.Player.PositionSync;
using Virial;
using NVector2 = System.Numerics.Vector2;

namespace Nebula.Player;

/// <summary>
/// CustomNetworkTransformのストリーム同期(Serialize/Deserialize)に代わり、
/// 非Reliableなカスタムrpcでプレイヤー位置を同期する。
/// </summary>
/// <remarks>
/// 送信側: 2tickごとに直近の数点を送る。MinSendIntervalの影響を受けない。
/// 受信側: 送信側のtick時刻に沿って補間する。点の消化速度に上限がない。
/// 受信側は、そのプレイヤーからrpcが最近届いている間だけ新方式で動かし、それ以外はバニラの同期に任せる。
/// </remarks>
[NebulaRPCHolder]
internal static class PlayerPositionSync
{
    /// <summary>このtime以内にrpcが届いていれば、そのプレイヤーは新方式で動かす。</summary>
    const float ReceiverActiveTimeout = 1f;

    class ReceiverState
    {
        public PositionSyncReceiver Receiver = null!;
        public float LastReceivedAt = float.NegativeInfinity;
        public float LastUpdatedAt = float.NegativeInfinity;
    }

    static readonly Dictionary<byte, ReceiverState> receivers = new();
    static PositionSyncSender sender = new();
    static object? currentGame = null;
    static bool senderWasPaused = false;

    static float cachedActiveAt = float.NaN;
    static bool cachedActive = false;

    /// <summary>自身のpingを記録する間隔(秒)。</summary>
    const float PingSampleInterval = 0.1f;
    /// <summary>自身のpingの揺らぎを見積もる窓幅(秒)。</summary>
    const float PingJitterWindow = PositionSyncReceiver.OffsetWindow;
    static readonly JitterMonitor pingMonitor = new(PingJitterWindow, PositionSyncReceiver.JitterPercentile);
    static float lastPingSampledAt = float.NegativeInfinity;

    /// <summary>
    /// 自身のpingの揺らぎから見積もった片道の揺らぎ(秒)。
    /// pingは往復なので、片道ぶんとして半分にする。
    /// </summary>
    static internal float LocalOneWayJitter => pingMonitor.Jitter * 0.001f * 0.5f;

    static void SamplePing(float now)
    {
        if (now - lastPingSampledAt < PingSampleInterval) return;
        lastPingSampledAt = now;
        if (!AmongUsClient.Instance.AsBoolFast()) return;
        pingMonitor.Add(now, AmongUsClient.Instance.Ping);
    }

    /// <summary>
    /// 自分が新方式で送信するかどうか。
    /// </summary>
    static bool IsSenderActive(float now)
    {
        if (cachedActiveAt == now) return cachedActive;
        cachedActiveAt = now;
        cachedActive =
            GeneralConfigurations.RpcPlayerSyncOption &&
            NebulaGameManager.Instance != null &&
            AmongUsClient.Instance.AsBoolFast() && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            (AmongUsUtil.IsCustomServer() || AmongUsUtil.IsLocalServer());
        return cachedActive;
    }

    static void CheckGame()
    {
        var game = NebulaGameManager.Instance;
        if (ReferenceEquals(game, currentGame)) return;
        currentGame = game;
        receivers.Clear();
        pingMonitor.Clear();
        lastPingSampledAt = float.NegativeInfinity;
        sender = new();
        senderWasPaused = false;
    }

    static float TickDuration => FastMethods.GetFixedDeltaTimeFast();
    static int CurrentTick(float now) => (int)Math.Round(now / TickDuration);

    static NVector2 ToN(Vector2 v) => new(v.x, v.y);
    static Vector2 ToU(NVector2 v) => new(v.X, v.Y);

    record struct PositionSyncMessage(byte PlayerId, PositionSyncPacket Packet);

    static readonly RemoteProcess<PositionSyncMessage> RpcPosition = new("PlayerPositionSync",
        (writer, message) =>
        {
            writer.Write(message.PlayerId);
            writer.Write(message.Packet.Epoch);
            writer.Write(message.Packet.NewestTick);
            writer.Write((byte)message.Packet.Positions.Length);
            writer.Write(message.Packet.FlipFlags);
            writer.Write(message.Packet.Idle);
            foreach (var p in message.Packet.Positions)
            {
                writer.Write(p.X);
                writer.Write(p.Y);
            }
        },
        (reader) =>
        {
            byte playerId = reader.ReadByte();
            byte epoch = reader.ReadByte();
            int tick = reader.ReadInt32();
            int count = reader.ReadByte();
            byte flipFlags = reader.ReadByte();
            bool idle = reader.ReadBoolean();
            var positions = new NVector2[count];
            for (int i = 0; i < count; i++) positions[i] = new(reader.ReadSingle(), reader.ReadSingle());
            return new(playerId, new(epoch, tick, positions, flipFlags, idle));
        },
        (message, calledByMe) =>
        {
            if (calledByMe) return;
            OnReceive(message);
        }, false);

    static void OnReceive(PositionSyncMessage message)
    {
        CheckGame();
        if (currentGame == null) return;

        var player = NebulaGameManager.Instance?.GetPlayer(message.PlayerId)?.VanillaPlayer;
        if (!player.AsBoolFast(out var vanillaPlayer)) return;
        var netTransform = vanillaPlayer.NetTransform;
        if (!netTransform.AsBoolFast() || netTransform.AmOwner) return;

        float now = FastMethods.GetTimeFast();
        var state = GetReceiverState(message.PlayerId);
        state.LastReceivedAt = now;

        //停止中・無効化中の位置は受け取らない(バニラも同様に捨てている)
        if (netTransform.isPaused || !netTransform.isActiveAndEnabled) return;
        state.Receiver.OnPacket(message.Packet, now);
    }

    static ReceiverState GetReceiverState(byte playerId)
    {
        if (!receivers.TryGetValue(playerId, out var state))
        {
            state = new() { Receiver = new(TickDuration) };
            receivers[playerId] = state;
        }
        return state;
    }

    static bool TryGetActiveReceiver(CustomNetworkTransform netTransform, float now, out ReceiverState state)
    {
        state = null!;
        if (currentGame == null) return false;
        var player = netTransform.myPlayer;
        if (!player.AsBoolFast()) return false;
        if (!receivers.TryGetValue(player.PlayerId, out state!)) return false;
        return now - state.LastReceivedAt < ReceiverActiveTimeout;
    }

    /// <summary>
    /// CustomNetworkTransform.FixedUpdateの前に呼ばれる。
    /// </summary>
    /// <returns>バニラのFixedUpdateを実行するならtrue</returns>
    static internal bool OnFixedUpdate(CustomNetworkTransform netTransform)
    {
        CheckGame();
        float now = FastMethods.GetTimeFast();

        if (netTransform.AmOwner)
        {
            if (!IsSenderActive(now) || !netTransform.myPlayer.AsBoolFast()) return true;

            if (netTransform.isPaused)
            {
                senderWasPaused = true;
                return true;
            }
            if (senderWasPaused)
            {
                //ポーズ中に動かされた位置とは連続しない
                senderWasPaused = false;
                sender.MarkDiscontinuity();
            }

            if (sender.Tick(CurrentTick(now), ToN(netTransform.body.position), netTransform.myPlayer.cosmetics.FlipX, out var packet))
                RpcPosition.Invoke(new(netTransform.myPlayer.PlayerId, packet));

            //バニラの送信キューには積ませない
            return false;
        }
        else
        {
            if (!TryGetActiveReceiver(netTransform, now, out var state)) return true;

            float dt = TickDuration;
            float elapsed = Math.Clamp(now - state.LastUpdatedAt, 0f, 0.1f);
            state.LastUpdatedAt = now;

            //自身の回線の揺らぎも、減速と目標遅延の見積もりに含める
            SamplePing(now);
            state.Receiver.SetExternalJitter(LocalOneWayJitter);

            //バニラ側のキューは使わない。空にしておけば、バニラのFixedUpdateはtempSnapPositionの処理だけを行って戻る。
            if (netTransform.incomingPosQueue.Count > 0) netTransform.incomingPosQueue.Clear();

            if (netTransform.isPaused)
            {
                state.Receiver.ClearSamples();
                return true;
            }

            bool hasPosition = state.Receiver.TryGetPosition(now, elapsed, dt, out var current, out var ahead, out var flipX);
            //ベントに入る演出中はバニラのコルーチンが体を動かすので触らない
            if (netTransform.IsInMiddleOfAnimationThatMakesPlayerInvisible()) return true;

            if (!hasPosition)
            {
                //点が無いときに速度が残っていると滑っていってしまう
                netTransform.body.velocity = Vector2.zero;
            }
            else
            {
                var body = netTransform.body;
                //アニメーションは速度で決まるので、大きくずれていない限り位置は直接動かさず速度で追従させる。
                var velocity = PositionSyncMotion.Compute(ToN(body.position), current, ahead, dt, out var teleportTo);
                if (teleportTo.HasValue) body.position = ToU(teleportTo.Value);
                body.velocity = ToU(velocity);
                ApplyFlipX(netTransform.myPlayer, velocity, flipX);
            }
            return true;
        }
    }

    /// <summary>
    /// 受け取った向きを反映する。
    /// 動いている間はバニラ(PlayerPhysics.HandleAnimation)が速度から向きを決めるので、そちらが向きを変えない場合のみ反映する。
    /// </summary>
    static void ApplyFlipX(PlayerControl player, NVector2 velocity, bool flipX)
    {
        var modInfo = player.GetModInfo()?.Unbox();
        if (modInfo == null) return;
        bool vanillaDecidesFlip = modInfo.IsDead ? MathF.Abs(velocity.X) > 0.01f : velocity.LengthSquared() >= 0.05f;
        if (vanillaDecidesFlip) return;
        modInfo.ApplySyncedFlipX(flipX);
    }

    /// <summary>
    /// 新方式で送信している間は、バニラのストリーム同期で位置を送らない。
    /// </summary>
    static internal bool ShouldBlockSerialize(CustomNetworkTransform netTransform) => netTransform.AmOwner && IsSenderActive(FastMethods.GetTimeFast());

    /// <summary>
    /// 新方式で受信している間は、バニラのストリーム同期で届いた位置を使わない。
    /// </summary>
    static internal bool ShouldBlockDeserialize(CustomNetworkTransform netTransform) => !netTransform.AmOwner && TryGetActiveReceiver(netTransform, FastMethods.GetTimeFast(), out _);

    static internal void OnSnapped(CustomNetworkTransform netTransform)
    {
        CheckGame();
        if (netTransform.AmOwner) sender.MarkDiscontinuity();
        else if (netTransform.myPlayer.AsBoolFast() && receivers.TryGetValue(netTransform.myPlayer.PlayerId, out var state)) state.Receiver.OnLocalSnap(FastMethods.GetTimeFast());
    }

    static internal void OnClearPositionQueues(CustomNetworkTransform netTransform)
    {
        if (netTransform.AmOwner) return;
        if (netTransform.myPlayer.AsBoolFast() && receivers.TryGetValue(netTransform.myPlayer.PlayerId, out var state)) state.Receiver.ClearSamples();
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Serialize))]
static class PlayerPositionSyncSerializePatch
{
    static bool Prefix(CustomNetworkTransform __instance, [HarmonyArgument(1)] bool initialState, ref bool __result)
    {
        if (initialState || !PlayerPositionSync.ShouldBlockSerialize(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Deserialize))]
static class PlayerPositionSyncDeserializePatch
{
    static bool Prefix(CustomNetworkTransform __instance, [HarmonyArgument(1)] bool initialState)
    {
        if (initialState) return true;
        return !PlayerPositionSync.ShouldBlockDeserialize(__instance);
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), [typeof(Vector2), typeof(ushort)])]
static class PlayerPositionSyncSnapToPatch
{
    static void Postfix(CustomNetworkTransform __instance, [HarmonyArgument(1)] ushort minSid)
    {
        //実際にスナップされた場合のみ (バニラはSequenceIdが古い場合や、ベントの演出中は何もしない)
        if (__instance.lastSequenceId != minSid) return;
        PlayerPositionSync.OnSnapped(__instance);
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.ClearPositionQueues))]
static class PlayerPositionSyncClearQueuesPatch
{
    static void Postfix(CustomNetworkTransform __instance) => PlayerPositionSync.OnClearPositionQueues(__instance);
}
