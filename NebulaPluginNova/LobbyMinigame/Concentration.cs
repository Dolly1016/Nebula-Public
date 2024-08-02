namespace Nebula.LobbyMinigame;

public class ConcentrationViewer : ILobbyMinigameViewer
{
    private Concentration MinigameData { get; init; }
    ILobbyMinigameData ILobbyMinigameViewer.MinigameData => MinigameData;
    public MetaScreen Screen { get; private init; }
    public ConcentrationViewer(Concentration minigameData) {
        Screen = MetaScreen.GenerateBlankWindow(Vector2.one, null, new(-2.5f, 1.4f), Vector3.zero, true);
        MinigameData = minigameData;
    }


}
public class Concentration : ILobbyMinigameData
{
    public Vector2Int AlignmentPattern => Table.Length switch { 
        12 => new(4,3),
        _ => new(Table.Length,1)
    };

    [JsonSerializableField]
    public int[] Table;
    [JsonSerializableField]
    public string[] Players;
    override public string[] GetPlayers() => Players;
    override public ILobbyMinigameViewer? Open() => new ConcentrationViewer(this);
}
