using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

public interface IPlayerlike : IGameObject
{
    Player? RealPlayer { get; }
    Player? VisualPlayer { get; }
}
