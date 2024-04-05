using Virial.Assignable;
using Virial.Text;

namespace Virial.Game;

public interface IPlayerAttribute
{
    internal int Id { get; }
    internal int ImageId { get; }
    internal string Name { get; }
    internal IPlayerAttribute CategorizedAttribute { get; }

    /// <summary>
    /// プレイヤーが属性を認識できるかどうか調べます。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    bool CanCognize(Player player);
}

public static class PlayerAttributes
{
    static public IPlayerAttribute Accel { get; internal set; }
    static public IPlayerAttribute Decel { get; internal set; }
    static public IPlayerAttribute Invisible { get; internal set; }
    static public IPlayerAttribute CurseOfBloody { get; internal set; }
    static public IPlayerAttribute InvisibleElseImpostor { get; internal set; }
    static public IPlayerAttribute Isolation { get; internal set; }
    static public IPlayerAttribute BuskerEffect { get; internal set; }
}

public interface Player
{
    internal PlayerControl VanillaPlayer { get; }

    public string Name { get; }
    public byte PlayerId { get; }
    public bool IsDead { get; }
    public bool AmOwner { get; }
    public bool AmHost { get; }
    public void MurderPlayer(Player player, CommunicableTextTag playerState, CommunicableTextTag eventDetail, bool showBlink, bool showKillOverlay);
    public void Suicide(CommunicableTextTag playerState, CommunicableTextTag eventDetail,bool showKillOverlay);
    public void GainAttribute(IPlayerAttribute attribute, float duration, bool canPassMeeting, int priority, int? duplicateTag = null);
    public void GainAttribute(float speedRate, float duration, bool canPassMeeting, int priority, int? duplicateTag = null);
    public bool HasAttribute(IPlayerAttribute attribute);

    public RuntimeRole Role { get; }
    public IEnumerable<RuntimeModifier> Modifiers { get; }

    public bool IsImpostor => Role.Role.Category == RoleCategory.ImpostorRole;
}
