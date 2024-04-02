using Il2CppInterop.Runtime.Injection;
using Mono.CSharp;
using Nebula.Behaviour;
using Nebula.Commands.Tokens;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands;

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
    List<ICommandLogText> allTexts = new();
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

/// <summary>
/// コマンドを実行するトークンです
/// </summary>
public class StatementCommandToken : ICommandToken
{
    private IReadOnlyArray<ICommandToken> tokens { get; init; }

    public StatementCommandToken(IReadOnlyArray<ICommandToken> arguments)
    {
        this.tokens = arguments;
    }

    CoTask<ICommandToken> ICommandToken.EvaluateHere(CommandEnvironment env)
    {
        return new CoImmediateTask<ICommandToken>(new StatementCommandToken(new ReadOnlyArray<ICommandToken>(tokens.Select(t => env.ArgumentTable.ApplyTo(t)))));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        var commandTask = CommandManager.CoExecute(tokens, env);
        return new CoChainedTask<IEnumerable<ICommandToken>, ICommandToken>(commandTask, result => result.AsEnumerable(env));
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var commandTask = CommandManager.CoExecute(tokens, env);
        return new CoChainedTask<T, ICommandToken>(commandTask, result => result.AsValue<T>(env));
    }

    IExecutable? ICommandToken.ToExecutable(CommandEnvironment env) => new CommandExecutable(this, env);
}


[NebulaPreLoad]
public class CommandManager
{

    static private Dictionary<string, ICommand> commandMap = new();
    static public bool TryGetCommand(string label, [MaybeNullWhen(false)] out ICommand command) => commandMap.TryGetValue(label, out command);
    static public void RegisterCommand(ICommand command,params string[] name)
    {
        foreach(var n in name) commandMap[n] = command;
    }


    static private IReadOnlyArray<ICommandToken> ParseCommand(Stack<string> args, ICommandLogger logger)
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
            List<(ICommandToken label, ICommandToken value)> members = new();
            while (args.Count > 0)
            {
                var val = ParseCommand(args, logger);
                if (val.Count == 0) break;
                if (val.Count != 1)
                {
                    logger.Push(CommandLogLevel.Error, "Tokens in struct tokens must be value.");
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

        List<ICommandToken> result = new();
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
                break;
            }
            else
                result.Add(
                    arg switch
                    {
                        "@a" => new ArrayCommandToken(new ReadOnlyArray<ICommandToken>(NebulaGameManager.Instance?.AllPlayerInfo().Select(p => new PlayerCommandToken(p)))),
                        _ => new StringCommandToken(arg)
                    }
                    );
        }
        return new ReadOnlyArray<ICommandToken>(result.ToArray());
    }
    static public CoTask<ICommandToken> CoExecute(string[] args, CommandEnvironment env)
    {
        var argStack = new Stack<string>(args.Reverse());
        return CoExecute(ParseCommand(argStack, env.Logger),  env);
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
                var header = args[0].AsValue<string>(env);
                yield return header.CoWait();

                if (header.IsFailed)
                {
                    env.Logger.PushError($"Unevaluable header.");
                    yield break;
                }

                if (TryGetCommand(header.Result, out var command))
                {
                    var commandTask = command.Evaluate(header.Result, args.Skip(1), env);
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
                else
                {
                    env.Logger.PushError($"No such command found \"{header.Result}\".");
                    yield break;
                }
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
    }

    Queue<Bubble> activeBubbles = new();
    Stack<Bubble> unusedBubblePool = new();

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
    }
}