using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;

namespace Nebula.Commands.Variations;

public class GUICommonStructure
{
    public GUIAlignment alignment = GUIAlignment.TopLeft;

    public static CommandStructureConverter<GUICommonStructure> Converter = new CommandStructureConverter<GUICommonStructure>()
        .Add<string>("alignment", (structure,val) =>
        {
            structure.alignment = val switch
            {
                "left" => GUIAlignment.Left,
                "right" => GUIAlignment.Right,
                "top" => GUIAlignment.Top,
                "bottom" => GUIAlignment.Bottom,
                "center" => GUIAlignment.Center,
                _ => GUIAlignment.Left
            };
        });
}

public class GuiHolderCommand : ICommand
{
    public class GUIHolderStructure : GUICommonStructure
    {
        public bool isVertical = true;
        public IEnumerable<GUIWidget>? inner = null;

        public static CommandStructureConverter<GUIHolderStructure> Converter = new CommandStructureConverter<GUIHolderStructure>()
            .Add<bool>("isVertical", (structure, val) => structure.isVertical = val)
            .Add<bool>("isHorizontal", (structure, val) => structure.isVertical = !val)
            .AddCollection<GUIWidget>("inner", (structure, val) => structure.inner = val)
            .Inherit(GUICommonStructure.Converter);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <structure>");

        return arguments[0].AsStructure(env).ConvertTo<GUIHolderStructure>(GUIHolderStructure.Converter, new(), env).ChainFast<ICommandToken, GUIHolderStructure>(
            structure => new ObjectCommandToken<GUIWidget>(structure.isVertical ? GUI.API.VerticalHolder(structure.alignment, structure.inner ?? []) : GUI.API.HorizontalHolder(structure.alignment, structure.inner ?? []))
            );
    }
}

public class GuiTextCommand : ICommand
{
    public class GUITextStructure : GUICommonStructure
    {
        public TextComponent? component;
        public string style = "";

        public static CommandStructureConverter<GUITextStructure> Converter = new CommandStructureConverter<GUITextStructure>()
            .Add<string>("localizedText", (structure, val) => structure.component = new TranslateTextComponent(val))
            .Add<string>("rawText", (structure, val) => structure.component = new RawTextComponent(val))
            .Add<string>("style", (structure, val) => structure.style = val)
            .Inherit(GUICommonStructure.Converter);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " {structure}");

        return arguments[0].AsStructure(env).ConvertTo<GUITextStructure>(GUITextStructure.Converter, new(), env).ChainFast<ICommandToken, GUITextStructure>(
            structure => new ObjectCommandToken<GUIWidget>(GUI.Instance.Text(structure.alignment,SerializableDocument.GetAttribute(structure.style), structure.component ?? new RawTextComponent("")))
            );
    }
}

public class GuiButtonCommand : ICommand
{
    public class GUIButtonStructure : GuiTextCommand.GUITextStructure
    {
        public IExecutable? onClick;

        public static CommandStructureConverter<GUIButtonStructure> Converter = new CommandStructureConverter<GUIButtonStructure>()
            .Add<IExecutable>("action", (structure, val) => structure.onClick = val)
            .Inherit(GuiTextCommand.GUITextStructure.Converter);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " {structure}");

        return arguments[0].AsStructure(env).ConvertTo<GUIButtonStructure>(GUIButtonStructure.Converter, new(), env).ChainFast<ICommandToken, GUIButtonStructure>(
            structure => new ObjectCommandToken<GUIWidget>(GUI.Instance.Button(structure.alignment, SerializableDocument.GetAttribute(structure.style), structure.component ?? new RawTextComponent(""), _ => NebulaManager.Instance.StartCoroutine(structure.onClick?.CoExecute([]).CoWait().HighSpeedEnumerator().WrapToIl2Cpp())))
            );
    }
}

public class GuiArrayerCommand : ICommand
{
    public class GuiArrayerStructure : GUICommonStructure
    {
        public IEnumerable<GUIWidget>? inner = null;
        public int perRow;

        public static CommandStructureConverter<GuiArrayerStructure> Converter = new CommandStructureConverter<GuiArrayerStructure>()
            .AddCollection<GUIWidget>("inner", (structure, val) => structure.inner = val)
            .Add<int>("perRow", (structure, val) => structure.perRow = Mathf.Max(val,0))
            .Inherit(GUICommonStructure.Converter);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " {structure}");

        return arguments[0].AsStructure(env).ConvertTo(GuiArrayerStructure.Converter, new(), env).ChainFast(
            (Func<GuiArrayerStructure, ICommandToken>)(            structure =>
            {
                List<GUIWidget> widgets = new();
                List<GUIWidget> holders = new();
                foreach (var i in structure.inner ?? [])
                {
                    widgets.Add(i);
                    if (widgets.Count >= structure.perRow)
                    {
                        holders.Add(GUI.API.HorizontalHolder(structure.alignment, widgets.ToArray()));
                        widgets.Clear();
                    }
                }
                if (widgets.Count > 0) holders.Add(GUI.API.HorizontalHolder(structure.alignment, widgets.ToArray()));
                return new ObjectCommandToken<GUIWidget>(GUI.Instance.VerticalHolder(structure.alignment, holders));
            })
            );
    }
}