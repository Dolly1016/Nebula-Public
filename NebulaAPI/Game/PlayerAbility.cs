using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

public interface IPlayerAbility : IBindPlayer, IGameOperator, IReleasable, ILifespan
{
    int[] RoleArguments => [];
    bool HideKillButton => false;
    bool KillIgnoreTeam => false;
}

public abstract class AbstractPlayerAbility : ComponentHolder, IPlayerAbility
{
    public Player MyPlayer { get; private init; }
    public bool AmOwner => MyPlayer.AmOwner;

    public AbstractPlayerAbility(Player player)
    {
        MyPlayer = player;
    }
}
