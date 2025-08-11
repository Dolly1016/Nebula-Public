using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.DI;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Player;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class DeadBodyManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess)
    {
        DIManager.Instance.RegisterModule(() => new DeadBodyManager());
    }

    private DeadBodyManager()
    {
        ModSingleton<DeadBodyManager>.Instance = this;
        this.Register(NebulaAPI.CurrentGame!);
    }

    static public int GenerateId(byte requestSender, int requestId, int variation)
    {
        return (int)requestSender << 2 | requestId << 8 | variation;
    }

    Dictionary<int, Virial.Game.DeadBody> deadBodies = [];

    public bool TryGetDeadBody(int id,  [MaybeNullWhen(false)] out Virial.Game.DeadBody deadBody) {
        return deadBodies.TryGetValue(id, out deadBody) && deadBody.VanillaDeadBody;
    }

    public Virial.Game.DeadBody RegisterDeadBody(global::DeadBody deadBody, int id, GamePlayer player)
    {
        var wrapped = new Virial.Game.DeadBody(deadBody, id, player);
        deadBodies[id] = wrapped;
        return wrapped;
    }
    public IEnumerable<Virial.Game.DeadBody> AllDeadBodies => deadBodies.Values.Where(d => d.IsActive);

    void OnRemoveAllDeadBodies(MeetingStartEvent _) => deadBodies.Clear();
}
