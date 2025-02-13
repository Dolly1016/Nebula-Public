using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using System.ComponentModel;

namespace Nebula.Roles.Crewmate;

internal class JekyllAndHyde : DefinedRoleTemplate, DefinedRole
{
    private JekyllAndHyde() : base("jekyllAndHyde", Virial.Color.White, RoleCategory.CrewmateRole, Crewmate.MyTeam, [])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagDifficult);
    }

    private string JekyllDisplayName => Language.Translate("role.jekyllAndHyde.name.jekyll");
    private string HydeDisplayName => Language.Translate("role.jekyllAndHyde.name.hyde");
    private string JekyllDisplayShort => Language.Translate("role.jekyllAndHyde.short.jekyll");
    private string HydeDisplayShort => Language.Translate("role.jekyllAndHyde.short.hyde");
    private string JAndHCombinationName => Language.Translate("role.jekyllAndHyde.name.combination");
    private string JAndHCombinationShort => Language.Translate("role.jekyllAndHyde.short.combination");
    string DefinedRole.DisplayIntroBlurb => Language.Translate("role.jekyllAndHyde.blurb").Replace("%J%", Palette.CrewmateBlue.ToTextColor()).Replace("%H%", Palette.ImpostorRed.ToTextColor()).Replace("%R%", "</color>");
    string DefinedAssignable.DisplayName => JAndHCombinationName.Replace("%J%", JekyllDisplayName).Replace("%H%", HydeDisplayName);
    string DefinedAssignable.DisplayColoredName => JAndHCombinationName.Replace("%J%", JekyllDisplayName.Color(Palette.CrewmateBlue)).Replace("%H%", HydeDisplayName.Color(Palette.ImpostorRed));
    string DefinedCategorizedAssignable.DisplayShort => JAndHCombinationShort.Replace("%J%", JekyllDisplayShort).Replace("%H%", HydeDisplayShort);
    string DefinedCategorizedAssignable.DisplayColoredShort => JAndHCombinationShort.Replace("%J%", JekyllDisplayShort.Color(Palette.CrewmateBlue)).Replace("%H%", HydeDisplayShort.Color(Palette.ImpostorRed));
    //static private FloatConfiguration EchoCooldownOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    //static private FloatConfiguration EchoRangeOption = NebulaAPI.Configurations.Configuration("options.role.echo.echoRange", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static public JekyllAndHyde MyRole = new JekyllAndHyde();
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;


        public Instance(GamePlayer player, int[] argument) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
        }

        public bool AmJekyll = true;
        void OnMeeting(MeetingEndEvent ev)
        {
            AmJekyll = !AmJekyll;
        }

        string RuntimeAssignable.DisplayName => AmJekyll ? Language.Translate("role.jekyllAndHyde.name.jekyll") : Language.Translate("role.jekyllAndHyde.name.hyde");
        string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color(AmJekyll ? Palette.CrewmateBlue : Palette.ImpostorRed);
    }
}