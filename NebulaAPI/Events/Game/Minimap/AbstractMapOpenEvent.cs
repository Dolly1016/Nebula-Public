using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Minimap;

/// <summary>
/// 何らかのミニマップを開いた際に発火するイベントを表します。
/// </summary>
public class AbstractMapOpenEvent : Event
{
    internal AbstractMapOpenEvent() { }
}
