using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.AeroGuesser;

internal class ScoreMap
{
    private readonly Dictionary<byte, (int score, int lastAdded)> scoreMap = [];

    public int GetScore(byte playerId) => scoreMap.TryGetValue(playerId, out var val) ? val.score : 0;
    public int GetLastAddedScore(byte playerId) => scoreMap.TryGetValue(playerId, out var val) ? val.lastAdded : 0;
    public void AddScore(byte playerId, int score)
    {
        scoreMap[playerId] = (GetScore(playerId) + score, score);
    }
}
