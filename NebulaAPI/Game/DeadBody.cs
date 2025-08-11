using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

public class DeadBody
{
    internal int Id { get; private set; }
    internal global::DeadBody VanillaDeadBody { get; private set; }

    public Virial.Compat.Vector2 TruePosition => VanillaDeadBody ? VanillaDeadBody.TruePosition : new(0f, 0f);
    public Virial.Compat.Vector2 Position => VanillaDeadBody ? new(VanillaDeadBody.transform.position) : new(0f, 0f);
    public bool IsActive => VanillaDeadBody;
    public Player Player { get; private init; }
    public Player? CurrentHolder { get; internal set; }

    internal DeadBody(global::DeadBody vanillaDeadBody, int id, Player player)
    {
        this.Id = id;
        this.VanillaDeadBody = vanillaDeadBody;
        this.Player = player;
    }
}
