using Nebula.AeroGuesser;
using Nebula.Roles.Assignment;
using Virial.Assignable;
using Virial.DI;
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
        GameModes.AeroGuesser = new GameModeDefinitionImpl("gamemode.aeroguesser", 1, typeof(IGameModeAeroGuesser), amHost => AeroGuesserSenario.CoIntro(amHost), false);
    }

    private GameModeDefinitionImpl(string translationKey, int minPlayers, Type gameModeType, Func<IRoleAllocator> roleAllocator, Func<bool, IEnumerator>? alternativeRoutine, bool withRoleSettings)
    {
        this.display = new TranslateTextComponent(translationKey);
        this.bit = 1u << GameModes.allGameModes.Count;
        this.gameModeType = gameModeType;
        this.roleAllocator = roleAllocator;
        this.minPlayers = minPlayers;
        this.alternativeRoutine = alternativeRoutine;
        this.withRoleSettings = withRoleSettings;
        GameModes.allGameModes.Add(this);
    }

    public GameModeDefinitionImpl(string translationKey, int minPlayers, Type gameModeType, Func<IRoleAllocator> roleAllocator) : this(translationKey, minPlayers, gameModeType, roleAllocator, null, true) { }
    public GameModeDefinitionImpl(string translationKey, int minPlayers, Type gameModeType, Func<bool, IEnumerator> alternativeRoutine, bool withRoleSettings = true) : this(translationKey, minPlayers, gameModeType, null!, alternativeRoutine, withRoleSettings) { }

    private bool withRoleSettings;
    public override bool WithRoleSettings => withRoleSettings;
    private TextComponent display;
    private uint bit { get; init; }
    private int minPlayers { get; init; }
    private Type gameModeType { get; init; }
    private Func<IRoleAllocator> roleAllocator { get; init; }
    private Func<bool, IEnumerator>? alternativeRoutine { get; init; }
    internal override IGameModeModule InstantiateModule() => (DIManager.Instance.Instantiate(gameModeType) as IGameModeModule)!;
    internal override IRoleAllocator InstantiateRoleAllocator() => roleAllocator.Invoke();
    internal override IEnumerator? GetAlternativeRoutine(bool amHost) => alternativeRoutine?.Invoke(amHost);
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
    bool IGameModeModule.CanGetTitle => false;
}
