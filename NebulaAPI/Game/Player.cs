using Virial.Text;

namespace Virial.Game;

public enum PlayerAttribute
{
    Accel,
    Decel,
    Invisible,
    CurseOfBloody,
    MaxId
}

public interface Player
{
    internal PlayerControl VanillaPlayer { get; }
    public string Name { get; }
    public byte PlayerId { get; }
    public bool IsDead { get; }
    public bool AmOwner { get; }
    public void MurderPlayer(Player player, CommunicableTextTag playerState, CommunicableTextTag eventDetail, bool showBlink, bool showKillOverlay);
    public void Suicide(CommunicableTextTag playerState, CommunicableTextTag eventDetail,bool showKillOverlay);
    public void GainAttribute(PlayerAttribute attribute, float duration, bool canPassMeeting, int priority, int? duplicateTag = null);
    public void GainAttribute(float speedRate, float duration, bool canPassMeeting, int priority, int? duplicateTag = null);
    public bool HasAttribute(PlayerAttribute attribute);
}
