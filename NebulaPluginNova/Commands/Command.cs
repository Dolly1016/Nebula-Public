using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Text;
using Nebula.Commands.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using TMPro;
using Virial.Command;
using Virial.Common;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace Nebula.Commands;

public static class CommandHelper
{
    static public bool DenyByPermission(CommandEnvironment env, Permission required, [MaybeNullWhen(false)]out CoTask<ICommandToken> task)
    {
        task = null;
        if (env.Executor.Test(required)) return false;

        env.Logger.PushError("You don't have the necessary permission to execute.");
        task = new CoImmediateErrorTask<ICommandToken>();
        return true;

    }
}
public class CommandLogText : ICommandLogText
{
    readonly static public Color InvalidColor = Color.Lerp(Color.red, Color.white, 0.4f);
    readonly static public Color WarningColor = Color.Lerp(new(1f,0.75f,0f), Color.white, 0.4f);

    public bool IsDirty{ get; set; }

    string currentText;
    string ICommandLogText.CurrentText { get => currentText.Color(logLevel switch
            {
                CommandLogLevel.Error => InvalidColor,
                CommandLogLevel.Warning => InvalidColor,
                _ => Color.white
            });
    }

    CommandLogLevel logLevel;
    CommandLogLevel ICommandLogText.LogLevel { get => logLevel; set { logLevel = value; IsDirty = true; } }

    void ICommandLogText.UpdateText(string newText)
    {
        currentText = newText;
        IsDirty = true;
    }

    public CommandLogText(CommandLogLevel level, string text)
    {
        this.logLevel = level;
        this.currentText = text;
        this.IsDirty = true;
    }
}

public class NebulaCommandLogger : ICommandLogger
{
    private bool isNew = true;
    public bool IsDirty => isNew || allTexts.Any(t => t.IsDirty);
    public void MarkUpdated()
    {
        AllTexts.Do(t => t.IsDirty = false);
        isNew = false;
    }

    public string ExecutedCommand { get; private init; }
    List<ICommandLogText> allTexts = [];
    IEnumerable<ICommandLogText> AllTexts => allTexts;

    ICommandLogText ICommandLogger.Push(CommandLogLevel logLevel, string message)
    {
        CommandLogText text = new(logLevel, message);
        allTexts.Add(text);
        return text;
    }

    void ICommandLogger.Remove(ICommandLogText text)
    {
        allTexts.Remove(text);
    }

    string ICommandLogger.ToLogString(CommandLogLevel logLevelMask)
    {
        return allTexts.Where(t => (int)(t.LogLevel & logLevelMask) != 0).Join(t => t.CurrentText, "\n");
    }

    public string Executed => ExecutedCommand;
    public NebulaCommandLogger(string executed)
    {
        ExecutedCommand = executed;
    }
}
public class CommandResource : INebulaResource
{
    ICommand command;
    public CommandResource(ICommand command)
    {
        this.command = command;
    }

    ICommand? INebulaResource.AsCommand() => command;
}

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public class CommandManager
{
    private class AddonCommand : ICommand, INebulaResource
    {
        public AddonCommand(string[] arguments, ICommandToken[] commands)
        {
            this.commands = commands;
            this.arguments = arguments;
        }

        ICommandToken[] commands;
        string[] arguments;

        IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
        {
            return [];
        }
        CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
        {
            if (arguments.Count != this.arguments.Length)
                return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " command requires " + this.arguments.Length + " argument(s).");

            return new CoImmediateTask<IEnumerable<(string, ICommandToken)>>(Helpers.Sequential(arguments.Count).Select(i => (this.arguments[i], arguments[i])))
                .SelectParallel(val => val.Item2.EvaluateHere(env).ChainFast(evaluated => (val.Item1, evaluated)))
                .Chain(args =>
                {
                    var newEnv = env.SwitchArgumentTable(new LetsCommandModifier(args, new ThroughCommandModifier()));
                    return new CoImmediateTask<IEnumerable<ICommandToken>>(commands).Do(c => c.ToExecutable(newEnv)?.CoExecute([]) ?? new CoImmediateErrorTask<ICommandToken>(env.Logger, "This command is broken. The execution was interrupted."));
                });
        }

        /// <summary>
        /// コマンドとして取得します。
        /// </summary>
        /// <returns></returns>
        ICommand? INebulaResource.AsCommand() => this;
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Commands");

        foreach (var addon in NebulaAddon.AllAddons)
        {
            string Prefix = addon.InZipPath + "Commands/";
            foreach (var entry in addon.Archive.Entries)
            {
                if (entry.FullName.StartsWith(Prefix) && entry.FullName.EndsWith(".command"))
                {
                    string path = System.IO.Path.GetFileNameWithoutExtension(entry.FullName.Substring(Prefix.Length));
                    var splittedPath = path.Split("/");

                    IResourceAllocator? allocator = addon;

                    string namespacePath = addon.Id.HeadLower();

                    if (splittedPath.Length > 1)
                    {
                        string childPath = splittedPath.Take(splittedPath.Length - 1).Join(null, "::");
                        allocator = (addon as IResourceAllocator).GetChildAllocator(childPath);
                        namespacePath += "::" + childPath;
                    }
                    var varAllocator = (allocator as IVariableResourceAllocator);

                    if (varAllocator == null) {
                        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, null, "Invalid namespace was specified. (command: " + splittedPath[^1] + ")" );
                        continue;
                    }

                    string[] arguments = [];
                    List<ICommandToken> commands = new();

                    using (var reader = new StreamReader(entry.Open()))
                    {
                        bool isFirst = true;

                        string? buffer = null;
                        while (true)
                        {
                            var str = reader.ReadLine();
                            if (str == null) break;

                            if (isFirst && str.StartsWith("*"))
                            {
                                arguments = str.Substring(1).Trim().Split(' ').Where(s => s.Length > 0).ToArray();
                            }
                            else
                            {
                                if(!str.StartsWith(" ") && buffer != null)
                                {
                                    commands.Add(new StatementCommandToken(CommandManager.ParseCommand(CommandManager.ParseRawCommand(buffer))));
                                    buffer = null;
                                }
                                if (buffer == null) buffer = str;
                                else buffer += str;
                            }

                            isFirst = false;
                        }

                        if(buffer != null) commands.Add(new StatementCommandToken(CommandManager.ParseCommand(CommandManager.ParseRawCommand(buffer))));
                    }

                    varAllocator.Register(splittedPath[^1], new AddonCommand(arguments, commands.ToArray()));
                    NebulaPlugin.Log.Print("Registered Command: " + splittedPath[^1] + " at " + namespacePath);

                }
            }
        }
 
    }

    static public bool TryGetCommand(string label, [MaybeNullWhen(false)] out ICommand command) => TryGetCommand(label, null, out command);
    
    static public bool TryGetCommand(string label, IResourceAllocator? defaultAllocator, [MaybeNullWhen(false)] out ICommand command) {
        command = NebulaResourceManager.GetResource(label, defaultAllocator ?? NebulaResourceManager.NebulaNamespace)?.AsCommand();
        return command != null;
    }

    static public void RegisterCommand(ICommand command, params string[] name)
    {
        foreach(var n in name) NebulaResourceManager.RegisterResource(n, new CommandResource(command));
    }

    static public string[] ParseRawCommand(string input)
    {
        return Regex.Replace(input, "(?<=[^:]):(?=[^:])", " : ").Replace(",", " , ")
            .Replace("(", " ( ").Replace(")", " ) ")
            .Replace("[", " [ ").Replace("]", " ] ")
            .Replace("{", " { ").Replace("}", " } ").Split(' ').Where(str => str.Length != 0).ToArray();
    }

    static public IReadOnlyArray<ICommandToken> ParseCommand(string[] args, ICommandLogger? logger = null) => ParseCommand(new Stack<string>(((IEnumerable<string>)args).Reverse()), logger);

    static private IReadOnlyArray<ICommandToken> ParseCommand(Stack<string> args, ICommandLogger? logger)
    {
        string ReplaceSpacedCharacter(string str, params char[] character)
        {
            foreach (char c in character)
                str = str.Replace(" " + c + " ", c.ToString());
            return str;
        }

        IReadOnlyArray<(ICommandToken label, ICommandToken value)> ParseStructElements(Stack<string> args)
        {
            IReadOnlyArray<ICommandToken>? storedLabel = null;
            List<(ICommandToken label, ICommandToken value)> members = [];
            while (args.Count > 0)
            {
                var val = ParseCommand(args, logger);
                if (val.Count == 0) break;
                if (val.Count != 1)
                {
                    logger?.Push(CommandLogLevel.Error, "Tokens in struct tokens must be value.");
                }

                var delimiter = args.Pop();
                if (delimiter is ":")
                    storedLabel = val;
                else if (delimiter is "," or "}")
                {
                    if (storedLabel != null)
                    {
                        members.Add((storedLabel[0], val[0]));
                        storedLabel = null;
                    }
                    else
                    {
                        //値が省略されている場合、trueとして扱う
                        members.Add((val[0], new BooleanCommandToken(true)));
                    }
                }

                if (delimiter is "}") break;
            }
            return new ReadOnlyArray<(ICommandToken label, ICommandToken value)>(members.ToArray());
        }

        string ParseTextToken()
        {
            List<string> sb = new();
            while (args.Count>0)
            {
                var str = args.Pop();

                if (str.EndsWith("\""))
                {
                    sb.Add(str.Substring(0, str.Length - 1));
                    break;
                }
                else
                    sb.Add(str);
            }

            
            return ReplaceSpacedCharacter(sb.Join(null," "),'(',')', '[', ']', '{', '}', ':', ',');
        }

        List<ICommandToken> result = [];
        while (args.Count > 0)
        {
            var arg = args.Pop();
            if (arg == "(")
                result.Add(new StatementCommandToken(ParseCommand(args, logger)));
            else if (arg == "[")
                result.Add(new ArrayCommandToken(ParseCommand(args, logger)));
            else if (arg == "{")
                result.Add(new StructCommandToken(ParseStructElements(args)));
            else if (arg is ")" or "]")
                return new ReadOnlyArray<ICommandToken>(result.ToArray());
            else if (arg is "," or ":" or "}")
            {
                args.Push(arg);
                return new ReadOnlyArray<ICommandToken>(result.ToArray());
            }
            else if(arg.StartsWith("\""))
            {
                args.Push(arg.Substring(1));
                result.Add(new StringCommandToken(ParseTextToken())); 
            }
            else
                result.Add(
                    arg switch
                    {
                        "@a" => new ArrayCommandToken(new ReadOnlyArray<ICommandToken>(NebulaGameManager.Instance?.AllPlayerInfo.OrderBy(p => p.PlayerId).Select(p => new PlayerCommandToken(p)))),
                        _ => new StringCommandToken(arg)
                    }
                    );
        }
        return new ReadOnlyArray<ICommandToken>(result.ToArray());
    }
    static public CoTask<ICommandToken> CoExecute(string[] args, CommandEnvironment env)
    {
        return CoExecute(ParseCommand(args, env.Logger),  env);
    }

    static public CoTask<ICommandToken> CoExecute(IReadOnlyArray<ICommandToken> args, CommandEnvironment env)
    {
        if (args.Count == 0)
        {
            env.Logger.PushError("Invalid input (Length: 0)");
            return new CoImmediateErrorTask<ICommandToken>(env.Logger);
        }
        else
        {
            IEnumerator CoExecuteCommand(CoBuiltInTask<ICommandToken> task)
            {
                var header = args[0].AsValue<ICommand>(env);


                var headerTxt = "?";
                if (args[0] is StringCommandToken sct) headerTxt = sct.Token;

                yield return header.CoWait();

                if (header.IsFailed)
                {
                    env.Logger.PushError($"Unevaluable command \"{headerTxt}\".");
                    yield break;
                }


                var commandTask = header.Result.Evaluate(headerTxt, args.Skip(1), env);
                yield return commandTask.CoWait();
                if (commandTask.IsFailed)
                {
                    env.Logger.PushError($"An error occurred while executing the command.");
                }
                else
                {
                    task.Result = commandTask.Result;
                }
                yield break;

            }
            CoBuiltInTask<ICommandToken> result = new(CoExecuteCommand);

            return result;
        }
    }
}

public class ConsoleShower : MonoBehaviour
{
    record Bubble(GameObject Holder, SpriteRenderer Background, TextMeshPro Text)
    {
        public ICommandLogger Logger { get; set; }
        public float Timer { get; set; } = 5f;
        public float Alpha { get; set; } = 1f;
    }

    Queue<Bubble> activeBubbles = [];
    Stack<Bubble> unusedBubblePool = [];
    public GameObject ConsoleInputHolder;

    static ConsoleShower() => ClassInjector.RegisterTypeInIl2Cpp<ConsoleShower>();

    public void Push(ICommandLogger logger)
    {
        Bubble newBubble = null!;
        if (unusedBubblePool.Count > 0)
            newBubble = unusedBubblePool.Pop();
        else
        {
            var holder = UnityHelper.CreateObject("Bubble", transform, UnityEngine.Vector3.zero);
            var background = UnityHelper.CreateObject<SpriteRenderer>("Background", holder.transform, UnityEngine.Vector3.zero);
            background.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
            background.drawMode = SpriteDrawMode.Sliced;
            background.tileMode = SpriteTileMode.Continuous;
            background.color = Color.white.RGBMultiplied(0.13f);

            TextMeshPro text = null!;
            new MetaWidgetOld.Text(new(TextAttributeOld.NormalAttrLeft) { Font = VanillaAsset.VersionFont, Size = new(6f, 4f), FontSize = 1.5f, AllowAutoSizing = false }) { PostBuilder = t => text = t }.Generate(holder, UnityEngine.Vector3.zero, out _);

            newBubble = new(holder, background, text);
        }


        newBubble.Logger = logger;
        newBubble.Alpha = 1f;
        newBubble.Timer = 5f;

        activeBubbles.Enqueue(newBubble);
    }

    public void LateUpdate()
    {

        float height = 0f;
        int num = 0;

        foreach (var bubble in activeBubbles.Reverse())
        {
            bubble.Holder.SetActive(true);

            var logger = bubble.Logger;
            if (logger.IsDirty)
            {
                bubble.Text.text = "<size=80%>" + logger.Executed.Color(Color.white.RGBMultiplied(0.78f)) + "</size>" + ("\n" + logger.ToLogString(CommandLogLevel.AllLevel)).Replace("\n", "\n ");

                bubble.Text.ForceMeshUpdate();

                logger.MarkUpdated();
            }

            float myHeight = bubble.Text.preferredHeight + 0.2f;
            float myWidth = bubble.Text.preferredWidth + 0.2f;

            bubble.Holder.transform.localPosition = new UnityEngine.Vector3(0f, height + myHeight * 0.5f);
            bubble.Background.transform.localPosition = new UnityEngine.Vector3((bubble.Text.preferredWidth - bubble.Text.rectTransform.sizeDelta.x) * 0.5f, 0f, 0f);
            bubble.Background.size = new UnityEngine.Vector2(myWidth - 0.06f, myHeight - 0.06f);
            height += myHeight;

            num++;

            if (height > 3f) break;
        }

        while (activeBubbles.Count > num) unusedBubblePool.Push(activeBubbles.Dequeue());

        foreach (var b in unusedBubblePool) b.Holder.gameObject.SetActive(false);

        foreach (var bubble in activeBubbles)
        {
            if (bubble.Timer > 0f) { bubble.Timer -= Time.deltaTime; }
            else if (bubble.Alpha > 0f) bubble.Alpha = Mathf.Clamp01(bubble.Alpha - Time.deltaTime * 0.7f);

            UnityEngine.Color alphaColor = new(1f, 1f, 1f, ConsoleInputHolder.active ? 1f : bubble.Alpha);

            bubble.Text.color = alphaColor;
            bubble.Background.color = alphaColor.RGBMultiplied(0.13f);
        }
    }
}