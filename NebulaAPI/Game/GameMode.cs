using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.DI;
using Virial.Text;

namespace Virial.Game;

public abstract class GameModeDefinition : IBit32
{
    abstract public uint AsBit { get; }
    abstract internal IGameModeModule InstantiateModule();
    abstract internal IRoleAllocator InstantiateRoleAllocator();
    abstract internal TextComponent DisplayName { get; }
    abstract internal int MinPlayers { get; }
    

    public static implicit operator uint(GameModeDefinition gamemode) => gamemode.AsBit;
}

public interface IGameModeModule : IModuleContainer, IModule
{
    bool AllowSpecialGameEnd { get; }
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
    public static GameModeDefinition GetGameMode(int id) => allGameModes[id];
    public static GameModeDefinition Standard { get; internal set; } = null!;
    public static GameModeDefinition FreePlay { get; internal set; } = null!;
}
