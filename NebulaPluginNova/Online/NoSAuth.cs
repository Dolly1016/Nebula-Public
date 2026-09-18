using InnerNet;
using Nebula.Patches;

namespace Nebula.Online;

internal readonly struct NebulaAuthEntry
{
    public NebulaAuthEntry(int clientId, string? uid, AuthStatus status, int experience)
    {
        ClientId = clientId;
        Uid = uid;
        Status = status;
        Experience = experience;
    }

    public int ClientId { get; }
    public string? Uid { get; }
    public AuthStatus Status { get; }
    public int Experience { get; }

    public bool IsVerified => Status == AuthStatus.Verified;
}

internal static class NoSAuth
{
    // キーはClientId
    private static readonly Dictionary<int, NebulaAuthEntry> Results = new();

    public static bool IsAuthRequired { get; private set; }

    public static bool IsPolicyKnown { get; private set; }

    // 認証情報のキャッシュを削除する。
    public static void Reset()
    {
        Results.Clear();
        IsAuthRequired = false;
        IsPolicyKnown = false;
    }

    public static NebulaAuthEntry Get(int clientId)
    {
        return Results.TryGetValue(clientId, out var entry) ? entry : new NebulaAuthEntry(clientId, null, AuthStatus.Unknown, -1);
    }

    public static NebulaAuthEntry Get(NetworkedPlayerInfo info) => Get(info.ClientId);

    public static NebulaAuthEntry Get(PlayerControl player)
    {
        var info = player.Data;
        return info.AsBoolFast() ? Get(info.ClientId) : new NebulaAuthEntry(0, null, AuthStatus.Unknown, -1);
    }

    public static NebulaAuthEntry LocalEntry
    {
        get
        {
            var clientId = LocalClientId;
            return clientId.HasValue ? Get(clientId.Value) : new NebulaAuthEntry(0, null, AuthStatus.Unknown, -1);
        }
    }

    private static int? LocalClientId
    {
        get
        {
            if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return null;
            var info = localPlayer.Data;
            return info.AsBoolFast() ? info.ClientId : null;
        }
    }

    // 部屋を認証必須にする。ホストのみ使用可能。
    public static void SetAuthRequired(bool required)
    {
        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return;
        if (!client.AmHost) return;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return;

        var writer = client.StartRpcImmediately(localPlayer.NetId, NebulaAuthProtocol.AuthCallId, Hazel.SendOption.Reliable, -1);
        writer.Write(NebulaAuthProtocol.Sub.SetPolicy);
        writer.Write(required);
        client.FinishRpcImmediately(writer);
    }

    // 認証結果情報を要求する。特に指定しない限り全員分取り寄せる。
    public static void RequestResults(int clientId = NebulaAuthProtocol.AllClients)
    {
        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return;

        var writer = client.StartRpcImmediately(
            localPlayer.NetId, NebulaAuthProtocol.AuthCallId, Hazel.SendOption.Reliable, -1);
        writer.Write(NebulaAuthProtocol.Sub.Request);
        writer.WritePacked(clientId);
        client.FinishRpcImmediately(writer);
    }

    private static void AnswerChallenge(string challengeBase64, string gameCode)
    {
        var identity = NoSIdentity.Instance;
        if (identity == null || !identity.IsRegistered) return;

        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return;

        // 部屋コードが一致しない場合、答えないでおく
        var actualCode = GameCode.IntToGameName(client.GameId);
        if (string.IsNullOrEmpty(actualCode) || actualCode != gameCode) return;

        byte[] challenge;
        try
        {
            challenge = Convert.FromBase64String(challengeBase64);
        }
        catch
        {
            return;
        }

        var proof = identity.CreateAuthProof(gameCode, challenge);
        if (proof == null) return;

        var writer = client.StartRpcImmediately(
            localPlayer.NetId, NebulaAuthProtocol.AuthCallId, Hazel.SendOption.Reliable, -1);
        writer.Write(NebulaAuthProtocol.Sub.Response);
        writer.Write(identity.UId);
        writer.Write(proof.Value.IssuedAt);
        writer.Write(proof.Value.Signature);
        client.FinishRpcImmediately(writer);
    }

    // 129RPCの受け取り
    public static void ReceiveAuth(Hazel.MessageReader reader)
    {
        var sub = reader.ReadByte();

        if (sub == NebulaAuthProtocol.Sub.Challenge)
        {
            var challenge = reader.ReadString();
            var gameCode = reader.ReadString();
            AnswerChallenge(challenge, gameCode);
            return;
        }
        else if (sub == NebulaAuthProtocol.Sub.Policy)
        {
            var required = reader.ReadBoolean();
            IsAuthRequired = required;
            IsPolicyKnown = true;
        }
    }

    // 認証結果の受け取り
    public static void ReceiveResult(Hazel.MessageReader reader)
    {
        var clientId = reader.ReadPackedInt32();
        var uid = reader.ReadString();
        var status = (AuthStatus)reader.ReadByte();
        var experience = reader.ReadPackedInt32();

        var entry = new NebulaAuthEntry(clientId, uid, status, experience);
        Results[clientId] = entry;

        // uid が確定したので、名前の突き合わせ待ちに積む
        PlayerNameCheck.Register(clientId);
    }
}
