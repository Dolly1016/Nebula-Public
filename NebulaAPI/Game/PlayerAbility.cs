using Virial.Assignable;

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

    /// <summary>
    /// インポスターであってもサボタージュが使用できない場合、trueを返します。
    /// </summary>
    bool BlockSabotage => false;

    /// <summary>
    /// 自身の陣営に関係なくサボタージュを発動できる場合、trueを返します。
    /// </summary>
    bool CanInvokeSabotage => false;

    /// <summary>
    /// プレイヤーが通報ボタンを持つとき、trueを返します。
    /// falseを返すアビリティが1つでもあれば、プレイヤーは通報ボタンを持ちません。
    /// </summary>
    bool HasReportButton => true;

    /// <summary>
    /// プレイヤーが通報ボタンを押せうるとき、trueを返します。
    /// falseを返すアビリティが1つでもあれば、プレイヤーは通報ボタンを押せません。
    /// 通報可能な死体が無ければこのプロパティの値によらず通報ボタンは光らず、押しても何も起こりません。
    /// </summary>
    bool CanReport => true;

    IEnumerable<IPlayerAbility> SubAbilities => [];
    IEnumerable<DefinedAssignable> SubAssignableOnHelp => [];

    Virial.Media.GUIWidget? ProgressWidget => null;
    string? MoreInformation => null;
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
    public virtual bool Usurp()
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