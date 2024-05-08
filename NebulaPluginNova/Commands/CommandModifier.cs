using Nebula.Commands.Tokens;
using Virial.Command;

namespace Nebula.Commands;

/// <summary>
/// 何もしないモディファイアです。
/// </summary>
public class ThroughCommandModifier : ICommandModifier
{
    static public ICommandModifier Modifier = new ThroughCommandModifier();
    ICommandToken ICommandModifier.ApplyTo(ICommandToken argument) => argument;
}

/// <summary>
/// 一つの変数を束縛するモディファイアです。
/// </summary>
public class LetCommandModifier : ICommandModifier
{
    public string Argument { get; private init; }
    public ICommandToken Value { get; private init; }
    ICommandModifier baseModifier { get; init; }
    public LetCommandModifier(string argument, ICommandToken value, ICommandModifier baseModifier)
    {
        Argument = argument;
        Value = value;
        this.baseModifier = baseModifier;
    }

    ICommandToken ICommandModifier.ApplyTo(ICommandToken argument)
    {
        if (argument is StringCommandToken sct && sct.CanSubstitute && sct.Token == Argument)
            return Value;
        return baseModifier.ApplyTo(argument);
    }
}

/// <summary>
/// 複数の変数を束縛するモディファイアです。
/// </summary>
public class LetsCommandModifier : ICommandModifier
{
    private Dictionary<string, ICommandToken> arguments = new();
    ICommandModifier baseModifier { get; init; }
    public LetsCommandModifier(IEnumerable<(string argument, ICommandToken value)> values, ICommandModifier baseModifier)
    {
        foreach (var tuple in values) arguments.Add(tuple.argument, tuple.value);
        this.baseModifier = baseModifier;
    }

    ICommandToken ICommandModifier.ApplyTo(ICommandToken argument)
    {
        if (argument is StringCommandToken sct && sct.CanSubstitute && arguments.TryGetValue(sct.Token, out var val))
            return val;
        return baseModifier.ApplyTo(argument);
    }
}