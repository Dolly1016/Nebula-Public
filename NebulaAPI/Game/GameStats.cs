using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Text;

namespace Virial.Game;

public enum GameStatsCategory
{
    Kill,
    Death,
    Game,
    Roles,
    Perks,
    AeroGuesser,
    PaintQuiz,
}

public enum GameStatsAnnotation
{
    None,
    /// <summary>
    /// 2026年11月以降に集計を開始した統計。
    /// </summary>
    From2611,
}

public enum GameStatsUnit
{
    None,
    Second,
    Distance,
}

public interface GameStatsEntry
{
    string Id { get; }
    TextComponent DisplayName { get; }
    int Progress { get; }
    GameStatsCategory Category { get; }
    DefinedAssignable? RelatedAssignable { get; }
    internal int InnerPriority { get; }
    string DisplayValue { get; }
    GameStatsAnnotation Annotation { get; }
    GameStatsUnit Unit { get; }
}

/// <summary>
/// 小数単位で値を記録する統計です。
/// </summary>
public interface DecimalGameStatsEntry
{
    GameStatsEntry Entry { get; }
    int Digits { get; }

    string Id => Entry.Id;
    TextComponent DisplayName => Entry.DisplayName;

    float Progress
    {
        get
        {
            float val = Entry.Progress;
            for (int i = 0; i < Digits; i++) val /= 10f;
            return val;
        }
    }
    GameStatsCategory Category => Entry.Category;
    DefinedAssignable? RelatedAssignable => Entry.RelatedAssignable;
    string DisplayValue => Entry.DisplayValue;
    GameStatsAnnotation Annotation => Entry.Annotation;
    GameStatsUnit Unit => Entry.Unit;
}
