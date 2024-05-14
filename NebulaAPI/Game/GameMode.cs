using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Game;

public abstract class GameModeDefinition
{
    abstract internal int AsBit { get; }
    abstract internal IGameModeModule InstantiateModule();
    

    public static implicit operator int(GameModeDefinition gamemode) => gamemode.AsBit;
}

public interface IGameModeModule : IModuleContainer, IModule
{

}

/// <summary>
/// 標準モードのゲームで生成されるモジュールです。
/// </summary>
public interface IGameModeStandard : IGameModeModule
{ 
}

/// <summary>
/// フリープレイモードのゲームで生成されるモジュールです。
/// </summary>
public interface IGameModeFreePlay : IGameModeModule
{
}


public static class GameModes
{
    internal static List<GameModeDefinition> allGameModes = new();
    public static IEnumerable<GameModeDefinition> AllGameModes => allGameModes;
    public static GameModeDefinition Standard { get; internal set; } = null!;
    public static GameModeDefinition FreePlay { get; internal set; } = null!;
}
