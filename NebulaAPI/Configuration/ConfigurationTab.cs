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

public class ConfigurationTab : IBit32
{
    internal static ConfigurationTab tabSetting;
    internal static ConfigurationTab tabCrewmate;
    internal static ConfigurationTab tabImpostor;
    internal static ConfigurationTab tabNeutral;
    internal static ConfigurationTab tabGhost;
    internal static ConfigurationTab tabModifier;
    internal static List<ConfigurationTab> allTab;

    public static ConfigurationTab Settings => tabSetting;
    public static ConfigurationTab CrewmateRoles => tabCrewmate;
    public static ConfigurationTab ImpostorRoles => tabImpostor;
    public static ConfigurationTab NeutralRoles => tabNeutral;
    public static ConfigurationTab GhostRoles => tabGhost;
    public static ConfigurationTab Modifiers => tabModifier;

    public uint AsBit => bitFlag;

    private uint bitFlag;
    private string translateKey { get; init; }
    public Color Color { get; private init; }
    internal ConfigurationTab(uint bitFlag, string translateKey, Color color)
    {
        this.bitFlag = bitFlag;
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
