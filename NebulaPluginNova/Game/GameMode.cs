using Nebula.Roles.Assignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events;
using Virial.Game;
using Virial.Text;

namespace Nebula.Game;


[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class GameModeDefinitionImpl : GameModeDefinition
{
    static GameModeDefinitionImpl()
    {
        GameModes.Standard = new GameModeDefinitionImpl("gamemode.standard", 4, typeof(IGameModeStandard), () => new StandardRoleAllocator());
        GameModes.FreePlay = new GameModeDefinitionImpl("gamemode.freeplay", 0, typeof(IGameModeFreePlay), () => new FreePlayRoleAllocator());
    }

    public GameModeDefinitionImpl(string translationKey, int minPlayers, Type gameModeType, Func<IRoleAllocator> roleAllocator)
    {
        this.display = new TranslateTextComponent(translationKey);
        this.bit = 1u << GameModes.allGameModes.Count;
        this.gameModeType = gameModeType;
        this.roleAllocator = roleAllocator;
        this.minPlayers = minPlayers;
        GameModes.allGameModes.Add(this);
    }

    private TextComponent display;
    private uint bit { get; init; }
    private int minPlayers { get; init; }
    private Type gameModeType { get; init; }
    private Func<IRoleAllocator> roleAllocator { get; init; }
    internal override IGameModeModule InstantiateModule() => (DIManager.Instance.Instantiate(gameModeType) as IGameModeModule)!;
    internal override IRoleAllocator InstantiateRoleAllocator() => roleAllocator.Invoke();

    public override uint AsBit => bit;
    internal override int MinPlayers => minPlayers;
    internal override TextComponent DisplayName => display;
}

internal class GameModeStandardImpl : AbstractModuleContainer, IModule, IGameModeStandard
{
    bool IGameModeModule.AllowSpecialGameEnd => true;
}

internal class GameModeFreePlayImpl : AbstractModuleContainer, IModule, IGameModeFreePlay
{
    bool IGameModeModule.AllowSpecialGameEnd => false;
}