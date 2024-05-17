using Nebula.Compat;
using Virial.Assignable;

namespace Nebula.Roles;

public class Team : RoleTeam
{
    public string TranslationKey { get; private init; }
    public UnityEngine.Color UnityColor { get; private init; }
    public Virial.Color Color { get;private init; }
    public int Id { get; set; }
    public TeamRevealType RevealType { get; set; }
    public Team(string translationKey, Virial.Color color, TeamRevealType revealType)
    {
        TranslationKey = translationKey;
        UnityColor = color.ToUnityColor();
        Color = color;
        Roles.Register(this);
        RevealType = revealType;
    }
}
