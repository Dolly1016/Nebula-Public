using AmongUs.Data;
using AmongUs.Matchmaking;
using InnerNet;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine.Rendering;
using Virial.Text;

namespace Nebula.Online;

internal static class PublicRoomListingConverter
{
    private static readonly Dictionary<int, PublicRoomEntry> byGameId = [];

    public static bool TryGet(int gameId, out PublicRoomEntry room) => byGameId.TryGetValue(gameId, out room!);

    public static GameListing ToGameListing(PublicRoomEntry room)
    {
        int gameId = room.GameId;
        byGameId[gameId] = room;

        return new GameListing
        {
            IP = ToAddress(room.ServerIp),
            Port = (ushort)Mathf.Clamp(room.ServerPort, 0, ushort.MaxValue),
            GameId = gameId,
            HostName = room.HostName,
            TrueHostName = room.HostName,
            HostPlatformName = string.Empty,
            PlayerCount = (byte)Mathf.Clamp(room.NumPlayers, 0, byte.MaxValue),
            MaxPlayers = room.MaxPlayers,
            NumImpostors = 0,
            MapId = (byte)Mathf.Clamp(room.Map, 0, (int)MapNames.Fungle),
            Age = room.Elapsed,
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Platform = Platforms.Unknown,
            QuickChat = QuickChatModes.FreeChatOrQuickChat,
            Options = GameOptionsManager.Instance.GameSearchOptions,
        };
    }

    private static uint ToAddress(string ip)
    {
        try
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) return 0u;
            uint address = 0u;
            for (int i = 0; i < 4; i++) address |= (uint)byte.Parse(parts[i], CultureInfo.InvariantCulture) << (i * 8);
            return address;
        }
        catch
        {
            return 0u;
        }
    }
}
