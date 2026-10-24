using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Player;

[NebulaRPCHolder]
internal class DeadBodyImpl : Virial.Game.DeadBody
{
    private int id;
    int Virial.Game.DeadBody.Id => id;
    private global::DeadBody vanillaDeadBody;
    global::DeadBody Virial.Game.DeadBody.VanillaDeadBody => vanillaDeadBody;
    private GamePlayer relatedPlayer;
    GamePlayer Virial.Game.DeadBody.Player => relatedPlayer;
    GamePlayer? Virial.Game.DeadBody.CurrentHolder { get; set; }
    bool Virial.Game.DeadBody.IsFullyDissolved => (vanillaDeadBody.ParentId & 0x80) != 0;
    bool Virial.Game.DeadBody.IsViperDeadBody
    {
        get
        {
            if (!isViperDeadBodyCache.HasValue) isViperDeadBodyCache = vanillaDeadBody.TryCast<ViperDeadBody>() != null;
            return isViperDeadBodyCache.Value;
        }
    }
    private bool? isViperDeadBodyCache = null;
    private bool canDrag = true;
    bool Virial.Game.DeadBody.CanDrag { get => canDrag; set => RpcUpdateCanDrag.Invoke((this.id, value)); }

    internal DeadBodyImpl(global::DeadBody vanillaDeadBody, int id, GamePlayer player)
    {
        this.id = id;
        this.vanillaDeadBody = vanillaDeadBody;
        this.relatedPlayer = player;
        this.canDrag = true;
    }

    static private readonly RemoteProcess<(int id, bool canDrag)> RpcUpdateCanDrag = new("UpdateCanDrag", (message, _) =>
    {
        if(ModSingleton<DeadBodyManager>.Instance.TryGetDeadBody(message.id, out var deadBody) && deadBody is DeadBodyImpl impl) 
        {
            impl.canDrag = message.canDrag;
        }
    });
}
