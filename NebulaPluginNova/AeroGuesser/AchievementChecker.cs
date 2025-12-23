using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.AeroGuesser;

internal class AchievementChecker : IGameOperator
{
    public AchievementChecker()
    {
        this.RegisterPermanently();
    }

    internal void OnGameStart()
    {
        new StaticAchievementToken("stats.aeroGuesser.gamePlay");
    }

    private bool recordLess900 = false;
    internal void OnGetAnswer(AeroGuesserQuizData.QuizEntry quiz, AeroPlayerOneQuizStatus[] currentStatus)
    {
        int maxElseMe = 0;
        int myScore = 0;
        var myId = PlayerControl.LocalPlayer.PlayerId;
        foreach (var status in currentStatus)
        {
            if(status.PlayerId == myId)
            {
                myScore = status.Score;
                if (status.selectedMap != byte.MaxValue && status.selectedMap != quiz.mapId) new StaticAchievementToken("aeroGuesser.mapMismatch");
            }
            else if(maxElseMe < status.Score)
            {
                maxElseMe = status.Score;
            }
        }

        new AchievementToken<int>("stats.aeroGuesser.totalScore", myScore, (v, _) => v);
        if(myScore == 1000) new StaticAchievementToken("stats.aeroGuesser.perfectScore");

        if (myScore >= 800 && maxElseMe * 2 <= myScore && maxElseMe > 0) new StaticAchievementToken("aeroGuesser.doubleScore2");
        recordLess900 |= myScore < 900;
    }

    private void OnGameEnd(GameEndEvent ev)
    {
        //10問、HardOnly、マップ2種以上、900未満を記録していない
        if(GeneralConfigurations.NumOfQuizOption == 10 && GeneralConfigurations.AeroGuesserHardOption >= 1 && GeneralConfigurations.AeroGuesserNormalOption == 0 && BitOperations.PopCount((uint)AeroGuesserSenario.MapMask) >= 2 && !recordLess900)
        {
            new StaticAchievementToken("aeroGuesser.allMore900");
        }
    }
}
