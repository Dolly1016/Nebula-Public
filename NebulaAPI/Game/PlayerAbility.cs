namespace Virial.Game;

public interface IPlayerAbility : IBindPlayer, IGameOperator, ILifespan
{
    int[] AbilityArguments => [];
    bool HideKillButton => false;
    bool KillIgnoreTeam => false;
    bool EyesightIgnoreWalls => false;
    bool IgnoreBlackout => false;
    bool BlockCallingEmergencyMeeting => false;
    bool BlockUsingUtility => false;
    IEnumerable<IPlayerAbility> SubAbilities => [];
}

public interface IUsurpableAbility : IPlayerAbility
{
    bool IsUsurped { get; }
    bool Usurp();
}

public abstract class AbstractPlayerAbility : DependentLifespan, IPlayerAbility
{
    public Player MyPlayer { get; private init; }
    public bool AmOwner => MyPlayer.AmOwner;

    public AbstractPlayerAbility(Player player)
    {
        MyPlayer = player;
    }
}

public abstract class AbstractPlayerUsurpableAbility : DependentLifespan, IUsurpableAbility
{
    public Player MyPlayer { get; private init; }
    public bool AmOwner => MyPlayer.AmOwner;
    public bool IsUsurped { get; private set; } = false;
    public bool Usurp()
    {
        if (IsUsurped) return false;
        IsUsurped = true;
        return true;
    }

    public AbstractPlayerUsurpableAbility(Player player, bool isUsurped)
    {
        MyPlayer = player;
        IsUsurped = isUsurped;
    }
}