using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Virial.Runtime;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public static class DebugTools
{
    static DebugTools() => saver.TrySave();
    
    private static DataSaver saver = new("DevSettings");

    private static BooleanDataEntry debugMode = new("DebugMode", saver, false, shouldWrite: false);
    public static bool DebugMode => debugMode.Value;
    private static readonly List<IDebugVariable> debugVariables = [];
    private static readonly DebugValueEntry<bool> showConfigurationId = new BooleanDataEntry("ShowConfigurationId", saver, false, shouldWrite: false);
    private static readonly DebugValueEntry<bool> releaseAllAchievement = new BooleanDataEntry("ReleaseAllAchievement", saver, false, shouldWrite: false);
    private static readonly DebugValueEntry<bool> lockAllAchievement = new BooleanDataEntry("LockAllAchievement", saver, false, shouldWrite: false);
    private static readonly DebugValueEntry<bool> allowVanillaLog = new BooleanDataEntry("AllowVanillaLog", saver, false, shouldWrite: false);
    private static readonly DebugValueEntry<bool> writeAllAchievementsData = new BooleanDataEntry("WriteAllAchievementsData", saver, false, shouldWrite: false);
    private static readonly DebugValueEntry<bool> showCostumeMetadata = new BooleanDataEntry("ShowCostumeMetadata", saver, false, shouldWrite: false);
    public static bool ShowConfigurationId => DebugMode && showConfigurationId.Value;
    public static bool WriteAllAchievementsData => DebugMode && writeAllAchievementsData.Value;
    public static bool ReleaseAllAchievement => DebugMode && releaseAllAchievement.Value;
    public static bool LockAllAchievement => DebugMode && lockAllAchievement.Value;
    public static bool ShowCostumeMetadata => DebugMode && showCostumeMetadata.Value;
    public static bool AllowVanillaLog => DebugMode && allowVanillaLog.Value;

    internal static void RegisterDebugVariable(IDebugVariable variable) => debugVariables.Add(variable);
    internal static IEnumerable<IDebugVariable> DebugVariables => debugVariables;

    public static Virial.Media.GUIWidget GetEditorWidget()
    {
        return GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center, debugVariables.Select(v => GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
            GUI.API.RawText(Virial.Media.GUIAlignment.Left, Virial.Text.AttributeAsset.OptionsTitle, v.Name),
            new Nebula.Modules.GUIWidget.GUITextField(Virial.Media.GUIAlignment.Center, new(3f, 0.4f)) { DefaultText = v.ToString(), IsSharpField = false, WithMaskMaterial = true, EnterAction = (str) =>
            {
                string old = v.ToString();

                if (str.Length == 0)
                {
                    NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.System, $"Canceled changing debug variable. (Name: {v.Name}, Current: {old})");
                    return true;
                }

                if (old != str)
                {
                    if (!v.Deserialize(str))
                        NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.System, $"Failed to deserialize. (Name: {v.Name}, Input: {str})");
                    else
                        NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.System, $"Changed debug variable. (Name: {v.Name}, Old: {old}, Current: {v.ToString()})");
                }
                return true;
            }, MaxLines = 1 }
            )
        ));
    }
}

internal interface IDebugVariable
{
    bool Deserialize(string value);
    string ToString();
    string Name { get; }
}

internal class DebugValueEntry<T> : IDebugVariable where T : notnull
{
    public string Name => entry.Name;
    private DataEntry<T> entry;
    public T Value => entry.Value;
    public DebugValueEntry(DataEntry<T> entry) {
        this.entry = entry;
        DebugTools.RegisterDebugVariable(this);
    }

    bool IDebugVariable.Deserialize(string value)
    {
        try
        {
            entry.Value = entry.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
    override public string ToString() => (entry as IDataEntry).Serialize();

    static public implicit operator DebugValueEntry<T>(DataEntry<T> entry) => new(entry);
}
internal class DebugVariable<T> : IDebugVariable
{
    private string name;
    public string Name => name;
    private T value;
    public T Value => value;
    private Func<string, T> converter;
    private Func<T, string> serializer;
    public override string? ToString()
    {
        return serializer.Invoke(value);
    }
    public bool Deserialize(string value)
    {
        try
        {
            this.value = converter.Invoke(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public DebugVariable(string name, T value, Func<string, T> converter, Func<T, string> serializer)
    {
        this.value = value;
        this.converter = converter;
        this.serializer = serializer;
        DebugTools.RegisterDebugVariable(this);
    }

    static unsafe bool GetPrimitiveDebugVariable<U>((string name, T value) value, Func<string, U> converter, ref DebugVariable<T> result) {
        if (typeof(T) == typeof(U)) {
            result = Unsafe.As<DebugVariable<T>>(new DebugVariable<U>(value.name, (U)((object)value.value!), converter, num => num!.ToString()!));
            return true;
        }
        return false;
    }

    static public implicit operator DebugVariable<T>((string name, T value) value)
    {
        unsafe
        {
            DebugVariable<T> variable = null!;
            if (GetPrimitiveDebugVariable(value, int.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, byte.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, short.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, uint.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, long.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, ushort.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, ulong.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, float.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, double.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, bool.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, v => v, ref variable)) return variable;

            if (GetPrimitiveDebugVariable(value, sbyte.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, char.Parse, ref variable)) return variable;
            if (GetPrimitiveDebugVariable(value, decimal.Parse, ref variable)) return variable;
        }

        throw new Exception("Reference type requires a converter.");
    }

    static public implicit operator T(DebugVariable<T> variable) => variable.Value;
}
