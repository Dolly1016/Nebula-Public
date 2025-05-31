using Nebula.Behavior;
using Nebula.Roles;
using System.Text;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Runtime;
using static Nebula.Modules.IFunctionalVariable;

namespace Nebula.Modules;

public interface IFunctionalVariable
{
    public class EnumeratorView<T> where T : class
    {
        IEnumerator<T>? myEnumerator;
        T? storedVal;
        EnumeratorView<T>? prev, next;
        bool isValid;

        public EnumeratorView(IEnumerator<T>? myEnumerator) : this(myEnumerator, null) { }

        private EnumeratorView(IEnumerator<T>? myEnumerator, EnumeratorView<T>? prev)
        {
            this.myEnumerator = myEnumerator;
            this.prev = prev;
            isValid = myEnumerator?.MoveNext() ?? false;
            storedVal = isValid ? myEnumerator!.Current : null;
        }

        public EnumeratorView<T>? GetPrev() => prev;
        public EnumeratorView<T>? GetNext()
        {
            next ??= new(myEnumerator, this);
            return next;
        }

        public T? Get() => storedVal;
        public bool IsValid => isValid;
    }

    public string? ToString() => AsString();
    public string AsString();
    public bool AsBool() => false;
    public float AsNumber() => 0;
    public EnumeratorView<string>? AsStringEnumerator() => null;
    public IEnumerable<string>? AsStringEnumerable() => null;
    public bool Equals(IFunctionalVariable variable) => AsString().Equals(variable.AsString());
    public T? AsObject<T>() where T : class => null;

    public class FunctionalVariableString : IFunctionalVariable
    {
        private string val;
        public FunctionalVariableString(string val) { this.val = val; }
        public string AsString() => val;
        public float AsNumber() => float.TryParse(val, out float num) ? num : 0;
    }

    public class FunctionalVariableFloat : IFunctionalVariable
    {
        private float val;
        public FunctionalVariableFloat(float val) { this.val = val; }
        public string AsString() => val.ToString();
        public float AsNumber() => val;

        public bool Equals(IFunctionalVariable variable) => val == variable.AsNumber();
    }

    public class FunctionalVariableBool : IFunctionalVariable
    {
        private bool val;
        public FunctionalVariableBool(bool val) { this.val = val; }
        public string AsString() => val.ToString();
        public bool AsBool() => val;
        public bool Equals(IFunctionalVariable variable) => val == variable.AsBool();
    }

    public class FunctionalVariableStringEnumerator : IFunctionalVariable
    {
        private EnumeratorView<string> val;
        public FunctionalVariableStringEnumerator(EnumeratorView<string> val) { this.val = val; }
        public string AsString() => val.Get() ?? "INVALID";
        public bool AsBool() => val.IsValid;
        public EnumeratorView<string>? AsStringEnumerator() => val;
    }

    public class FunctionalVariableStringEnumerable : IFunctionalVariable
    {
        private IEnumerable<string> val;
        public FunctionalVariableStringEnumerable(IEnumerable<string> val) { this.val = val; }
        public string AsString() => "Enumerable";
        public IEnumerable<string>? AsStringEnumerable() => val;
    }

    public class FunctionalObjectWrapping<Obj> : IFunctionalVariable where Obj : class
    {
        private Obj? val;
        public string AsString() => "WrappedObj";
        public bool AsBool() => val != null;
        public T? AsObject<T>() where T : class
        {
            if (typeof(T) == typeof(Obj)) return val as T;
            return null;
        }

        public FunctionalObjectWrapping(Obj? val)
        {
            this.val = val;
        }
    }

    public static IFunctionalVariable Generate(string val) => new FunctionalVariableString(val);
    public static IFunctionalVariable Generate(int val) => new FunctionalVariableFloat(val);
    public static IFunctionalVariable Generate(float val) => new FunctionalVariableFloat(val);
    public static IFunctionalVariable Generate(bool val) => new FunctionalVariableBool(val);
    public static IFunctionalVariable Generate(EnumeratorView<string> val) => new FunctionalVariableStringEnumerator(val);
    public static IFunctionalVariable Generate(IEnumerable<string> val) => new FunctionalVariableStringEnumerable(val);
    public static IFunctionalVariable GenerateWrapped<T>(T? val) where T : class => new FunctionalObjectWrapping<T>(val);
}
public class TextFunction
{
    public Predicate<int> RequiredArguments { get; private set; }
    public Predicate<FunctionalEnvironment?>? RequiredEnvironments { get; private set; }
    private Func<IFunctionalVariable[], IFunctionalVariable> myfunc;
    private TextFunction? overloadedLink = null;

    public TextFunction(int requiredArguments, Func<IFunctionalVariable[], IFunctionalVariable> function) : this(null, requiredArguments, function) { }

    public TextFunction(Predicate<int> requiredArguments, Func<IFunctionalVariable[], IFunctionalVariable> function) : this(null,requiredArguments, function) { }

    public TextFunction(Predicate<FunctionalEnvironment?>? requiredEnv, int requiredArguments, Func<IFunctionalVariable[], IFunctionalVariable> function)
    {
        myfunc = function;
        RequiredArguments = (num) => num == requiredArguments;
        RequiredEnvironments = requiredEnv;
    }

    public TextFunction(Predicate<FunctionalEnvironment?>? requiredEnv, Predicate<int> requiredArguments, Func<IFunctionalVariable[], IFunctionalVariable> function)
    {
        myfunc = function;
        RequiredArguments = requiredArguments;
        RequiredEnvironments = requiredEnv;
    }

    public bool Execute(FunctionalEnvironment? env, IFunctionalVariable[] arguments, out IFunctionalVariable? result)
    {
        result = null;

        if (RequiredArguments.Invoke(arguments.Length) && (RequiredEnvironments?.Invoke(env) ?? true))
        {
            result = myfunc.Invoke(arguments);
            return true;
        }

        return overloadedLink?.Execute(env, arguments, out result) ?? false;
    }

    public void Overload(TextFunction function)
    {
        if (overloadedLink != null)
            overloadedLink.Overload(function);
        else
            overloadedLink = function;
    }
}
public class FunctionalEnvironment
{
    public Dictionary<string, IFunctionalVariable> Arguments;
    public FunctionalSpace? MySpace = null;

    public FunctionalEnvironment(Dictionary<string, string>? arguments = null, FunctionalEnvironment? baseTable = null)
    {
        Arguments = [];
        if (arguments == null) return;
        foreach (var entry in arguments)
        {
            Arguments[baseTable.GetValueOrRaw(entry.Key).AsString()] = baseTable.GetValueOrRaw(entry.Value);
        }
    }

    public void TryRegister(string name, Func<IFunctionalVariable> variable)
    {
        if (!Arguments.ContainsKey(name)) Arguments[name] = variable.Invoke();
    }
}

static public class ArgumentTableHelper
{
    public static string GetString(this FunctionalEnvironment? table, string rawString) => GetValueOrRaw(table, rawString).AsString();

    public static IFunctionalVariable GetValueOrRaw(this FunctionalEnvironment? table, string rawString)
    {
        if (!rawString.StartsWith("#")) return IFunctionalVariable.Generate(rawString);

        string inStr = rawString.Substring(1);
        return GetValue(table, inStr);
    }

    public static IFunctionalVariable GetValue(this FunctionalEnvironment? table, string programStr)
    {
        try
        {
            return GetValueInternal(table, programStr, out _);
        }
        catch
        {
            return IFunctionalVariable.Generate($"BADTEXTCODE({programStr})");
        }
    }

    private static IFunctionalVariable GetValueInternal(FunctionalEnvironment? table, string innerString, out int progress)
    {
        //生文字列
        string tokenString = innerString.TrimStart();
        if (tokenString.StartsWith('\''))
        {
            string[] splitted = tokenString.Split('\'', 3);
            if (splitted.Length == 3)
            {
                progress = (innerString.Length - tokenString.Length) + 2 + splitted[1].Length;
                return IFunctionalVariable.Generate(splitted[1]);
            }
        }

        int diff = innerString.Length - tokenString.Length;

        //引数・関数と一致する場合
        int lastIndex = 0;
        while (tokenString.Length > lastIndex && TextField.IdPredicate.Invoke(tokenString[lastIndex])) lastIndex++;

        //関数
        if (lastIndex < tokenString.Length && tokenString[lastIndex] is '(')
        {
            string funcName = tokenString.Substring(0, lastIndex);

            string argStr = tokenString.Substring(lastIndex + 1);
            progress = lastIndex + 1 + diff;
            List<IFunctionalVariable> args = [];
            while (true)
            {

                args.Add(GetValueInternal(table, argStr, out int p));
                if (p == -1)
                {
                    progress = -1;
                    return IFunctionalVariable.Generate($"BADFORMAT({tokenString}) at ({argStr})");
                }

                argStr = argStr.Substring(p);
                progress += p;

                var temp = argStr.TrimStart();
                progress += argStr.Length - temp.Length;
                argStr = temp;

                if (argStr.Length == 0) return IFunctionalVariable.Generate($"BADFORMAT({tokenString})");


                if (argStr[0] is ',')
                {
                    progress++;
                    argStr = argStr.Substring(1);
                }
                else if (argStr[0] is ')')
                {
                    progress++;
                    return (table?.MySpace ?? FunctionalSpace.DefaultSpace).CallFunction(funcName, table, args.ToArray());
                }

            }
        }

        //引数
        string valString = lastIndex == 0 ? "" : tokenString.Substring(0, lastIndex);
        progress = lastIndex + diff;
        string trimmed = valString.Trim();
        if (float.TryParse(trimmed, out var num))
            return IFunctionalVariable.Generate(num);
        else if (table != null && table!.Arguments.TryGetValue(trimmed, out var val))
            return val;
        else if (bool.TryParse(trimmed, out var flag))
            return IFunctionalVariable.Generate(flag);
        else
            return IFunctionalVariable.Generate($"UNKNOWN({trimmed})");
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class FunctionalSpace
{
    private Dictionary<string, TextFunction> allFunctions = [];
    private FunctionalSpace? parentSpace = null;

    public static FunctionalSpace DefaultSpace = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        DefaultSpace = new();

        DefaultSpace.LoadFunction("Concat", new(n => n >= 2, (args) => {
            StringBuilder builder = new();
            foreach (var arg in args) builder.Append(arg.AsString());
            return IFunctionalVariable.Generate(builder.ToString());
        }));
        DefaultSpace.LoadFunction("ToRoleName", new(1, (args) => IFunctionalVariable.Generate("role." + args[0].AsString())));
        DefaultSpace.LoadFunction("IfConfig", new(3, (args) =>
        {

            var option = ConfigurationValues.AllEntries.FirstOrDefault(option => option.Name == args[0].AsString()) as ValueConfiguration<bool>;
            if (option == null)
                return IFunctionalVariable.Generate($"BADCONF({args[0]})");
            if (option.GetValue()) return args[1]; else return args[2];
        }));
        DefaultSpace.LoadFunction("Translate", new(1, (args) => IFunctionalVariable.Generate(Language.Translate(args[0].AsString()))));
        //DefaultSpace.LoadFunction("ConfigVal", new(1, (args) => IFunctionalVariable.Generate(ConfigurationValues.AllEntries.FirstOrDefault(option => option.Name == args[0].AsString())?.va ?.ToDisplayString() ?? $"BADCONF({args[0]})")));
        //DefaultSpace.LoadFunction("ConfigBool", new(1, (args) => IFunctionalVariable.Generate(NebulaConfiguration.AllConfigurations.FirstOrDefault(option => option.Id == args[0].AsString())?.GetBool() ?? false)));
        //DefaultSpace.LoadFunction("ConfigRaw", new(1, (args) => IFunctionalVariable.Generate(NebulaConfiguration.AllConfigurations.FirstOrDefault(option => option.Id == args[0].AsString())?.CurrentValue ?? 0)));
        DefaultSpace.LoadFunction("Replace", new(3, (args) => IFunctionalVariable.Generate(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()))));
        DefaultSpace.LoadFunction("Property", new(1, (args) => IFunctionalVariable.Generate(PropertyManager.GetProperty(args[0].AsString())?.GetString() ?? $"BADPROP({args[0]})")));
        DefaultSpace.LoadFunction("PropertyVal", new(1, (args) => IFunctionalVariable.Generate(PropertyManager.GetProperty(args[0].AsString())?.GetFloat() ?? 0f)));

        //整数演算
        DefaultSpace.LoadFunction("Add", new(2, (args) => IFunctionalVariable.Generate(args[0].AsNumber() + args[1].AsNumber())));
        DefaultSpace.LoadFunction("Sub", new(2, (args) => IFunctionalVariable.Generate(args[0].AsNumber() - args[1].AsNumber())));
        DefaultSpace.LoadFunction("Mul", new(2, (args) => IFunctionalVariable.Generate(args[0].AsNumber() * args[1].AsNumber())));
        DefaultSpace.LoadFunction("Lessthan", new(2, (args) => IFunctionalVariable.Generate(args[0].AsNumber() < args[1].AsNumber())));
        DefaultSpace.LoadFunction("Morethan", new(2, (args) => IFunctionalVariable.Generate(args[0].AsNumber() > args[1].AsNumber())));

        //論理演算
        DefaultSpace.LoadFunction("Not", new(1, (args) => IFunctionalVariable.Generate(!args[0].AsBool())));
        DefaultSpace.LoadFunction("And", new(2, (args) => IFunctionalVariable.Generate(args[0].AsBool() && args[1].AsBool())));
        DefaultSpace.LoadFunction("Or", new(2, (args) => IFunctionalVariable.Generate(args[0].AsBool() || args[1].AsBool())));
        DefaultSpace.LoadFunction("Xor", new(2, (args) => IFunctionalVariable.Generate(args[0].AsBool() != args[1].AsBool())));

        //制御
        DefaultSpace.LoadFunction("If", new(3, (args) => args[0].AsBool() ? args[1] : args[2]));

        //比較
        DefaultSpace.LoadFunction("Equal", new(2, (args) => IFunctionalVariable.Generate(args[0].Equals(args[1]))));

        //列挙型
        DefaultSpace.LoadFunction("GetIterator", new(1, (args) => IFunctionalVariable.Generate(new EnumeratorView<string>(args[1].AsStringEnumerable()!.GetEnumerator()))));
        DefaultSpace.LoadFunction("GetNext", new(1, (args) => IFunctionalVariable.Generate(args[1].AsStringEnumerator()!.GetNext()!)));
        DefaultSpace.LoadFunction("GetPrev", new(1, (args) => IFunctionalVariable.Generate(args[1].AsStringEnumerator()!.GetPrev()!)));

        //オブジェクト
        DefaultSpace.LoadFunction("GetRole", new(1, args => GenerateWrapped(Roles.Roles.AllAssignables().FirstOrDefault(r => r.LocalizedName == args[0].AsString()))));
        DefaultSpace.LoadFunction("GetCitation", new(1, (args) =>
        {
            Citation? citation = null;
            if (args[0] is FunctionalObjectWrapping<DefinedAssignable> w)
                citation = (w.AsObject<DefinedAssignable>() as HasCitation)?.Citation;
            if (Citation.TryGetCitation(args[0].AsString(), out var temp))
                citation = temp;
            
            return GenerateWrapped(citation);
        }));

    }


    public void LoadFunction(string name, TextFunction function)
    {
        if (allFunctions.TryGetValue(name, out var val))
            val.Overload(function);
        else
            allFunctions[name] = function;
    }

    public IFunctionalVariable CallFunction(string name, FunctionalEnvironment? env, IFunctionalVariable[] argument)
    {
        if (allFunctions.TryGetValue(name, out var func))
            return (func.Execute(env, argument, out var result) ? result : IFunctionalVariable.Generate($"BADARGS({name})")) ?? IFunctionalVariable.Generate($"BADFUNC({name})");

        if (parentSpace != null) return parentSpace.CallFunction(name, env, argument);
        else return IFunctionalVariable.Generate($"BADCALL({name})");
    }

    public void SetParent(FunctionalSpace? parent)
    {
        parentSpace = parent;
    }

}