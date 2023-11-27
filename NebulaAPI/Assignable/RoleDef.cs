namespace Virial.Assignable;

[Flags]
public enum RoleCategory
{
    ImpostorRole = 0x01,
    NeutralRole = 0x02,
    CrewmateRole = 0x04
}

public abstract class AbstractRoleDef
{
    abstract public RoleCategory RoleCategory { get; }
    abstract public string LocalizedName { get; }
    abstract public Color RoleColor { get; }
    abstract public RoleTeam Team { get; }
    virtual internal Type? RoleInstanceType { get => null; }

    public virtual void LoadOptions() { }
}

public abstract class VariableRoleDef<T> : AbstractRoleDef
{
    override internal Type? RoleInstanceType => typeof(T);
}