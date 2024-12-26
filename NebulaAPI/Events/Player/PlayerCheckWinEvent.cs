namespace Virial.Events.Player;

/// <summary>
/// プレイヤーが勝利しているかどうか判定します。
/// このイベントはホストでのみ実行されます。<see cref="Virial.Attributes.Local"/>を指定すると意図せぬ挙動をする恐れがあります。
/// </summary>

public class PlayerCheckWinEvent : AbstractPlayerEvent
{
    public Virial.Game.GameEnd GameEnd { get; private init; }
    public bool IsWin { get; set; } = false;
    public void SetWinIf(bool win) => IsWin |= win;
    public BitMask<Virial.Game.Player> LastWinners { get; private init; }

    internal PlayerCheckWinEvent(Virial.Game.Player player, Virial.Game.GameEnd gameEnd, BitMask<Virial.Game.Player> lastWinners) : base(player)
    {
        this.LastWinners = lastWinners;
        this.GameEnd = gameEnd;
    }
}
