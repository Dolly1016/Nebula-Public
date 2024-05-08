namespace Virial.Common;

[Flags]
internal enum PermissionResult
{
    Undefined = 0x00,
    Denied = 0x01,
    Accepted = 0x10,
    Confused = 0x11,
}

public class Permission
{
    static internal PermissionResult Inverse(PermissionResult result) => (PermissionResult)((int)result ^ 0x11);
    public record PermissionVariable(Permission permission, bool inverse);

    private PermissionHolder parents;
    internal Permission(params PermissionVariable[] parents)
    {
        this.parents = new(parents);
    }

    internal PermissionResult Test(Permission permission)
    {
        if (permission == this) return PermissionResult.Accepted;
        return PermissionTest(permission, parents.permissions);   
    }

    static internal PermissionResult PermissionTest(Permission permission, IEnumerable<PermissionVariable> permissions)
    {
        PermissionResult result = PermissionResult.Undefined;
        foreach (var val in permissions)
        {
            var temp = val.permission.Test(permission);
            if (val.inverse) temp = Permission.Inverse(temp);

            if (temp == PermissionResult.Denied) return PermissionResult.Denied;

            result |= temp;
        }
        return result;
    }
}

public interface IPermissionHolder
{
    bool Test(Permission permission);
}

public class PermissionHolder : IPermissionHolder
{
    internal Permission.PermissionVariable[] permissions;

    public PermissionHolder(Permission.PermissionVariable[] permissions)
    {
        this.permissions = permissions;
    }

    bool IPermissionHolder.Test(Virial.Common.Permission permission) => Test(permission);
    public bool Test(Virial.Common.Permission permission) => Permission.PermissionTest(permission, permissions) == PermissionResult.Accepted;
}

public class VariablePermissionHolder : IPermissionHolder
{
    internal List<Permission.PermissionVariable> permissions;

    public VariablePermissionHolder(IEnumerable<Permission.PermissionVariable> permissions)
    {
        this.permissions = new(permissions);
    }

    public VariablePermissionHolder AddPermission(Permission permission, bool inverse = false)
    {
        permissions.Add(new(permission, inverse));
        return this;
    }

    bool IPermissionHolder.Test(Virial.Common.Permission permission) => Test(permission);
    public bool Test(Virial.Common.Permission permission) => Permission.PermissionTest(permission, permissions) == PermissionResult.Accepted;
}
