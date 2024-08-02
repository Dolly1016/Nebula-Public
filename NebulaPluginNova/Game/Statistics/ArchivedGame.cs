using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;
using Virial.Utilities;

namespace Nebula.Game.Statistics;

[Flags]
internal enum PlayerTrackingFlags : byte
{
    IsDead = 0x01,
    InVent = 0x02,
    IsInvisible = 0x04,
}

/// <summary>
/// 
/// </summary>
/// <param name="Position"></param>
/// <param name="States"></param>
internal record ArchivedPlayerMoment(Vector2 Position, PlayerTrackingFlags States)
{
    bool HasState(PlayerTrackingFlags flag) => (States & flag) != 0;
}

/// <summary>
/// アーカイブされた瞬間の切り出し
/// </summary>
internal class ArchivedMoment
{
    public ArchivedPlayerMoment[] PlayerData { get; init; }
    public float Time { get; init; }

    public ArchivedMoment() { }

    public static ArchivedMoment CaptureCurrent()
    {
        var trackingDataArray = new ArchivedPlayerMoment[NebulaGameManager.Instance!.AllPlayersNum];
        foreach (var p in NebulaGameManager.Instance.AllPlayerInfo())
        {
            PlayerTrackingFlags flag = 0;
            if (p.IsDead) flag |= PlayerTrackingFlags.IsDead;
            if (p.VanillaPlayer.inVent) flag |= PlayerTrackingFlags.InVent;
            if (p.Unbox().VisibilityLevel > 0) flag |= PlayerTrackingFlags.IsInvisible;
            trackingDataArray[p.PlayerId] = new(p.VanillaPlayer.transform.position, flag);
        }

        ArchivedMoment tracked = new() { PlayerData = trackingDataArray, Time = NebulaGameManager.Instance.CurrentTime };

        return tracked;
    }
}

internal record ArchivedAssignmentHistory(float Time, RoleType Type, string AssignableId, byte PlayerId, int[] Arguments, bool SetAction = true)
{
}

/// <summary>
/// アーカイブされたタスクフェイズ情報
/// </summary>
internal class ArchivedTaskPhase
{
    public bool IsClosed { get; set; } = false;
    public List<ArchivedMoment> Moments { get; init; } = new();
    public float Start { get; set; } = 0f;
    public float End { get; set; } = float.MaxValue;
    public void CaptureCurrent()
    {
        Moments.Add(ArchivedMoment.CaptureCurrent());
    }
}


/// <summary>
/// アーカイブされたゲーム内イベント
/// </summary>
/// <param name="Moment"></param>
/// <param name="ImageType"></param>
/// <param name="TranslationKey"></param>
/// <param name="LeftMask"></param>
/// <param name="RightMask"></param>
internal record ArchivedEvent(ArchivedMoment Moment, string ImageType, string TranslationKey, int LeftMask, int RightMask)
{
}

/// <summary>
/// アーカイブされたプレイヤー情報
/// </summary>
/// <param name="Name">プレイヤー名</param>
/// <param name="Id">ゲーム内ID</param>
/// <param name="MainColor">プレイヤー色</param>
/// <param name="ShadowColor">影の色</param>
/// <param name="VisorColor">バイザーの色</param>
/// <param name="HatId">ハットID</param>
/// <param name="VisorId">バイザーID</param>
/// <param name="SkinId">スキンID</param>
internal record ArchivedPlayer(string Name, byte Id, Color32 MainColor, Color32 ShadowColor, Color32 VisorColor, string HatId, string VisorId, string SkinId)
{
    public static ArchivedPlayer FromPlayer(GamePlayer player)
    {
        var outfit = player.DefaultOutfit.outfit;
        byte id = player.PlayerId;
        return new ArchivedPlayer(player.Name, id, Palette.PlayerColors[id], Palette.ShadowColors[id], DynamicPalette.VisorColors[id], outfit.HatId, outfit.VisorId, outfit.SkinId);
    }

    public void ReflectTo(PoolablePlayer player, PlayerMaterial.MaskType maskType)
    {
        player.cosmetics.SetMaskType(maskType);
        Palette.PlayerColors[NebulaPlayerTab.ArchiveColorId] = MainColor;
        Palette.ShadowColors[NebulaPlayerTab.ArchiveColorId] = ShadowColor;

        player.cosmetics.SetBodyColor(NebulaPlayerTab.ArchiveColorId);
        player.cosmetics.SetSkin(SkinId, NebulaPlayerTab.ArchiveColorId, null);
        player.cosmetics.SetHatColor(Palette.White);
        player.cosmetics.SetVisorAlpha(1f);
        player.cosmetics.SetHat(HatId, NebulaPlayerTab.ArchiveColorId);
        player.cosmetics.SetVisor(VisorId, NebulaPlayerTab.ArchiveColorId);
        player.cosmetics.visor.Image.sharedMaterial.SetColor(PlayerMaterial.VisorColor, VisorColor);
        player.cosmetics.hat.FrontLayer.sharedMaterial.SetColor(PlayerMaterial.VisorColor, VisorColor);
        player.cosmetics.hat.BackLayer.sharedMaterial.SetColor(PlayerMaterial.VisorColor, VisorColor);
        player.cosmetics.skin.layer.sharedMaterial.SetColor(PlayerMaterial.VisorColor, VisorColor);
        player.cosmetics.SetEnabledColorblind(false);

        player.SetName(Name);
    }

    public void ReflectTo(SpriteRenderer renderer)
    {
        Palette.PlayerColors[NebulaPlayerTab.ArchiveColorId] = MainColor;
        Palette.ShadowColors[NebulaPlayerTab.ArchiveColorId] = ShadowColor;

        PlayerMaterial.SetColors(NebulaPlayerTab.ArchiveColorId, renderer);
        renderer.sharedMaterial.SetColor(PlayerMaterial.VisorColor, VisorColor);
    }
}

/// <summary>
/// アーカイブされたゲーム
/// </summary>
internal class ArchivedGame
{
    public byte MapId;
    public ArchivedTaskPhase[] TaskPhases;
    public ArchivedEvent[] Events;
    public ArchivedPlayer[] Players;
    public ArchivedAssignmentHistory[] AssignmentHistory;

    public byte[] Serialize()
    {
        SerializedDataWriter writer = new();
        writer.Write((byte)1);

        void WriteMoment(ArchivedMoment m)
        {
            writer.Write(m.Time);
            foreach (var p in m.PlayerData)
            {
                writer.Write(p.Position.x);
                writer.Write(p.Position.y);
                writer.Write((byte)p.States);
            }
        }

        writer.Write(MapId);

        writer.Write(Players.Length);
        foreach (var p in Players)
        {
            writer.Write(p.Name);
            writer.Write(p.MainColor);
            writer.Write(p.ShadowColor);
            writer.Write(p.VisorColor);
            writer.Write(p.HatId);
            writer.Write(p.VisorId);
            writer.Write(p.SkinId);
        }

        writer.Write(AssignmentHistory.Length);
        foreach(var a in AssignmentHistory)
        {
            writer.Write(a.Time);
            writer.Write(a.PlayerId);
            writer.Write((byte)a.Type);
            writer.Write(a.AssignableId);
            writer.Write(a.Arguments.Length);
            foreach (var arg in a.Arguments) writer.Write(arg);
        }

        writer.Write(TaskPhases.Length);
        foreach (var t in TaskPhases)
        {
            writer.Write(t.Start);
            writer.Write(t.End);
            writer.Write(t.Moments.Count);
            foreach (var m in t.Moments) WriteMoment(m);
        }

        writer.Write(Events.Length);
        foreach (var e in Events)
        {
            WriteMoment(e.Moment);
            writer.Write(e.ImageType);
            writer.Write(e.TranslationKey);
            writer.Write(e.LeftMask);
            writer.Write(e.RightMask);
        }
        return writer.ToData();
    }

    public static ArchivedGame? Deserialize(Stream stream)
    {
        SerializedDataReader reader = new(stream);

        int version = reader.ReadByte();

        if (version == 1)
            return DeserializeV1(reader);

        return null;
    }

    private static ArchivedMoment DeserializeMomentV1(SerializedDataReader reader, int players)
    {
        float time = reader.ReadSingle();
        ArchivedPlayerMoment[] trackedPlayers = new ArchivedPlayerMoment[players];
        for (int p = 0; p < trackedPlayers.Length; p++)
            trackedPlayers[p] = new(new(reader.ReadSingle(), reader.ReadSingle()), (PlayerTrackingFlags)reader.ReadByte());
        return new() { Time = time, PlayerData = trackedPlayers };
    }

    private static ArchivedGame DeserializeV1(SerializedDataReader reader)
    {
        byte mapId = reader.ReadByte();

        ArchivedPlayer[] players = new ArchivedPlayer[reader.ReadInt32()];
        for (int p = 0; p < players.Length; p++)
        {
            players[p] = new(reader.ReadString(), (byte)p, reader.ReadColor32(), reader.ReadColor32(), reader.ReadColor32(), reader.ReadString(), reader.ReadString(), reader.ReadString());
        }

        ArchivedAssignmentHistory[] assignmentHistory = new ArchivedAssignmentHistory[reader.ReadInt32()];
        for (int a = 0; a < assignmentHistory.Length; a++)
        {
            float time = reader.ReadSingle();
            byte playerId =reader.ReadByte();
            RoleType type = (RoleType)reader.ReadByte();
            string assignableId = reader.ReadString();
            int[] arguments = new int[reader.ReadInt32()];
            for(int i = 0;i<arguments.Length;i++) arguments[i] = reader.ReadInt32();

            assignmentHistory[a] = new(time, type, assignableId, playerId, arguments);
        }

        ArchivedTaskPhase[] taskPhases = new ArchivedTaskPhase[reader.ReadInt32()];
        for (int t = 0; t < taskPhases.Length; t++)
        {
            float start = reader.ReadSingle();
            float end = reader.ReadSingle();
            ArchivedMoment[] moments = new ArchivedMoment[reader.ReadInt32()];
            for (int m = 0; m < moments.Length; m++)
                moments[m] = DeserializeMomentV1(reader, players.Length);
            taskPhases[t] = new() { Start = start, End = end, Moments = new(moments), IsClosed = true };
        }

        ArchivedEvent[] events = new ArchivedEvent[reader.ReadInt32()];
        for (int e = 0; e < events.Length; e++)
        {
            events[e] = new(DeserializeMomentV1(reader, players.Length), reader.ReadString(), reader.ReadString(), reader.ReadInt32(), reader.ReadInt32());
        }

        return new() { Players = players, TaskPhases = taskPhases, Events = events };
    }
}

/// <summary>
/// イベントのアイコン画像を数値ID(ランタイム用)・テキストID(保存用)と紐づけるオブジェクト
/// </summary>
[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class TrackedEventImage
{
    public string TextId { get; private init; }
    public int Id { get; private set; } = -1;
    public Image Image { get; private init; }

    public TrackedEventImage(string id, Image image)
    {
        TextId = id;
        Image = image;
        AllImages.Add(this);
        ImagesDic[id] = this;
    }

    static public List<TrackedEventImage> AllImages = new();
    static public Dictionary<string, TrackedEventImage> ImagesDic = new();

    static void Preproces(NebulaPreprocessor preprocessor)
    {
        int num = 0;
        foreach (var i in AllImages.OrderBy(i => i.TextId))
        {
            i.Id = num;
            num++;
        }
        AllImages.Sort((i1, i2) => i2.Id - i1.Id);
    }
}