using Nebula.Behavior;
using Nebula.Roles.Impostor;
using Nebula.Roles.Modifier;
using Nebula.Roles.Neutral;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Game;

public class CriteriaManager
{
    private record TriggeredGameEnd(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, EditableBitMask<Virial.Game.Player>? additionalWinners);
    private List<TriggeredGameEnd> triggeredGameEnds = [];
    
    public void Trigger(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, EditableBitMask<Virial.Game.Player>? additionalWinners)
    {
        if (additionalWinners != null && reason == GameEndReason.Special && triggeredGameEnds.Find(end => end.gameEnd == gameEnd && end.reason == reason && end.additionalWinners != null, out var end))
        {
            GamePlayer.AllPlayers.Where(additionalWinners.Test).Do(p => end.additionalWinners!.Add(p));
        }
        else
        {
            triggeredGameEnds.Add(new(gameEnd, reason, additionalWinners));
        }
    }

    public void CheckAndTriggerGameEnd()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        //終了条件が確定済みなら何もしない
        if (NebulaGameManager.Instance?.EndState != null) return;

        //ゲーム終了が許可されていなければ何もしない
        if (!(NebulaAPI.CurrentGame?.GameMode?.AllowSpecialGameEnd ?? true)) return;

        if ((ExileController.Instance) && !Minigame.Instance)
        {
            triggeredGameEnds.RemoveAll(t => t.reason is GameEndReason.Situation or GameEndReason.SpecialSituation);
            return; //追放中はゲーム終了条件の判定をスキップする。
        }

        //条件にそぐわない勝利条件の削除
        triggeredGameEnds.RemoveAll(t =>
        {
            return GameOperatorManager.Instance?.Run<EndCriteriaPreMetEvent>(new(t.gameEnd, t.reason))?.IsBlocked ?? false;
        });

        if(triggeredGameEnds.Count == 0) return;

        var max = triggeredGameEnds.Max(g => g.gameEnd.Priority);
        var ends = triggeredGameEnds.Where(g => g.gameEnd.Priority == max).ToArray();
        triggeredGameEnds.Clear();

        var basicEnd = ends[0];
        var combinedWinners = ends.Where(g => g.gameEnd == basicEnd.gameEnd).Select(g => g.additionalWinners);
        
        NebulaGameManager.Instance?.InvokeEndGame(basicEnd.gameEnd, basicEnd.reason, NebulaGameManager.Instance.AllPlayerInfo.Aggregate(0, (v, p) => ends.Any(e => e.additionalWinners?.Test(p) ?? false) ? (v | (1 << p.PlayerId)) : v));
    }
}
