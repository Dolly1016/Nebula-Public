using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Impostor;
using Nebula.VoiceChat;
using Virial.Events.VoiceChat;

namespace Nebula.Collab;

internal unsafe static class TBCLFields
{
    internal struct PlayerData
    {
        public byte PlayerId;

        public bool IsKiller;
        public bool IsImpostor;
        public bool IsCrewmate;
        public bool IsNeutral;
        public bool IsImpostorlike;
        public bool IsJammed;

        public float SpeakerPositionX;
        public float SpeakerPositionY;

        public float BodyRateX;
        public float BodyRateY;

        //Among UsのPlayerBodyTypesの値
        public int BodyType;
        //ろくろ首の長さ。伸ばしていない間は0。
        public float NeckLength;

        public byte NameLength;
        public fixed char Name[32];
        public float ColorR, ColorG, ColorB;

        public CostumeData Skin;
        public CostumeData Hat;
        public CostumeData Visor;
    }

    internal struct CostumeData
    {
        public byte NameLength;
        public fixed char Name[128];
    }

    internal struct RadioData
    {
        public RadioKind Kind;

        //このラジオで声の届くプレイヤーのPlayerIdマスク
        public int HearableMask;

        public byte NameLength;
        public fixed char Name[32];
    }

    internal struct Snapshot
    {
        public float LocalMicPositionX;
        public float LocalMicPositionY;

        public int PlayersLength;
        public PlayerData* Players;

        public int RadiosLength;
        public RadioData* Radios;
    }

    const int Version = 20261009;
    const int SnapshotCapacity = 64;
    const int PlayersCapacity = 24;
    const int RadiosCapacity = 8;
    const int NameCapacity = 32;
    const int CostumeNameCapacity = 128;

    static private Snapshot* snapshots = null;
    static private int nextIndex = 0;

    static public Snapshot* Latest = null;

    static public void Initialize()
    {
        if (snapshots != null) return;

        snapshots = (Snapshot*)NativeMemory.AllocZeroed(SnapshotCapacity, (nuint)sizeof(Snapshot));

        var players = (PlayerData*)NativeMemory.AllocZeroed(SnapshotCapacity * PlayersCapacity, (nuint)sizeof(PlayerData));
        for (int i = 0; i < SnapshotCapacity; i++) snapshots[i].Players = players + i * PlayersCapacity;

        var radios = (RadioData*)NativeMemory.AllocZeroed(SnapshotCapacity * RadiosCapacity, (nuint)sizeof(RadioData));
        for (int i = 0; i < SnapshotCapacity; i++) snapshots[i].Radios = radios + i * RadiosCapacity;

        nextIndex = 0;
        Latest = null;
    }

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

    static private void SetName(ref PlayerData data, string name)
    {
        fixed (char* buffer = data.Name) data.NameLength = SetText(buffer, NameCapacity, name);
    }

    static private void SetName(ref RadioData data, string name)
    {
        fixed (char* buffer = data.Name) data.NameLength = SetText(buffer, NameCapacity, name);
    }

    static private void SetName(ref CostumeData data, string? name)
    {
        fixed (char* buffer = data.Name) data.NameLength = SetText(buffer, CostumeNameCapacity, name);
    }

    //容量は255以下であること。書き込んだ文字数を返す。
    static private byte SetText(char* buffer, int capacity, string? text)
    {
        if (text == null) return 0;

        int length = Math.Min(text.Length, capacity);
        for (int i = 0; i < length; i++) buffer[i] = text[i];
        return (byte)length;
    }

    static private int GetBodyType(GamePlayer player)
    {
        var cosmetics = player.VanillaCosmetics;
        if (!cosmetics.AsBoolFast() || cosmetics.currentBodySprite == null) return (int)PlayerBodyTypes.Normal;
        return (int)cosmetics.CurrentBodyType;
    }

    static private void SetCostumes(ref PlayerData data, NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        SetName(ref data.Skin, outfit.SkinId);
        SetName(ref data.Hat, outfit.HatId);
        SetName(ref data.Visor, outfit.VisorId);
    }

    static private int UpdateRadios(RadioData* radios)
    {
        var manager = ModSingleton<RadioManager>.Instance;
        if (manager == null) return 0;

        int length = 0;
        foreach (var radio in manager.AllRadios)
        {
            if (length >= RadiosCapacity) break;
            if (radio.IsDead) continue;

            ref var data = ref radios[length];
            data.Kind = radio.Kind;
            data.HearableMask = radio.GetHearableMask();
            SetName(ref data, radio.LocalizedName);

            length++;
        }

        return length;
    }

    static internal void Update()
    {
        Initialize();

        var snapshot = snapshots + nextIndex;
        nextIndex = (nextIndex + 1) % SnapshotCapacity;

        var localPlayer = GamePlayer.LocalPlayer;

        var micPosition = GetLocalMicPosition(localPlayer);
        snapshot->LocalMicPositionX = micPosition?.x ?? 0f;
        snapshot->LocalMicPositionY = micPosition?.y ?? 0f;

        var allPlayers = GamePlayer.AllOrderedPlayers;
        int length = Math.Min(allPlayers.Count, PlayersCapacity);
        var players = snapshot->Players;

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

            data.IsJammed = AmongUsUtil.InMeeting && MeetingHudExtension.IsJammed(player.PlayerId);

            var speakerPosition = GameOperatorManager.Instance?.Run<FixSpeakerPositionEvent>(new(player, player.Position), true)?.Position ?? player.Position;
            data.SpeakerPositionX = speakerPosition.x;
            data.SpeakerPositionY = speakerPosition.y;

            var bodyRate = (player as PlayerModInfo)?.PlayerScaler is { } scaler && scaler.AsBoolFast() ? scaler.localScale : Vector3.one;
            data.BodyRateX = bodyRate.x;
            data.BodyRateY = bodyRate.y;

            data.BodyType = GetBodyType(player);
            data.NeckLength = player.TryGetAbility<Rokurokubi.Ability>(out var rokurokubi) ? rokurokubi.NeckLength : 0f;

            var outfit = (AmongUsUtil.InMeeting ? player.DefaultOutfit : player.CurrentOutfit).outfit;

            SetName(ref data, outfit.PlayerName ?? "");
            SetCostumes(ref data, outfit);

            int colorId = outfit.ColorId;
            if (colorId < 0 || colorId >= DynamicPalette.ColorsLength) colorId = 0;
            var color = DynamicPalette.PlayerColors[colorId];
            data.ColorR = color.R;
            data.ColorG = color.G;
            data.ColorB = color.B;
        }

        snapshot->PlayersLength = length;

        snapshot->RadiosLength = UpdateRadios(snapshot->Radios);

        //全ての書き込みを終えてから公開する
        Latest = snapshot;
    }
}