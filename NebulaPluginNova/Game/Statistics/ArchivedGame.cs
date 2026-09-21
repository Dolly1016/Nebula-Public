using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Game;

namespace Nebula.Game.Statistics;

internal class ArchivedPlayerImpl : IArchivedPlayer
{
    private byte playerId;
    private OutfitDefinition outfit;
    OutfitDefinition IArchivedPlayer.DefaultOutfit => outfit;

    byte IArchivedPlayer.PlayerId => playerId;
    public ArchivedPlayerImpl(GamePlayer player)
    {
        playerId = player.PlayerId;
        outfit = player.DefaultOutfit;
    }
}

internal class ArchivedGameImpl : IArchivedGame
{
    private IReadOnlyList<RoleHistory> roleHistory;
    private IArchivedEvent[] events;
    private Dictionary<byte, IArchivedPlayer> players;
    private byte mapId;
    private ArchivedColor[] palette;
    private DateTime? startedAtUtc;
    private DateTime? endedAtUtc;
    private ArchivedGameEnd? endInfo;
    private ArchivedPlayerResult[] playerResults;
    private ArchivedMovementPhase[] movementPhases;
    private ArchivedGameEventRecord[] eventRecords;
    IReadOnlyList<RoleHistory> IArchivedGame.RoleHistory => roleHistory;

    IArchivedEvent[] IArchivedGame.ArchivedEvents => events;

    byte IArchivedGameData.MapId => mapId;

    DateTime? IArchivedGameData.StartedAtUtc => startedAtUtc;

    DateTime? IArchivedGameData.EndedAtUtc => endedAtUtc;

    ArchivedGameEnd? IArchivedGameData.EndInfo => endInfo;

    IReadOnlyList<ArchivedPlayerResult> IArchivedGameData.PlayerResults => playerResults;

    IReadOnlyList<ArchivedMovementPhase> IArchivedGameData.MovementPhases => movementPhases;

    IReadOnlyList<ArchivedGameEventRecord> IArchivedGameData.Events => eventRecords;

    IEnumerable<IArchivedPlayer> IArchivedGame.GetAllPlayers() => players.Values;

    IArchivedPlayer? IArchivedGame.GetPlayer(byte playerId) => players.TryGetValue(playerId, out var p) ? p : null;

    private ArchivedGameImpl(NebulaGameManager game)
    {
        players = [];
        game.AllPlayerInfo.Do(p => players[p.PlayerId] = new ArchivedPlayerImpl(p));
        playerResults = game.AllPlayerInfo.OrderBy(p => p.PlayerId)
            .Select(p => ArchivedPlayerResult.FromPlayer(p, ColorOf(p), ResultText.RoleText(game.RoleHistory, p.PlayerId), ResultText.TaskText(p), ResultText.MoreInformation(p)))
            .ToArray();
        events = game.GameStatistics.Sealed;
        eventRecords = [.. events.Select(ArchivedGameEventRecord.FromEvent)];
        movementPhases = [.. ModSingleton<MovementRecorder>.Instance?.Phases ?? []];
        roleHistory = game.RoleHistory;
        mapId = NebulaAPI.AmongUs.MapId;
        startedAtUtc = game.StartedAtUtc;
        endedAtUtc = game.EndedAtUtc;
        endInfo = game.EndState != null ? ArchivedGameEnd.FromEndState(game.EndState, game.AllPlayerInfo) : null;
        palette = new ArchivedColor[DynamicPalette.PlayerColors.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            palette[i] = new(DynamicPalette.PlayerColors[i], DynamicPalette.ShadowColors[i], DynamicPalette.VisorColors[i]);
        }

    }

    /// <summary>
    /// 保存されていたデータから復元します。
    /// </summary>
    /// <remarks>
    /// <see cref="IArchivedGameData"/> に含まれない項目、すなわちイベント記録・役職履歴・
    /// プレイヤーの見た目・配色は、まだ保存対象ではないため空の状態になります。
    /// それらを保存できるようにしたら、ここも合わせて埋めること。
    /// </remarks>
    private ArchivedGameImpl(IArchivedGameData data)
    {
        players = [];
        playerResults = data.PlayerResults.ToArray();
        movementPhases = data.MovementPhases.ToArray();
        eventRecords = data.Events.ToArray();
        events = [];
        roleHistory = [];
        mapId = data.MapId;
        startedAtUtc = data.StartedAtUtc;
        endedAtUtc = data.EndedAtUtc;
        endInfo = data.EndInfo;
        palette = [];
    }

    /// <summary>プレイヤーの見た目の色。配色は設定で変わるので、値そのものを写し取る。</summary>
    static private ArchivedColor ColorOf(GamePlayer player)
    {
        var colorId = player.DefaultOutfit.outfit.ColorId;
        if (colorId < 0 || colorId >= DynamicPalette.PlayerColors.Length) return new(VColor.White, VColor.White, VColor.White);
        return new(DynamicPalette.PlayerColors[colorId], DynamicPalette.ShadowColors[colorId], DynamicPalette.VisorColors[colorId]);
    }

    public static IArchivedGame FromCurrentGame() => new ArchivedGameImpl(NebulaGameManager.Instance!);

    /// <summary>
    /// 保存されていたデータから復元します。
    /// </summary>
    public static IArchivedGame FromData(IArchivedGameData data) => new ArchivedGameImpl(data);

    /// <summary>
    /// JSONから復元します。読めない場合はnull。
    /// </summary>
    public static IArchivedGame? FromJson(string json)
    {
        var record = GameRecord.FromJson(json);
        return record != null ? new ArchivedGameImpl(record) : null;
    }

    ArchivedColor IArchivedGame.GetColor(byte colorId) =>
        colorId < palette.Length ? palette[colorId] : new(VColor.White, VColor.White, VColor.White);
}

internal static class ArchivedColorHelper
{
    static public void ReflectToArchivedPalette(this ArchivedColor color)
    {
        DynamicPalette.PlayerColors[NebulaPlayerTab.ArchiveColorId] = color.MainColor;
        DynamicPalette.ShadowColors[NebulaPlayerTab.ArchiveColorId] = color.ShadowColor;
        DynamicPalette.VisorColors[NebulaPlayerTab.ArchiveColorId] = color.VisorColor;
    }
}