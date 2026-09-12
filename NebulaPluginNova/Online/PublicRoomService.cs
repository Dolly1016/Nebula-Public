using Nebula.Game;
using Virial.Events.Lobby;

namespace Nebula.Online;


internal static class PublicRoomService
{
    // 掲載の拒否理由
    internal enum PublishRefusal
    {
        // 掲載成功。
        None,
        // ロビーとの接続なし。
        NotConnected,
        // 非ホストプレイヤーからの要求のため拒絶。
        NotHost,
        // 部屋の認証状態不正。
        AuthNotRequired,
        // ホストの認証状態不正。
        NotVerified,
    }

    // タグ数上限
    public const int MaxTags = 16;


    public static PublishRefusal Publish(string title, uint lang, IEnumerable<string>? tags = null)
    {
        var refusal = CheckPublishable();
        if (refusal != PublishRefusal.None) return refusal;

        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return PublishRefusal.NotConnected;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return PublishRefusal.NotConnected;

        var tagList = tags?.Take(MaxTags).ToArray() ?? Array.Empty<string>();

        var writer = client.StartRpcImmediately(
            localPlayer.NetId, NebulaAuthProtocol.RoomCallId, Hazel.SendOption.Reliable, -1);
        writer.Write(NebulaAuthProtocol.RoomSub.Publish);
        writer.Write(title ?? "");
        writer.WritePacked(lang);
        writer.WritePacked(NebulaPlugin.PluginEpoch);
        writer.WritePacked(NebulaPlugin.PluginBuildNum);
        writer.WritePacked(Modules.NebulaAddon.AddonHandshakeHash);
        writer.WritePacked(tagList.Length);
        foreach (var tag in tagList) writer.Write(tag ?? "");
        client.FinishRpcImmediately(writer);
        return PublishRefusal.None;
    }

    public static PublishRefusal CheckPublishable()
    {
        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return PublishRefusal.NotConnected;
        if (!client.AmHost) return PublishRefusal.NotHost;

        if (NoSAuth.LocalEntry.IsVerified) return PublishRefusal.None;
        return NoSAuth.IsAuthRequired ? PublishRefusal.NotVerified : PublishRefusal.AuthNotRequired;
    }

    public static void Unpublish()
    {
        if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var client)) return;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return;

        var writer = client.StartRpcImmediately(
            localPlayer.NetId, NebulaAuthProtocol.RoomCallId, Hazel.SendOption.Reliable, -1);
        writer.Write(NebulaAuthProtocol.RoomSub.Unpublish);
        client.FinishRpcImmediately(writer);
    }

    public static void Receive(Hazel.MessageReader reader)
    {
        var sub = reader.ReadByte();
        if (sub != NebulaAuthProtocol.RoomSub.Status) return;

        // どの依頼への応答かはサーバーが添えてくる
        var requestSub = reader.ReadByte();
        var code = reader.ReadPackedInt32();

        // 0 以外は拒否。可視性は変わっていない
        if (code != 0) return;

        if (requestSub == NebulaAuthProtocol.RoomSub.Publish)
        {
            GameOperatorManager.Instance?.Run(new LobbyChangeToPublicHostEvent());
            AmongUsClient.Instance.ChangeGamePublic(true);
        }
        else if (requestSub == NebulaAuthProtocol.RoomSub.Unpublish)
        {
            GameOperatorManager.Instance?.Run(new LobbyChangeToPrivateHostEvent());
            AmongUsClient.Instance.ChangeGamePublic(false);
        }
    }
}
