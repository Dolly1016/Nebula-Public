using Nebula.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.LobbyMinigame;

public class ILobbyMinigameData
{
    /// <summary>
    /// ゲームに参加しているプレイヤーを返します。
    /// </summary>
    virtual public string[] GetPlayers() => [];

    /// <summary>
    /// 有効なゲームである場合trueを返します。
    /// </summary>
    virtual public bool IsInvalidGame => true;

    /// <summary>
    /// ミニゲームを開きます。
    /// </summary>
    /// <returns></returns>
    virtual public ILobbyMinigameViewer? Open() => null;
}

public interface ILobbyMinigameViewer
{
    ILobbyMinigameData MinigameData { get; }
}

public static class LobbyMinigameManager
{
    static private JsonDataSaver<ILobbyMinigameData> LobbyMinigameSaver = new("LobbyGame");
    static public bool HasSavedMinigame => !(LobbyMinigameSaver.Data?.IsInvalidGame ?? true);

    public record LobbyMinigameType(string InternalName, Virial.Media.Image? Image, Func<string[],ILobbyMinigameData> GameGenerator);
    static private List<LobbyMinigameType> AllTypes = new();

    static public void RegisterMinigame(LobbyMinigameType minigameType) => AllTypes.Add(minigameType);
}
