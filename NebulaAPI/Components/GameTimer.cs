namespace Virial.Components;

public interface GameTimer : IReleasable
{
    GameTimer SetAsKillCoolTimer();
    GameTimer SetAsAbilityTimer();
    GameTimer Start(float? time = null);
    GameTimer Pause();
    GameTimer Resume();
    GameTimer SetRange(float min, float max);
    GameTimer SetTime(float time);
    GameTimer Expand(float time);
    float CurrentTime { get; }
    float Percentage { get; }
    bool IsInProcess { get; }

    /// <summary>
    /// タイマーのクールダウンがタスクフェイズの再開時にリセットされるよう特性付けます。
    /// </summary>
    /// <returns></returns>
    GameTimer ResetsAtTaskPhase();
}
