namespace Virial.Assignable;

public enum TeamRevealType
{
    OnlyMe,
    Everyone,
    Teams,
}

public interface RoleTeam
{
    public string TranslationKey { get; }
    internal UnityEngine.Color UnityColor { get; }
    public Virial.Color Color { get; }
    public int Id { get; }
    public TeamRevealType RevealType { get; }
}

public static class NebulaTeams
{
    public static RoleTeam CrewmateTeam { get; internal set; } = null!;
    public static RoleTeam ImpostorTeam { get; internal set; } = null!;
    public static RoleTeam JackalTeam { get; internal set; } = null!;
    public static RoleTeam JesterTeam { get; internal set; } = null!;
    public static RoleTeam VultureTeam { get; internal set; } = null!;
    public static RoleTeam ArsonistTeam { get; internal set; } = null!;
    public static RoleTeam PaparazzoTeam { get; internal set; } = null!;
    public static RoleTeam ChainShifterTeam { get; internal set; } = null!;
}
