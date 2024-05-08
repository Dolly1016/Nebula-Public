using Virial.Assignable;

namespace Nebula.Roles;

public class Team : RoleTeam
{
    public string TranslationKey { get; private init; }
    public Color Color { get; private init; }
    public int Id { get; set; }
    public TeamRevealType RevealType { get; set; }
    public Team(string translationKey, Color color, TeamRevealType revealType)
    {
        TranslationKey = translationKey;
        Color = color;
        Roles.Register(this);
        RevealType = revealType;
    }
}
