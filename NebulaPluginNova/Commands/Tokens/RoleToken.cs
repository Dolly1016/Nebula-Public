using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Command;
using Virial.Game;

namespace Nebula.Commands.Tokens;

/// <summary>
/// 役職定義のトークンです。
/// </summary>
public class RoleCommandToken : ICommandToken
{
    private DefinedRole role;

    public RoleCommandToken(DefinedRole role)
    {
        this.role = role;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(DefinedRole))
        {
            return new CoImmediateTask<T>(Unsafe.As<DefinedRole, T>(ref role));
        }
        else if (type == typeof(string))
        {
            string name = role.LocalizedName;
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }
        else if (type == typeof(int))
        {
            int id = role.Id;
            return new CoImmediateTask<T>(Unsafe.As<int, T>(ref id));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}