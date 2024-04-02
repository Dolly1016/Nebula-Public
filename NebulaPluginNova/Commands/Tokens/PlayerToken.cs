using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;

namespace Nebula.Commands.Tokens;

/// <summary>
/// プレイヤーのトークンです
/// </summary>
public class PlayerCommandToken : ICommandToken
{
    private GamePlayer player;

    public PlayerCommandToken(GamePlayer player)
    {
        this.player = player;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if(type == typeof(GamePlayer))
        {
            return new CoImmediateTask<T>(Unsafe.As<GamePlayer, T>(ref player));
        }else if(type == typeof(string))
        {
            string name = player.Name;
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }
        else if (type == typeof(int) || type == typeof(byte))
        {
            byte id = player.PlayerId;
            return new CoImmediateTask<T>(Unsafe.As<byte, T>(ref id));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}
