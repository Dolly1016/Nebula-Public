using Il2CppInterop.Runtime.Injection;
using Mono.CSharp;
using Nebula.Behaviour;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace Nebula.Commands;

public class ConsoleBubble
{
    public string Executed { get; private set; }
    private Stack<string> responseStack = new();
    private string? responseText = null;
    public string PeekResponse() => responseStack.Peek();
    public string PopResponse()
    {
        IsDirty = true;
        return responseStack.Pop();
    }
    public void PushResponse(string response)
    {
        IsDirty = true;
        responseStack.Push(response);
    }
    public void UpdateResponse(string response)
    {
        PopResponse();
        PushResponse(response);
    }
    public bool IsDirty { get => responseText == null; set => responseText = null; }
    
    public string Response { get { 
            responseText ??= responseStack.Reverse().Join(null, "\n");
            return responseText;
        }
    }

    public ConsoleBubble(string executed)
    {
        this.Executed = executed;
    }
}

public interface ICommandArgument
{
    string? GetString(ConsoleBubble log);
    int? GetInteger(ConsoleBubble log);
    IEnumerable<string> GetStringEnumerator(ConsoleBubble log) { yield break; }
    IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log) { yield break; }

    //引数を評価するコルーチンを返します。評価済みの引数は各種の値を正しく取得することができます。
    IEnumerator Evaluate(ConsoleBubble log);
}

public class EmptyCommandArgument : ICommandArgument
{
    public string? GetString(ConsoleBubble log) => null;
    public int? GetInteger(ConsoleBubble log) => null;
    public IEnumerable<string> GetStringEnumerator(ConsoleBubble log) { yield break; }
    public IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log) { yield break; }
    public IEnumerator Evaluate(ConsoleBubble log) { yield break; }
}

public class StaticCommandArgument : ICommandArgument
{
    string myStr;

    public string? GetString(ConsoleBubble log) => myStr;
    public int? GetInteger(ConsoleBubble log) => int.TryParse(myStr,out int val) ? val : null;
    public IEnumerable<string> GetStringEnumerator(ConsoleBubble log) { yield return myStr; }
    public IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log) { int? val = GetInteger(log); if (val.HasValue) yield return val.Value; }
    public StaticCommandArgument(string text)
    {
        myStr = text;
    }

    public IEnumerator Evaluate(ConsoleBubble log)
    {
        yield break;
    }
}

public class InnerCommandArgument : ICommandArgument
{
    ICommandArgument[] expr;
    ICommandArgument? result = null;

    public string? GetString(ConsoleBubble log) => result?.GetString(log);
    public int? GetInteger(ConsoleBubble log) => result?.GetInteger(log);
    public IEnumerable<string> GetStringEnumerator(ConsoleBubble log) => (result ?? new EmptyCommandArgument()).GetStringEnumerator(log);
    public IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log) => (result ?? new EmptyCommandArgument()).GetIntegerEnumerator(log);
    public InnerCommandArgument(ICommandArgument[] expr)
    {
        this.expr = expr;
    }

    public IEnumerator Evaluate(ConsoleBubble log)
    {
        if (result != null) yield break;
        Reference<ICommandArgument> reference = new();
        yield return CommandManager.Execute(log, expr, reference);
        result = reference.Value;
    }
}

public class EnumerableCommandArgument : ICommandArgument
{
    ICommandArgument[] expr;

    public string? GetString(ConsoleBubble log) => null;
    public int? GetInteger(ConsoleBubble log) => null;
    public IEnumerable<string> GetStringEnumerator(ConsoleBubble log)
    {
        foreach(var arg in expr) foreach (var val in arg.GetStringEnumerator(log)) yield return val;
    }
    public IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log)
    {
        foreach (var arg in expr) foreach (var val in arg.GetIntegerEnumerator(log)) yield return val;
    }
    public EnumerableCommandArgument(ICommandArgument[] expr)
    {
        this.expr = expr;
    }

    public IEnumerator Evaluate(ConsoleBubble log)
    {
        yield return Effects.All(expr.Select(a => a.Evaluate(log).WrapToIl2Cpp()).ToArray());
    }
}

public class BuiltInCommandArgument : ICommandArgument
{
    public IEnumerable<int>? IntegerEnumerator = null;
    public IEnumerable<string>? StringEnumerator = null;
    public BuiltInCommandArgument() { }

    public string? GetString(ConsoleBubble log) => null;
    public int? GetInteger(ConsoleBubble log) => null;
    public IEnumerable<string> GetStringEnumerator(ConsoleBubble log)
    {
        foreach(var val in StringEnumerator ?? IntegerEnumerator?.Select(v=>v.ToString()) ?? Array.Empty<string>())
            yield return val;
    }
    public IEnumerable<int> GetIntegerEnumerator(ConsoleBubble log) {
        foreach (var val in IntegerEnumerator ?? Array.Empty<int>())
            yield return val;
    }

    public IEnumerator Evaluate(ConsoleBubble log) { yield break; }
}

public class Command
{
    //出力先バブルと引数、返り値の格納先を渡して評価の手続きを返す
    Func<ConsoleBubble, ICommandArgument[], Reference<ICommandArgument>, IEnumerator> Executor;

    public Command(Func<ConsoleBubble, ICommandArgument[], Reference<ICommandArgument>, IEnumerator> executor)
    {
        Executor = executor;
    }

    public IEnumerator Execute(ConsoleBubble bubble, ICommandArgument[] args, Reference<ICommandArgument> reference)
    {
        //引数を評価する
        yield return Effects.All(args.Select(a => a.Evaluate(bubble).WrapToIl2Cpp()).ToArray());
        yield return Executor.Invoke(bubble, args, reference);
    }
}

[NebulaPreLoad]
public class CommandManager
{
    readonly static public Color InvalidColor = Color.Lerp(Color.red, Color.white, 0.4f);

    static private Dictionary<string, Command> commandMap = new();

    static public void RegisterCommand(Command command,params string[] name)
    {
        foreach(var n in name) commandMap[n] = command;
    }

    static private ICommandArgument[] ParseCommand(Stack<string> args)
    {
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
            return sb.Join(null," ").Replace(" ( ", "(").Replace(" ) ", ")").Replace(" [ ", "[").Replace(" ] ", "]");
        }

        List<ICommandArgument> result = new();
        while (args.Count > 0)
        {
            var arg = args.Pop();
            if (arg == "(")
                result.Add(new InnerCommandArgument(ParseCommand(args)));
            else if (arg == "[")
                result.Add(new EnumerableCommandArgument(ParseCommand(args)));
            else if(arg is ")" or "]")
                return result.ToArray();
            else if(arg.StartsWith("\""))
            {
                args.Push(arg.Substring(1));
                result.Add(new StaticCommandArgument(ParseTextToken()));
            }
            else
                result.Add(
                    arg switch
                    {
                        "@a" => new BuiltInCommandArgument() { IntegerEnumerator = PlayerControl.AllPlayerControls.GetFastEnumerator().Select(p => (int)p.PlayerId) },
                        _ => new StaticCommandArgument(arg)
                    }
                    );
        }
        return result.ToArray();
    }
    static public IEnumerator Execute(ConsoleBubble bubble, string[] args, Reference<ICommandArgument> result)
    {
        var argStack = new Stack<string>(args.Reverse());
        yield return Execute(bubble, ParseCommand(argStack), result);
    }

    static public IEnumerator Execute(ConsoleBubble bubble, ICommandArgument[] args, Reference<ICommandArgument> result)
    {
        if (args.Length == 0)
        {
            bubble.PushResponse("Invalid input".Color(InvalidColor));
            yield break;
        }
        else
        {
            //ヘッダーのみ評価する
            yield return args[0].Evaluate(bubble);
            string? key = args[0].GetString(bubble);

            if (key != null && commandMap.TryGetValue(key!, out var command))
            {
                //引数の評価は内部で行われる
                yield return command.Execute(bubble, args.Skip(1).ToArray(), result);
            }
            else
            {
                if (key != null)
                    bubble.PushResponse($"No such command found \"{key!}\".".Color(InvalidColor));
                else
                    bubble.PushResponse($"Header must be not null.".Color(InvalidColor));

                yield break;
            }
        }
    }
}

public class ConsoleShower : MonoBehaviour
{
    record Bubble(GameObject holder, SpriteRenderer background, TextMeshPro text, Reference<ConsoleBubble> reference);

    Queue<Bubble> activeBubbles = new();
    Stack<Bubble> unusedBubblePool = new();

    static ConsoleShower() => ClassInjector.RegisterTypeInIl2Cpp<ConsoleShower>();

    public void PushBubble(ConsoleBubble bubble)
    {
        Bubble newBubble = null!;
        if (unusedBubblePool.Count > 0)
            newBubble = unusedBubblePool.Pop();
        else
        {
            var holder = UnityHelper.CreateObject("Bubble", transform, Vector3.zero);
            var background = UnityHelper.CreateObject<SpriteRenderer>("Background", holder.transform, Vector3.zero);
            background.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
            background.drawMode = SpriteDrawMode.Sliced;
            background.tileMode = SpriteTileMode.Continuous;
            background.color = Color.white.RGBMultiplied(0.13f);

            TextMeshPro text = null!;
            new MetaWidgetOld.Text(new(TextAttributeOld.NormalAttrLeft) { Font = VanillaAsset.VersionFont, Size = new(6f, 4f), FontSize = 1.5f, AllowAutoSizing = false }) { PostBuilder = t => text = t }.Generate(holder, Vector3.zero, out _);

            newBubble = new(holder, background, text, new());
        }


        newBubble.reference.Value = bubble;
        bubble.IsDirty = true;
        activeBubbles.Enqueue(newBubble);
    }

    public void LateUpdate()
    {
        float height = 0f;
        int num = 0;

        foreach (var bubble in activeBubbles.Reverse())
        {
            bubble.holder.SetActive(true);

            var consoleBubble = bubble.reference.Value!;
            if (consoleBubble.IsDirty)
            {
                bubble.text.text = "<size=80%>" + consoleBubble.Executed.Color(Color.white.RGBMultiplied(0.78f)) + "</size>" + ("\n" + consoleBubble.Response).Replace("\n", "\n ");
                bubble.text.ForceMeshUpdate();
            }

            float myHeight = bubble.text.preferredHeight + 0.2f;
            float myWidth = bubble.text.preferredWidth + 0.2f;

            bubble.holder.transform.localPosition = new Vector3(0f, height + myHeight * 0.5f);
            bubble.background.transform.localPosition = new Vector3((bubble.text.preferredWidth - bubble.text.rectTransform.sizeDelta.x) * 0.5f, 0f, 0f);
            bubble.background.size = new Vector2(myWidth - 0.06f, myHeight - 0.06f);
            height += myHeight;

            num++;

            if (height > 3f) break;
        }

        while (activeBubbles.Count > num) unusedBubblePool.Push(activeBubbles.Dequeue());

        foreach (var b in unusedBubblePool) b.holder.gameObject.SetActive(false);
    }
}
