using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;

namespace Virial.Game.Object;

public class Door : IGameObject, IEquatable<Door>
{

    internal OpenableDoor VanillaObject { get; private set; }
    internal int DoorId => VanillaObject.Id;
    public Virial.Compat.Vector2 Position => VanillaObject.transform.position;
    public bool Equals(Door? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return DoorId == other.DoorId;
    }

    internal Door(OpenableDoor door)
    {
        this.VanillaObject = door;
    }
}
