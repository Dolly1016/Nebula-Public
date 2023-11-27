using Virial.Game;
using Virial.Text;

namespace Virial.Events.Meeting;

public class CheckExtraVictimEvent
{
    public Game.Player? Exiled { get; internal set; }
    internal List<(Game.Player victim, Game.Player? killer,CommunicableTextTag reason, CommunicableTextTag eventDetail)> ExtraVictim = new();
    internal CheckExtraVictimEvent(Game.Player? exiled) 
    {
        Exiled = exiled;
    }

    public void AddExtraVictims(Game.Player victim, Game.Player? killer, CommunicableTextTag deathReason, CommunicableTextTag eventDetail)
    {
        ExtraVictim.Add((victim, killer, deathReason, eventDetail));
    }
}
