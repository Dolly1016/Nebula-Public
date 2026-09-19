using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Nebula.Modules.Cosmetics;
using Virial.Events.VoiceChat;

namespace Nebula.Collab;

internal static class TBCLFields
{
    internal unsafe struct PlayerData
    {
        public byte PlayerId;

        public bool IsKiller;
        public bool IsImpostor;
        public bool IsCrewmate;
        public bool IsNeutral;
        public bool IsImpostorlike;

        public float SpeakerPositionX;
        public float SpeakerPositionY;

        public byte NameLength;
        public fixed char Name[32];
        public float ColorR, ColorG, ColorB;
    }

    internal struct Snapshot
    {
        public float LocalMicPositionX;
        public float LocalMicPositionY;
        public PlayerData[] Players;
    }

    internal class SnapshotEntry
    {
        public Snapshot Data;

        internal SnapshotEntry(Snapshot data)
        {
            this.Data = data;
        }
    }

    const int Version = 20260918;
    const int PlayersCapacity = 24;
    const int NameCapacity = 32;
    static public bool RequireUpdate = false;
    static public SnapshotEntry Data = null;

    static public Queue<SnapshotEntry> Saver = [];

    static private VVector2? GetLocalMicPosition(GamePlayer? localPlayer)
    {
        if (localPlayer == null) return null;

        var camTarget = AmongUsLLImpl.TryGetHudManager(out var hudManager) && hudManager.PlayerCam.AsBoolFast() ? AmongUsUtil.CurrentCamTarget : null;
        VVector2? position = camTarget.AsBoolFast() ? camTarget!.transform.position : localPlayer.Position;

        if (position.HasValue)
        {
            var ev = GameOperatorManager.Instance?.Run<FixMicPositionEvent>(new(localPlayer, position.Value, localPlayer.EyesightIgnoreWalls), true);
            position = ev?.Position ?? position;
        }

        return position;
    }

    static private unsafe void SetName(ref PlayerData data, string name)
    {
        int length = Math.Min(name.Length, NameCapacity);
        fixed (char* buffer = data.Name)
        {
            for (int i = 0; i < length; i++) buffer[i] = name[i];
        }
        data.NameLength = (byte)length;
    }

    static internal void Update()
    {
        if (!RequireUpdate) return;

        Snapshot snapshot = new();

        var localPlayer = GamePlayer.LocalPlayer;

        var micPosition = GetLocalMicPosition(localPlayer);
        snapshot.LocalMicPositionX = micPosition?.x ?? 0f;
        snapshot.LocalMicPositionY = micPosition?.y ?? 0f;

        var allPlayers = GamePlayer.AllOrderedPlayers;
        int length = Math.Min(allPlayers.Count, PlayersCapacity);
        var players = new PlayerData[length];

        for (int index = 0; index < length; index++)
        {
            var player = allPlayers[index];
            ref var data = ref players[index];

            data.PlayerId = player.PlayerId;

            if (player.Role != null)
            {
                data.IsKiller = player.IsKiller;
                data.IsImpostor = player.IsImpostor;
                data.IsCrewmate = player.IsCrewmate;
                data.IsNeutral = player.IsNeutral;
                data.IsImpostorlike = player.IsImpostorlike;
            }

            var speakerPosition = GameOperatorManager.Instance?.Run<FixSpeakerPositionEvent>(new(player, player.Position), true)?.Position ?? player.Position;
            data.SpeakerPositionX = speakerPosition.x;
            data.SpeakerPositionY = speakerPosition.y;

            var outfit = (AmongUsUtil.InMeeting ? player.DefaultOutfit : player.CurrentOutfit).outfit;

            SetName(ref data, outfit.PlayerName ?? "");

            int colorId = outfit.ColorId;
            if (colorId < 0 || colorId >= DynamicPalette.ColorsLength) colorId = 0;
            var color = DynamicPalette.PlayerColors[colorId];
            data.ColorR = color.R;
            data.ColorG = color.G;
            data.ColorB = color.B;
        }

        snapshot.Players = players;

        while (Saver.Count > 100) Saver.Dequeue();
        Data = new(snapshot);
        Saver.Enqueue(Data);
    }
}