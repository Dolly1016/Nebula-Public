using Virial;
using Virial.DI;

namespace Nebula.Modules;

public enum SynchronizeTag
{
    PreSpawnMinigame,
    CheckExtraVictims,
    PostMeeting
}

[NebulaRPCHolder]
public class Synchronizer : AbstractModule<Virial.Game.Game>
{
    public Dictionary<SynchronizeTag, uint> sync = new();
    
    public void ResetSync(SynchronizeTag tag)
    {
        sync[tag] = 0;
    }

    private void Sync(SynchronizeTag tag,byte playerId)
    {
        if(!sync.ContainsKey(tag)) sync[tag] = 0;
        sync[tag] |= (uint)1 << (int)playerId;
    }

    public bool TestSyncSingle(SynchronizeTag tag, byte playerId)
    {
        if (!sync.TryGetValue(tag, out var val)) return false;
        return (val & ((uint)1 << (int)playerId)) != 0;
    }

    static public RemoteProcess<(SynchronizeTag, byte)> RpcSync = new(
        "Syncronize",
        (message, calledByMe) => NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.Sync(message.Item1,message.Item2)
        );

    public void SendSync(SynchronizeTag tag)
    {
        RpcSync.Invoke((tag,PlayerControl.LocalPlayer.PlayerId));
    }
    
    public IEnumerator CoSync(SynchronizeTag tag, bool withSurviver = true,bool withGhost = false,bool withBot = false, bool withVanilla = false)
    {
        if (!sync.ContainsKey(tag)) sync[tag] = 0;

        while (true)
        {
            if (TestSync(tag, withSurviver, withGhost, withBot, withVanilla)) yield break;

            yield return null;
        }        
    }

    public IEnumerator CoSyncAndReset(SynchronizeTag tag, bool withSurviver = true, bool withGhost = false, bool withBot = false, bool withVanilla = false)
    {
        yield return CoSync(tag, withSurviver, withGhost, withBot, withVanilla);
        ResetSync(tag);
        yield break;
    }

    public bool TestSync(SynchronizeTag tag, bool withSurviver = true, bool withGhost = false, bool withBot = false, bool withVanilla = false)
    {
        if (!sync.TryGetValue(tag, out var val)) val = 0;
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            if (!withSurviver && !p.Data.IsDead) continue;
            if (!withGhost && p.Data.IsDead) continue;
            if (!withBot && p.isDummy) continue;

            if ((val & (1 << p.PlayerId)) == 0) return false;
        }
        return true;
    }
}
