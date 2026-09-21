namespace Virial.Events.Player;

/// <summary>
/// プレイヤーが勝利しているかどうか判定します。
/// このイベントはホストでのみ実行されます。<see cref="Virial.Attributes.Local"/>を指定すると意図せぬ挙動をする恐れがあります。
/// </summary>

public class PlayerCheckWinEvent : AbstractPlayerEvent
{
    public Virial.Game.GameEnd GameEnd { get; private init; }
    public bool IsWin { get; private set; } = false;
    /// <summary>
    /// 条件を満たすなら勝利させます。
    /// </summary>
    /// <param name="win">勝利するかどうか。</param>
    /// <returns>この呼び出しで勝利するようになったならtrue。条件を満たさない場合や、既に勝利していた場合はfalse。</returns>
    public bool SetWinIf(bool win)
    {
        //条件を満たさないなら何も起きていない。変化なしとして扱う。
        if (!win) return false;

        var lastVal = IsWin;
        IsWin = true;
        return !lastVal;
    }
    public BitMask<Virial.Game.Player> LastWinners { get; private init; }
    public Virial.Game.GameEndDetail Recorder { get; }

    internal PlayerCheckWinEvent(Virial.Game.Player player, Virial.Game.GameEnd gameEnd, BitMask<Virial.Game.Player> lastWinners, Virial.Game.GameEndDetail recorder) : base(player)
    {
        this.LastWinners = lastWinners;
        this.GameEnd = gameEnd;
        this.Recorder = recorder;
    }
}
