using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class FormulaCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    enum FTokenType
    {
        //総合
        Statement,
        Terminal,

        // 数値
        AExpr,
        ATerm,
        AFactor,
        
        // 真偽値
        LXorTerm,
        LOrTerm,
        LTerm,
        LFactor,

        //記号
        SPlus,
        SMinus,
        SCross,
        SDivide,
        SXor,
        SOr,
        SAnd,
        SNot,
        SLessThan,
        SMoreThan,
        SLessThanOrEqual,
        SMoreThanOrEqual,
        SEqual,
        SNotEqual,

        // 数値 or 真偽値
        Number,

        //エラー
        ErrorType
    }

    class FToken
    {
        public FTokenType TokenType { get; set; }
        public Func<CommandEnvironment, CoTask<ICommandToken>> Task { get; set; }

        public FToken(FTokenType type, Func<CommandEnvironment, CoTask<ICommandToken>>? task = null)
        {
            TokenType = type;
            Task = task == null ? env => new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token) : task;
        }
    }

    record FRule(FTokenType[] rule, Func<CommandEnvironment, FToken[], CoTask<ICommandToken>> generator);

    static Dictionary<FTokenType, List<FRule>> AllRule;
    static Dictionary<FTokenType, FTokenType[]> FollowersCache;
    static Dictionary<FTokenType, FTokenType[]> LoopFollowersCache;

    static FormulaCommand()
    {
        void AddRule(FTokenType type, FTokenType[] tokens, Func<CommandEnvironment, FToken[], CoTask<ICommandToken>> generator)
        {
            List<FRule> list = null!;
            if (!AllRule.TryGetValue(type, out list!))
            {
                list = new();
                AllRule[type] = list;
            }

            list.Add(new(tokens, generator));
        }

        Func<CommandEnvironment, FToken[], CoTask<ICommandToken>> MonoFunc<T>(Func<T, ICommandToken> monoFunc) =>
            (env, tokens) =>
            {
                var val1 = tokens[1].Task!.Invoke(env);
                return val1.Chain(c => c.AsValue<T>(env)).ChainFast(v => monoFunc.Invoke(v));
            };

        Func<CommandEnvironment, FToken[], CoTask<ICommandToken>> BinaryFunc<T1, T2>(Func<T1, T2, ICommandToken> binaryFunc) =>
            (env, tokens) =>
            {
                var val1 = tokens[0].Task!.Invoke(env);
                var val2 = tokens[2].Task!.Invoke(env);
                T1 v1 = default(T1)!;
                return val1.Chain(c => c.AsValue<T1>(env)).Chain(t =>
                {
                    v1 = t;
                    return val2.Chain(c => c.AsValue<T2>(env));
                }).ChainFast(v2 => binaryFunc.Invoke(v1, v2));
            };

        AllRule = new();

        AddRule(FTokenType.Statement, [FTokenType.AExpr], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.AExpr, [FTokenType.ATerm], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.ATerm, [FTokenType.AFactor], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.AFactor, [FTokenType.Number], (env, tokens) => tokens[0].Task!.Invoke(env));

        AddRule(FTokenType.Statement, [FTokenType.LXorTerm], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.LXorTerm, [FTokenType.LOrTerm], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.LOrTerm, [FTokenType.LTerm], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.LTerm, [FTokenType.LFactor], (env, tokens) => tokens[0].Task!.Invoke(env));
        AddRule(FTokenType.LFactor, [FTokenType.Number], (env, tokens) => tokens[0].Task!.Invoke(env));

        AddRule(FTokenType.AExpr, [FTokenType.AExpr, FTokenType.SPlus, FTokenType.ATerm], BinaryFunc<float, float>((v1, v2) => new FloatCommandToken(v1 + v2)));
        AddRule(FTokenType.AExpr, [FTokenType.AExpr, FTokenType.SMinus, FTokenType.ATerm], BinaryFunc<float, float>((v1, v2) => new FloatCommandToken(v1 - v2)));
        AddRule(FTokenType.ATerm, [FTokenType.ATerm, FTokenType.SCross, FTokenType.AFactor], BinaryFunc<float, float>((v1, v2) => new FloatCommandToken(v1 * v2)));
        AddRule(FTokenType.ATerm, [FTokenType.ATerm, FTokenType.SDivide, FTokenType.AFactor], BinaryFunc<float, float>((v1, v2) => new FloatCommandToken(Mathf.Abs(v2) > 0 ? v1 / v2 : 0f)));

        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SLessThan, FTokenType.AExpr], BinaryFunc<float, float>((v1, v2) => new BooleanCommandToken(v1 < v2)));
        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SMoreThan, FTokenType.AExpr], BinaryFunc<float, float>((v1, v2) => new BooleanCommandToken(v1 > v2)));
        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SMoreThanOrEqual, FTokenType.AExpr], BinaryFunc<int, int>((v1, v2) => new BooleanCommandToken(v1 <= v2)));
        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SMoreThanOrEqual, FTokenType.AExpr], BinaryFunc<int, int>((v1, v2) => new BooleanCommandToken(v1 <= v2)));
        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SEqual, FTokenType.AExpr], BinaryFunc<int, int>((v1, v2) => new BooleanCommandToken(v1 == v2)));
        AddRule(FTokenType.LFactor, [FTokenType.AExpr, FTokenType.SNotEqual, FTokenType.AExpr], BinaryFunc<int, int>((v1, v2) => new BooleanCommandToken(v1 != v2)));

        AddRule(FTokenType.LFactor, [FTokenType.SNot, FTokenType.LFactor], MonoFunc<bool>((v) => new BooleanCommandToken(!v)));
        AddRule(FTokenType.LXorTerm, [FTokenType.LXorTerm, FTokenType.SXor, FTokenType.LOrTerm], BinaryFunc<bool, bool>((v1, v2) => new BooleanCommandToken(v1 ^ v2)));
        AddRule(FTokenType.LOrTerm, [FTokenType.LOrTerm, FTokenType.SOr, FTokenType.LTerm], BinaryFunc<bool, bool>((v1, v2) => new BooleanCommandToken(v1 || v2)));
        AddRule(FTokenType.LTerm, [FTokenType.LTerm, FTokenType.SAnd, FTokenType.LFactor], BinaryFunc<bool, bool>((v1, v2) => new BooleanCommandToken(v1 && v2)));

        FollowersCache = new();
        LoopFollowersCache = new();

        foreach (FTokenType type in Enum.GetValues(typeof(FTokenType))) {
            HashSet<FTokenType> types = [type];
            FTokenType[] newAdded = [type];
            HashSet<FTokenType> temp = new();

            //新たに追加された記号に対して繰り返す
            while (newAdded.Length > 0)
            {
                foreach (var t in newAdded)
                {
                    //計算済みのフォロワー集合があればそれを利用する
                    if (FollowersCache.TryGetValue(t, out var f))
                    {
                        f.Where(types.Add).Do(f => temp.Add(f));
                    }
                    else if (AllRule.TryGetValue(type, out var rules))
                    {
                        //全生成規則に対して、未登録な先頭の記号をフォロワー集合に追加する
                        foreach (var r in rules) if (r.rule.Length > 0 && types.Add(r.rule[0])) temp.Add(r.rule[0]);
                    }
                }

                newAdded = temp.ToArray();
                temp.Clear();
            }
            FollowersCache[type] = types.ToArray();
        }

        foreach (FTokenType type in Enum.GetValues(typeof(FTokenType)))
        {
            HashSet<FTokenType> types = [type];

            //自身を生成するルールを取得する
            if (AllRule.TryGetValue(type, out var rules))
            {
                //先頭と導出される記号が同じものを抽出
                foreach(var rule in rules.Where(r => r.rule[0] == type && r.rule.Length > 1))
                {
                    foreach(var t in FollowersCache[rule.rule[1]]) types.Add(t);
                }

                LoopFollowersCache[type] = types.ToArray();
            }
        }
    }

    FToken[] LexicalAnalyze(IEnumerable<ICommandToken> tokens, CommandEnvironment env)
    {
        return tokens.Select(a =>
        {
            //数式としての括弧
            if (a is StatementCommandToken sct)
            {
                var inner = Analyze(sct.RawTokens, env);
                return new FToken(FTokenType.Number, inner.Task);
            }

            if (a is StringCommandToken str)
            {
                FToken? result = str.Token switch
                {
                    "+" => new(FTokenType.SPlus),
                    "-" => new(FTokenType.SMinus),
                    "*" => new(FTokenType.SCross),
                    "/" => new(FTokenType.SDivide),
                    "or" => new(FTokenType.SOr),
                    "and" => new(FTokenType.SAnd),
                    "xor" => new(FTokenType.SXor),
                    "~" => new(FTokenType.SNot),
                    "<" => new(FTokenType.SLessThan),
                    ">" => new(FTokenType.SMoreThan),
                    "<=" => new(FTokenType.SLessThanOrEqual),
                    ">=" => new(FTokenType.SMoreThanOrEqual),
                    "==" => new(FTokenType.SEqual),
                    "!=" => new(FTokenType.SNotEqual),
                    _ => null
                };
                if (result != null) return result;
            }

            return new(FTokenType.Number, env => new CoImmediateTask<ICommandToken>(a));
        }).Append(new(FTokenType.Terminal)).ToArray();
    }

    FToken? TestRule(FTokenType required, FRule rule, FTokenType[] followerTypes, IReadOnlyArray<FToken> tokens, FToken? firstToken, CommandEnvironment env, out IReadOnlyArray<FToken> follower, out FToken[] tokensOnRule)
    {
        //Debug.Log("TryRule: [" + rule.rule.Join(r => r.ToString(), ", ") + "]");

        //各ルールごとに最後まで到達できるかどうか試す
        follower = tokens;
        tokensOnRule = new FToken[rule.rule.Length];
        bool withoutFirst = firstToken != null;
        bool generated = true;

        if (withoutFirst) tokensOnRule[0] = firstToken!;

        for (int i = withoutFirst ? 1 : 0; i < rule.rule.Length; i++)
        {
            //フォロワー集合は次の記号のフォロワー集合か、呼び出し元が指定しているフォロワー集合と同じ記号を繰り返す場合のフォロワー集合の和集合
            var result = SyntaxAnalyze(rule.rule[i],
                i + 1 < rule.rule.Length ? FollowersCache[rule.rule[i + 1]] : followerTypes.Concat(LoopFollowersCache[required]).ToArray(),
                follower, env, out var f);

            if (result == null)
            {
                generated = false;
                break;
            }

            tokensOnRule[i] = result;
            follower = f;
        }

        if(generated && follower.Count > 0)
        {
            var tempAry = tokensOnRule.ToArray();
            var myToken = new FToken(required, env => rule.generator.Invoke(env, tempAry!));

            //導出する記号と同じ記号から始まる規則に適用させてみる (T := T + E のようなケース)
            if (LoopFollowersCache[required].Contains(follower[0].TokenType))
            {
                var loop = LoopSyntaxAnalyze(myToken, required, followerTypes, follower, env, out var f, out tokensOnRule);
                //適用できたとき
                if (loop != null)
                {
                    follower = f;
                    return loop;
                }
            }

            //自身をそのまま還元してよいとき
            if (followerTypes.Contains(follower[0].TokenType))
            {
                //Debug.Log("Reduce: " + required.ToString() + ", Left: [" + follower.Join(l => l.TokenType.ToString()) + "]");
                return myToken;
            }
        }

        return null;
    }

    FToken? LoopSyntaxAnalyze(FToken firstToken, FTokenType required, FTokenType[] followerTypes, IReadOnlyArray<FToken> tokensWithoutFirst, CommandEnvironment env, out IReadOnlyArray<FToken> follower, out FToken[] tokensOnRule)
    {
        if (AllRule.TryGetValue(required, out var rules))
        {
            //繰り返し規則の方で調べる
            foreach (var rule in rules.Where(r => r.rule[0] == required))
            {
                var result = TestRule(required, rule, followerTypes, tokensWithoutFirst, firstToken, env, out follower, out tokensOnRule);
                if (result != null) return result;
            }
        }

        follower = tokensWithoutFirst;
        tokensOnRule = [];
        return null;
    }

    FToken? SyntaxAnalyze(FTokenType required, FTokenType[] followerTypes, IReadOnlyArray<FToken> tokens, CommandEnvironment env, out IReadOnlyArray<FToken> follower)
    {
        //Debug.Log("Required: " + required.ToString() +", [" + tokens.Join(t => t.TokenType.ToString(), ", ")+ "]");

        if (AllRule.TryGetValue(required, out var rules))
        {
            //要求と同じ記号から始まらない規則より、導出できるものを発見する
            foreach(var rule in rules.Where(r => r.rule[0] != required))
            {
                var result = TestRule(required, rule, followerTypes, tokens, null, env, out follower, out var tokensOnRule);
                if (result != null) return result;
            }
        }

        //シフト
        if (tokens.Count >= 1 && tokens[0].TokenType == required)
        {
            //Debug.Log("Shift: " + required.ToString());
            follower = tokens.Skip(1);
            return tokens[0];
        }

        follower = tokens;
        return null;
    }

    FToken Analyze(IEnumerable<ICommandToken> tokens, CommandEnvironment env)
    {
        var result = SyntaxAnalyze(FTokenType.Statement, [FTokenType.Terminal], new ReadOnlyArray<FToken>(LexicalAnalyze(tokens, env)), env, out var follower);

        if(result == null) return new FToken(FTokenType.ErrorType);

        if (follower.Count != 1)
        {
            env.Logger.Push("The end of the expression was not reached.");
            return new FToken(FTokenType.ErrorType);
        }

        return result;
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        return Analyze(arguments, env).Task.Invoke(env);
    }
}
