using Nebula.Behavior;
using Rewired.ControllerExtensions;
using Steamworks;
using Virial.Runtime;

namespace Nebula.Utilities;

public interface IKeyAssignment
{
    static protected readonly List<IKeyAssignment> allKeyAssignments = [];
    static public IEnumerable<IKeyAssignment> AllKeyAssignments => allKeyAssignments;
    public string DisplayName { get; }
    public KeyCode KeyInput { get; set; }
    public KeyCode DefaultKey { get; }
    public string TranslationKey { get; }
}

public class KeyAssignment : IKeyAssignment
{
    static private DataSaver KeyAssignmentSaver = new("KeyMapping");

    private DataEntry<int> dataEntry;
    public KeyCode KeyInput { get => (KeyCode)dataEntry.Value; set => dataEntry.Value = (int)value; }
    public KeyCode DefaultKey { get; private set; }
    public string TranslationKey { get; private set; }
    public KeyAssignment(string translationKey,KeyCode defaultKey)
    {
        TranslationKey = translationKey;
        DefaultKey = defaultKey;
        dataEntry = new IntegerDataEntry(translationKey,KeyAssignmentSaver,(int)defaultKey);

        IKeyAssignment.allKeyAssignments.Add(this);
    }

    public string DisplayName => Language.Translate("input." + TranslationKey);
}

public class VirtualInput
{
    private Func<KeyCode>[] assignments;
    private IEnumerable<KeyCode>? assignmentEnumerator = null;
    public VirtualInput(params Func<KeyCode>[] assignments)
    {
        this.assignments = assignments;
    }

    public VirtualInput(IEnumerable<KeyCode> enumerable,params Func<KeyCode>[] assignments)
    {
        this.assignmentEnumerator = enumerable;
        this.assignments = assignments;
    }

    public VirtualInput(KeyCode keyCode)
    {
        assignments = [() => keyCode];
    }

    public IEnumerable<KeyCode> AllKeyCode()
    {
        if (assignmentEnumerator != null) foreach (var key in assignmentEnumerator) yield return key;
        foreach (var func in assignments) yield return func.Invoke();
    }

    public bool KeyDownInGame => AllKeyCode().Any(a => NebulaInput.GetKeyDown(a));
    public bool KeyUpInGame => AllKeyCode().Any(a => NebulaInput.GetKeyUp(a));
    public bool KeyInGame => AllKeyCode().Any(a => NebulaInput.GetKey(a));

    public bool KeyDownForAction => AllKeyCode().Any(a => NebulaInput.GetKeyDownForAction(a));
    public bool KeyUpForAction => AllKeyCode().Any(a => NebulaInput.GetKeyUpForAction(a));
    public bool KeyForAction => AllKeyCode().Any(a => NebulaInput.GetKeyForAction(a));

    public bool KeyDown => AllKeyCode().Any(a => Input.GetKeyDown(a));
    public bool KeyUp => AllKeyCode().Any(a => Input.GetKeyUp(a));
    public bool KeyState => AllKeyCode().Any(a => Input.GetKey(a));

    public KeyCode TypicalKey => assignments[0].Invoke();

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
    }

}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class NebulaInput
{
    public static bool SomeUiIsActive => (ControllerManager.Instance && ControllerManager.Instance.CurrentUiState?.BackButton != null) || NebulaManager.Instance.HasSomeUI || TextField.AnyoneValid;
    private static bool SomeInputUiIsActive
    {
        get
        {
            try
            {
                return (HudManager.InstanceExists && HudManager.Instance.Chat.freeChatField.textArea.hasFocus) || TextField.AnyoneValid;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool GetKeyDown(KeyCode keyCode)
    {
        if (SomeUiIsActive) return false;
        return Input.GetKeyDown(keyCode);
    }

    public static bool GetKeyUp(KeyCode keyCode)
    {
        if (SomeUiIsActive) return false;
        return Input.GetKeyUp(keyCode);
    }

    public static bool GetKey(KeyCode keyCode)
    {
        if (SomeUiIsActive) return false;
        return Input.GetKey(keyCode);
    }

    public static bool GetKeyDownForAction(KeyCode keyCode)
    {
        if (SomeInputUiIsActive) return false;
        return Input.GetKeyDown(keyCode);
    }

    public static bool GetKeyUpForAction(KeyCode keyCode)
    {
        if (SomeInputUiIsActive) return false;
        return Input.GetKeyUp(keyCode);
    }

    public static bool GetKeyForAction(KeyCode keyCode)
    {
        if (SomeInputUiIsActive) return false;
        return Input.GetKey(keyCode);
    }

    private static Dictionary<Virial.Compat.VirtualKeyInput, VirtualInput> modInput = [];

    static public VirtualInput GetInput(Virial.Compat.VirtualKeyInput type) => modInput[type];

    static NebulaInput()
    {
        IEnumerable<KeyCode> GetVanillaKeyCode(int actionId)
        {
            foreach (var action in Rewired.ReInput.mapping.GetKeyboardMapInstanceSavedOrDefault(0, 0, 0).GetButtonMapsWithAction(actionId)) yield return action.keyCode;
        }

        Func<KeyCode> GetModKeyCodeGetter(string translationKey, KeyCode defaultKey)
        {
            var assignment = new KeyAssignment(translationKey, defaultKey);
            return () => assignment.KeyInput;
        }

        modInput[Virial.Compat.VirtualKeyInput.Kill] = new(GetVanillaKeyCode(8), GetModKeyCodeGetter("kill", KeyCode.Q));
        modInput[Virial.Compat.VirtualKeyInput.Vent] = new(GetVanillaKeyCode(50));
        modInput[Virial.Compat.VirtualKeyInput.Ability] = new(GetModKeyCodeGetter("ability", KeyCode.F));
        modInput[Virial.Compat.VirtualKeyInput.SecondaryAbility] = new(GetModKeyCodeGetter("secondaryAbility", KeyCode.G));
        modInput[Virial.Compat.VirtualKeyInput.SidekickAction] = new(GetModKeyCodeGetter("sidekickAction", KeyCode.X));
        modInput[Virial.Compat.VirtualKeyInput.FreeplayAction] = new(GetModKeyCodeGetter("freeplayAction", KeyCode.Z));
        modInput[Virial.Compat.VirtualKeyInput.AidAction] = new(GetModKeyCodeGetter("aidAction", KeyCode.LeftShift));
        modInput[Virial.Compat.VirtualKeyInput.PerkAction1] = new(GetModKeyCodeGetter("perkAction1", KeyCode.Alpha1));
        modInput[Virial.Compat.VirtualKeyInput.PerkAction2] = new(GetModKeyCodeGetter("perkAction2", KeyCode.Alpha2));
        modInput[Virial.Compat.VirtualKeyInput.Command] = new(GetModKeyCodeGetter("command", KeyCode.LeftControl));
        modInput[Virial.Compat.VirtualKeyInput.Screenshot] = new(GetModKeyCodeGetter("screenshot", KeyCode.P));
        modInput[Virial.Compat.VirtualKeyInput.Mute] = new(GetModKeyCodeGetter("mute", KeyCode.M));
        modInput[Virial.Compat.VirtualKeyInput.VCFilter] = new(GetModKeyCodeGetter("vcFilter", KeyCode.B));
        modInput[Virial.Compat.VirtualKeyInput.Help] = new(GetModKeyCodeGetter("help", KeyCode.H));
        modInput[Virial.Compat.VirtualKeyInput.Spectator] = new(GetModKeyCodeGetter("spectator", KeyCode.C));
        modInput[Virial.Compat.VirtualKeyInput.SpectatorRight] = new(GetModKeyCodeGetter("spectatorRight", KeyCode.Period));
        modInput[Virial.Compat.VirtualKeyInput.SpectatorLeft] = new(GetModKeyCodeGetter("spectatorLeft", KeyCode.Comma));
        modInput[Virial.Compat.VirtualKeyInput.Stamp] = new(GetModKeyCodeGetter("stamp", KeyCode.R));
    }
}
