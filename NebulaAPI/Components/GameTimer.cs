namespace Virial.Components;

/// <summary>
/// 能力ボタンで使用できるタイマーを表します。
/// </summary>
public interface IVisualTimer : ILifespan, IReleasable
{
    /// <summary>
    /// タイマーを開始します。
    /// </summary>
    /// <param name="time">開始時間</param>
    /// <returns></returns>
    IVisualTimer Start(float? time = null);

    /// <summary>
    /// 現在の秒数を文字列で返します。
    /// </summary>
    string? TimerText { get; }

    /// <summary>
    /// 現在の進行度を0から1の実数で返します。
    /// </summary>
    float Percentage { get; }

    /// <summary>
    /// 現在の秒数を実数で返します。
    /// </summary>
    float CurrentTime { get; }

    /// <summary>
    /// タイマーが進行中であれば<c>true</c>を返します。
    /// </summary>
    bool IsProgressing { get; }
}

/// <summary>
/// クールダウン及び能力効果タイマーを表します。
/// </summary>
public interface GameTimer : IVisualTimer, INestedLifespan
{
    /// <summary>
    /// 最大時間を表します。
    /// </summary>
    float Max { get; }
    /// <summary>
    /// キルクールダウンとして秒数の進行条件を設定します。
    /// </summary>
    /// <returns></returns>
    GameTimer SetAsKillCoolTimer();
    /// <summary>
    /// 能力クールダウンとして秒数の進行条件を設定します。
    /// </summary>
    /// <returns></returns>
    GameTimer SetAsAbilityTimer();
    /// <summary>
    /// 秒数の進行条件を設定します。
    /// </summary>
    /// <param name="progressWhile">タイマーが進行するとき<c>true</c>を返す関数</param>
    /// <returns></returns>
    GameTimer SetCondition(Func<bool> progressWhile);
    /// <summary>
    /// タイマーを一時停止します。
    /// </summary>
    /// <returns></returns>
    GameTimer Pause();
    /// <summary>
    /// タイマーの進行を再開します。
    /// </summary>
    /// <returns></returns>
    GameTimer Resume();
    /// <summary>
    /// タイマーの範囲を設定します。
    /// </summary>
    /// <param name="min">最小の時間。ふつう、0に設定します。</param>
    /// <param name="max">最大の時間。</param>
    /// <returns></returns>
    GameTimer SetRange(float min, float max);
    /// <summary>
    /// タイマーの範囲を設定します。
    /// </summary>
    /// <param name="max">最大の時間。</param>
    /// <returns></returns>
    GameTimer SetRange(float max) => SetRange(0, max);
    /// <summary>
    /// タイマーの現在時間を設定します。
    /// </summary>
    /// <param name="time">時間。</param>
    /// <returns></returns>
    GameTimer SetTime(float time);
    /// <summary>
    /// タイマーの最大時間を伸ばします。
    /// </summary>
    /// <param name="time">延長する時間。</param>
    /// <returns></returns>
    GameTimer Expand(float time);

    /// <summary>
    /// タイマーのクールダウンがタスクフェイズの再開時にリセットされるよう特性付けます。
    /// </summary>
    /// <returns></returns>
    GameTimer ResetsAtTaskPhase();
}
