using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    IReadOnlyList<RoleHistory> IArchivedGame.RoleHistory => roleHistory;

    IArchivedEvent[] IArchivedGame.ArchivedEvents => events;

    byte IArchivedGame.MapId => mapId;

    IEnumerable<IArchivedPlayer> IArchivedGame.GetAllPlayers() => players.Values;

    IArchivedPlayer? IArchivedGame.GetPlayer(byte playerId) => players.TryGetValue(playerId, out var p) ? p : null;

    private ArchivedGameImpl(NebulaGameManager game)
    {
        players = [];
        game.AllPlayerInfo.Do(p => players[p.PlayerId] = new ArchivedPlayerImpl(p));
        events = game.GameStatistics.Sealed;
        roleHistory = game.RoleHistory;
        mapId = AmongUsUtil.CurrentMapId;
        palette = new ArchivedColor[DynamicPalette.PlayerColors.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            palette[i] = new(new(DynamicPalette.PlayerColors[i]), new(DynamicPalette.ShadowColors[i]), new(DynamicPalette.VisorColors[i]));
        }

    }

    public static IArchivedGame FromCurrentGame() => new ArchivedGameImpl(NebulaGameManager.Instance!);

    ArchivedColor IArchivedGame.GetColor(byte colorId) => palette[colorId];
}

internal static class ArchivedColorHelper
{
    static public void ReflectToArchivedPalette(this ArchivedColor color)
    {
        DynamicPalette.PlayerColors[NebulaPlayerTab.ArchiveColorId] = color.MainColor.ToUnityColor();
        DynamicPalette.ShadowColors[NebulaPlayerTab.ArchiveColorId] = color.ShadowColor.ToUnityColor();
        DynamicPalette.VisorColors[NebulaPlayerTab.ArchiveColorId] = color.VisorColor.ToUnityColor();
    }
}