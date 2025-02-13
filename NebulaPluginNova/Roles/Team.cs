
using Virial.Assignable;

namespace Nebula.Roles;

public class Team : RoleTeam
{
    public string TranslationKey { get; private init; }
    public UnityEngine.Color UnityColor { get; private init; }
    public Virial.Color Color { get;private init; }
    public int Id { get; set; }
    public TeamRevealType RevealType { get; set; }
    private Func<float>? killCooldownSupplier;
    public float KillCooldown => killCooldownSupplier?.Invoke() ?? AmongUsUtil.VanillaKillCoolDown;
    public Team(string translationKey, Virial.Color color, TeamRevealType revealType, Func<float> killCooldown = null)
    {
        TranslationKey = translationKey;
        UnityColor = color.ToUnityColor();
        Color = color;
        RevealType = revealType;
        this.killCooldownSupplier = killCooldown;

        Roles.Register(this);
    }
}
