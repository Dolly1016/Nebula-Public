using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Game;

namespace Nebula.Game;


[NebulaPreLoad]
internal class GameModeDefinitionImpl : GameModeDefinition
{
    public static void Load()
    {
        GameModes.Standard = new GameModeDefinitionImpl(() => new GameModeStandardImpl());
        GameModes.FreePlay = new GameModeDefinitionImpl(() => new GameModeFreePlayImpl());
    }

    public GameModeDefinitionImpl(Func<IGameModeModule> moduleGenerator)
    {
        this.bit = 1 << GameModes.allGameModes.Count;
        this.moduleGenerator = moduleGenerator;
        GameModes.allGameModes.Add(this);
    }

    private int bit { get; init; }
    private Func<IGameModeModule> moduleGenerator;
    internal override IGameModeModule InstantiateModule() => moduleGenerator.Invoke();
    internal override int AsBit => bit;
}

internal class GameModeStandardImpl : AbstractModuleContainer, IModule, IGameModeStandard
{

}

internal class GameModeFreePlayImpl : AbstractModuleContainer, IModule, IGameModeFreePlay
{

}


