using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Helpers;

namespace Virial.Game;

public interface DeadBody
{
    internal int Id { get; }
    internal global::DeadBody VanillaDeadBody { get; }
    internal Virial.Compat.ModGameObject ModObject => VanillaDeadBody.ModGameObject();

    public Virial.Compat.Vector2 TruePosition => VanillaDeadBody.AsBoolFast() ? VanillaDeadBody.TruePosition : new(0f, 0f);
    public Virial.Compat.Vector2 Position => VanillaDeadBody.AsBoolFast() ? ModObject.Position : new(0f, 0f);
    public bool IsActive => VanillaDeadBody.AsBoolFast();
    public Player Player { get; }
    public Player? CurrentHolder { get; internal set; }
    public bool IsFullyDissolved { get; }
    public bool IsViperDeadBody { get; }
    public bool CanDrag { get; set; }
}
