namespace Virial.Components;

public interface IVisualTimer
{
    void Start(float? time = null);
    string? TimerText { get; }
    float Percentage { get; }

    //タイマーが進行中であるとき、真を返します。
    bool IsProgressing { get; }
}

public interface GameTimer : IReleasable, IVisualTimer
{
    GameTimer SetAsKillCoolTimer();
    GameTimer SetAsAbilityTimer();
    GameTimer Pause();
    GameTimer Resume();
    GameTimer SetRange(float min, float max);
    GameTimer SetRange(float max) => SetRange(0, max);
    GameTimer SetTime(float time);
    GameTimer Expand(float time);
    float CurrentTime { get; }

    /// <summary>
    /// タイマーのクールダウンがタスクフェイズの再開時にリセットされるよう特性付けます。
    /// </summary>
    /// <returns></returns>
    GameTimer ResetsAtTaskPhase();
}
