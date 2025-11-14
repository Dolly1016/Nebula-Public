using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Game;

public class GameShowIntroLocalEvent: Event
{
    public string TeamName { get; set; }
    public string RoleName { get; set; }
    public string RoleBlurb { get; set; }
    public Virial.Color TeamColor { get; set; }
    public Virial.Color? TeamFadeColor { get; set; }
    public Virial.Color RoleColor { get; set; }
    public TeamRevealType RevealType { get; set; }

    public void SetTeam(RoleTeam team)
    {
        TeamName = team.DisplayName;
        TeamColor = team.Color;
        RevealType = team.RevealType;
    }

    public void SetRole(RuntimeRole role)
    {
        RoleName = role.DisplayIntroRoleName;
        RoleBlurb = role.DisplayIntroBlurb;
        TeamFadeColor = role.Role.IsMadmate ? Virial.Color.ImpostorColor : null;
        RoleColor = role.Role.Color;
    }

    public void SetRole(DefinedRole role, string? displayIntroRoleName = null)
    {
        RoleName = displayIntroRoleName ?? role.DisplayName;
        RoleBlurb = role.DisplayIntroBlurb;
        TeamFadeColor = role.IsMadmate ? Virial.Color.ImpostorColor : null;
        RoleColor = role.Color;
    }

    internal GameShowIntroLocalEvent(RoleTeam team, RuntimeRole role)
    {
        SetTeam(team);
        SetRole(role);
    }
}
