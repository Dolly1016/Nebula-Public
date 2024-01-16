using Virial.Game;
using Virial.Text;

namespace Virial.Events.Meeting;

/// <summary>
/// プレイヤーが追放される直前に、追加追放者を選定する際に呼び出されます。
/// さらなる追加追放者を追加することができます。
/// </summary>
public class CheckExtraVictimEvent
{
    public Game.Player? Exiled { get; internal set; }
    internal List<(Game.Player victim, Game.Player? killer,CommunicableTextTag reason, CommunicableTextTag eventDetail)> ExtraVictim = new();
    internal CheckExtraVictimEvent(Game.Player? exiled) 
    {
        Exiled = exiled;
    }

    /// <summary>
    /// 追加追放者を追加します。
    /// </summary>
    /// <param name="victim">追加追放者</param>
    /// <param name="killer">追放者をキルしたプレイヤー 自殺の場合はnullを指定してください</param>
    /// <param name="deathReason">死亡理由</param>
    /// <param name="eventDetail">追放イベントの種別</param>
    public void AddExtraVictims(Game.Player victim, Game.Player? killer, CommunicableTextTag deathReason, CommunicableTextTag eventDetail)
    {
        ExtraVictim.Add((victim, killer, deathReason, eventDetail));
    }
}
