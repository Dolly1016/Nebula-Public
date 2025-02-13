using Virial;
using Virial.DI;
using static UnityEngine.RemoteConfigSettingsHelper;

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
    public (SynchronizeTag tag, float time)? LastSync { get; private set; } = null;

    protected override void OnInjected(Virial.Game.Game container)
    {
        DebugScreen.Push(new FunctionalDebugTextContent(() => {
            if (LastSync == null) return null;
            if (!PlayerControl.LocalPlayer) return null;

            float wait = 3f;
            if (LastSync.Value.tag == SynchronizeTag.PreSpawnMinigame) return null;

            if (Time.time - LastSync.Value.time < wait) return null;
            if (!sync.TryGetValue(LastSync.Value.tag, out var val)) return null;

            var waitFor = PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => (val & (1 << p.PlayerId)) == 0).ToArray() ?? [];
            if (waitFor.Length == 0 || waitFor.Any(p => p.AmOwner)) return null;

            return Language.Translate("log.awaiting").Replace("%TAG%", LastSync.Value.tag.ToString()).Bold() + waitFor.Join(p => "\n -" + p.name);
        }, container));
    }

    public void ResetSync(SynchronizeTag tag)
    {
        sync[tag] = 0;
        ResetSyncOnlyHistory(tag);
    }

    public void ResetSyncOnlyHistory(SynchronizeTag tag)
    {
        if (LastSync?.tag == tag) LastSync = null;
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
        (message, calledByMe) =>
        {
            var synchronizer = NebulaAPI.CurrentGame?.GetModule<Synchronizer>();
            synchronizer?.Sync(message.Item1, message.Item2);
            if (calledByMe && synchronizer != null) synchronizer.LastSync = (message.Item1, Time.time);
        }
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
