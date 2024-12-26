using System.Runtime.CompilerServices;
using Virial.Command;
using Virial.Game;

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
        }
        else if (type == typeof(OutfitDefinition))
        {
            NebulaGameManager.Instance!.TryGetOutfit(OutfitDefinition.OutfitId.PlayersDefault(player.PlayerId), out var outfit);
            return new CoImmediateTask<T>(Unsafe.As<OutfitDefinition, T>(ref outfit));
        }
        else if(type == typeof(string))
        {
            string name = player.Name;
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }
        else if (type == typeof(int))
        {
            int id = player.PlayerId;
            return new CoImmediateTask<T>(Unsafe.As<int, T>(ref id));
        }
        else if (type == typeof(byte))
        {
            byte id = player.PlayerId;
            return new CoImmediateTask<T>(Unsafe.As<byte, T>(ref id));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}
