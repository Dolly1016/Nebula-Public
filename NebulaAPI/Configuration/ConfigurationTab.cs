using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;

namespace Virial.Configuration;

public class ConfigurationTab
{
    private static ConfigurationTab tabSetting = new ConfigurationTab(0x01, "options.tab.setting", new Color(0.75f, 0.75f, 0.75f));
    private static ConfigurationTab tabCrewmate = new ConfigurationTab(0x02, "options.tab.crewmate", new(Palette.CrewmateBlue));
    private static ConfigurationTab tabImpostor = new ConfigurationTab(0x04, "options.tab.impostor", new(Palette.ImpostorRed));
    private static ConfigurationTab tabNeutral = new ConfigurationTab(0x08, "options.tab.neutral", new Color(244f / 255f, 211f / 255f, 53f / 255f));
    private static ConfigurationTab tabGhost = new ConfigurationTab(0x10, "options.tab.ghost", new Color(150f / 255f, 150f / 255f, 150f / 255f));
    private static ConfigurationTab tabModifier = new ConfigurationTab(0x20, "options.tab.modifier", new Color(255f / 255f, 255f / 255f, 243f / 255f));
    private static List<ConfigurationTab> allTab = [tabSetting, tabCrewmate, tabImpostor, tabNeutral, tabGhost, tabModifier];

    public static ConfigurationTab Settings => tabSetting;
    public static ConfigurationTab CrewmateRoles => tabCrewmate;
    public static ConfigurationTab ImpostorRoles => tabImpostor;
    public static ConfigurationTab NeutralRoles => tabNeutral;
    public static ConfigurationTab GhostRoles => tabGhost;
    public static ConfigurationTab Modifiers => tabModifier;

    public int AsBit => bitFlag;

    private int bitFlag;
    private string translateKey { get; init; }
    public Color Color { get; private init; }
    public ConfigurationTab(int bitFlag, string translateKey, Color color)
    {
        this.bitFlag = bitFlag;
        allTab.Add(this);
        this.translateKey = translateKey;
        Color = color;
    }


    private static byte ToByte(float f) => (byte)(Mathf.Clamp01(f) * 255);
    private static string ColorBegin(Color color) => string.Format("<color=#{0:X2}{1:X2}{2:X2}{3:X2}>", ToByte(color.R), ToByte(color.G), ToByte(color.B), ToByte(color.A));
    private static string ColorEnd() => "</color>";
    public string DisplayName { get => NebulaAPI.Language.Translate(translateKey).Replace("[", ColorBegin(Color)).Replace("]", ColorEnd()); }

    public static ReadOnlyCollection<ConfigurationTab> AllTab { get => allTab.AsReadOnly(); }

    private static ConfigurationTab FromRoleCategory(RoleCategory roleCategory)
    {
        switch (roleCategory)
        {
            case RoleCategory.CrewmateRole:
                return CrewmateRoles;
            case RoleCategory.ImpostorRole:
                return ImpostorRoles;
            case RoleCategory.NeutralRole:
                return NeutralRoles;
        }
        return Settings;
    }

    public static implicit operator ConfigurationTab(RoleCategory category) => FromRoleCategory(category);

}
