using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.SpecialModes.PaintQuiz;

internal enum QuizCategories
{
    TitleToRole, // 称号から役職 (称号を共有)
    BlurbToRole, // フレーバーから役職 (役職を共有、事前共有不要)
    RoleToBlurb, // 役職からフレーバー (役職を共有、事前共有不要)
    RoleToChallengeTitle, // 役職から称号 (役職を共有)
    IconToRole, //アイコンから役職 (役職を共有、事前共有不要)
}

internal interface QuizCategoryStrategy
{
    QuizCategories Category { get; }

    /// <summary>
    /// 自身の進行状況から問題を提案します。
    /// </summary>
    int[] SuggestMyCandidate(int numOfQuizzes);

    /// <summary>
    /// 自身の進行状況から称号の獲得状況を返します。
    /// </summary>
    /// <param name="numTitleId"></param>
    /// <returns></returns>
    bool HaveAchievedAlready(int numTitleId);

    /// <summary>
    /// クイズのテキストを取得します。
    /// </summary>
    /// <param name="quizSeed"></param>
    /// <param name="achieved"></param>
    /// <returns></returns>
    string GetQuizText(int quizSeed, GamePlayer[] achieved);

    /// <summary>
    /// クイズの答えを取得します。
    /// </summary>
    /// <param name="quizSeed"></param>
    /// <returns></returns>
    string GetAnswerText(int quizSeed);

    /// <summary>
    /// クイズを作成します。
    /// </summary>
    /// <returns></returns>
    int? GenerateQuizSeed();

    void OnReceivePreSharing(int[] candidates);

    static internal QuizCategoryStrategy Create(QuizCategories category)
    {
        return category switch
        {
            QuizCategories.TitleToRole => new TitleToRoleQuizCategory(),
            _ => throw new NotImplementedException($"QuizCategoryStrategy for {category} is not implemented."),
        };
    }

}

internal class TitleToRoleQuizCategory : QuizCategoryStrategy
{
    HashSet<int> idCandidates = [];
    List<INebulaAchievement>? achCandidates = null; //ホスト以外は作成する必要がない

    public QuizCategories Category => QuizCategories.TitleToRole;
    int[] QuizCategoryStrategy.SuggestMyCandidate(int numOfQuizzes)
    {
        int numOfCands = Mathn.Clamp(numOfQuizzes * 10, 50, 100);

        List<INebulaAchievement> achievements = [];
        foreach (var ach in NebulaAchievementManager.AllAchievements)
        {
            if (ach.RelatedRole.IsEmpty()) continue;
            if (ach.IsCleared) achievements.Add(ach);
        }

        List<int> nums = [];
        var random = System.Random.Shared;
        for (int i = 0; i < numOfCands; i++)
        {
            if (achievements.Count == 0) break;
            int index = random.Next(achievements.Count);
            nums.Add(achievements[index].NumId);
            achievements.RemoveAt(index);
        }
        return nums.ToArray();
    }

    bool QuizCategoryStrategy.HaveAchievedAlready(int numTitleId) => NebulaAchievementManager.GetFromNumId(numTitleId)?.IsCleared ?? false;
    string QuizCategoryStrategy.GetQuizText(int quizSeed, GamePlayer[] achieved)
    {
        var achievement = NebulaAchievementManager.GetFromNumId(quizSeed);
        if (achievement == null) return "クイズデータが壊れています。";
        return $"「{Language.Translate(achievement.TranslationKey)}」は何の称号？";
    }

    string QuizCategoryStrategy.GetAnswerText(int quizSeed)
    {
        var achievement = NebulaAchievementManager.GetFromNumId(quizSeed);
        if (achievement == null) return "クイズデータが壊れています。";
        return string.Join(", ", achievement.RelatedRole.Select(r => r.DisplayColoredName));
    }

    void QuizCategoryStrategy.OnReceivePreSharing(int[] candidates)
    {
        foreach(var id in candidates) this.idCandidates.Add(id);
    }

    int? QuizCategoryStrategy.GenerateQuizSeed()
    {
        if(this.achCandidates == null)
        {
            this.achCandidates = new List<INebulaAchievement>(this.idCandidates.Count);
            foreach (var id in this.idCandidates)
            {
                var ach = NebulaAchievementManager.GetFromNumId(id);
                if (ach != null && ach.RelatedRole.Any()) this.achCandidates.Add(ach);
            }
        }
        if (this.achCandidates.Count == 0) return null;

        var index = System.Random.Shared.Next(this.achCandidates.Count);
        var selected = this.achCandidates[index];
        this.achCandidates.RemoveAt(index);
        return selected.NumId;
    }
}