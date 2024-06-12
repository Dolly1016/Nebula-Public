using System.Collections;
using Virial.Helpers;

namespace Virial.Command;

public interface CoTask<T>
{
    /// <summary>
    /// 実行は一度きりで、2回目以降のCoWaitの呼び出しはすぐに終了することが求められます。
    /// </summary>
    /// <returns></returns>
    IEnumerator CoWait();
    bool IsCompleted { get; }
    bool IsFailed { get; }
    bool CanAccessResult => IsCompleted && IsFailed;

    T Result { get; }
}

/// <summary>
/// 引数を率いて、タスクを提供する関数を表します。
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="V"></typeparam>
/// <param name="input"></param>
/// <returns></returns>
public delegate CoTask<T> TaskSupplier<T, V>(V input);

public delegate CoTask<T> CoTaskChainer<T, V>(CoTask<V> input, CommandEnvironment env);

public class CoImmediateTask<T> : CoTask<T>
{
    public IEnumerator CoWait() { yield break; }
    public bool IsCompleted => true;
    public bool IsFailed => false;
    public T Result { get; private init; }

    public CoImmediateTask(T result)
    {
        Result = result;
    }
}

public class CoActionTask<T> : CoTask<T>
{
    public IEnumerator CoWait() { Result = myAction.Invoke(); yield break; }
    public bool IsCompleted => true;
    public bool IsFailed => false;
    private Func<T> myAction;
    public T Result { get; private set; }

    public CoActionTask(Func<T> action)
    {
        myAction = action;
    }
}

public class CoImmediateErrorTask<T> : CoTask<T>
{
    ICommandLogger? logger;
    string? errorMessage;
    public IEnumerator CoWait()
    {
        if (errorMessage != null) logger?.PushError(errorMessage);
        yield break;
    }
    public bool IsCompleted => true;
    public bool IsFailed => true;
    public T Result { get => throw new Exception("It is failed task."); }

    public CoImmediateErrorTask(ICommandLogger? logger = null, string? errorMessage = null)
    {
        this.logger = logger;
        this.errorMessage = errorMessage;
    }
}

public class CoBuiltInTask<T> : CoTask<T>
{
    public Func<CoBuiltInTask<T>, IEnumerator> ProcedureFunc { get; private init; }
    public bool IsCompleted { get; set; } = false;

    public bool IsFailed { get; set; } = false;
    public T Result { get; set; } = default(T)!;

    public CoBuiltInTask(Func<CoBuiltInTask<T>, IEnumerator> supplier)
    {
        ProcedureFunc = supplier;
    }

    IEnumerator CoTask<T>.CoWait()
    {
        if (IsCompleted && !IsFailed) yield break;

        yield return ProcedureFunc.Invoke(this);
        IsCompleted = true;
        yield break;
    }
}

public class CoChainedTask<T, S> : CoTask<T>
{
    CoTask<S> precedeTask;
    TaskSupplier<T, S> followerSupplier;
    Func<CoTask<T>?>? onFailed;

    public IEnumerator CoWait()
    {
        yield return precedeTask.CoWait();

        CoTask<T>? follower = null;
        if (precedeTask.IsFailed)
        {
            IsCompleted = true;
            IsFailed = true;
            follower = onFailed?.Invoke();
        }
        else
        {
            follower = followerSupplier.Invoke(precedeTask.Result);
        }

        if (follower == null) yield break;

        yield return follower.CoWait();

        if (follower.IsFailed)
        {
            IsCompleted = true;
            IsFailed = true;
            yield break;
        }

        Result = follower.Result;
        IsCompleted = true;
    }

    public bool IsCompleted { get; private set; } = false;
    public bool IsFailed { get; private set; } = false;
    public T Result { get; private set; }

    public CoChainedTask(CoTask<S> precedeTask, TaskSupplier<T, S> followerSupplier, Func<CoTask<T>?>? onFailed = null)
    {
        this.precedeTask = precedeTask;
        this.followerSupplier = followerSupplier;
        this.onFailed = onFailed;
    }
}

public static class CoChainedTasksHelper
{
    /// <summary>
    /// 連続するタスクを定義します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="S"></typeparam>
    /// <param name="precedeTask"></param>
    /// <param name="onFailed"></param>
    /// <param name="followerSupplier"></param>
    /// <returns></returns>
    public static CoTask<T> Chain<T, S>(this CoTask<S> precedeTask, TaskSupplier<T, S> followerSupplier, Action onFailed)
    {
        return new CoChainedTask<T, S>(precedeTask, result => followerSupplier(result), () => { onFailed.Invoke(); return null; });
    }

    public static CoTask<T> Chain<T, S>(this CoTask<S> precedeTask, TaskSupplier<T, S> followerSupplier, Func<CoTask<T>?>? onFailed = null)
    {
        return new CoChainedTask<T, S>(precedeTask, result => followerSupplier(result), onFailed);
    }

    public static CoTask<T> ChainFast<T, S>(this CoTask<S> precedeTask, Func<S, T> followerSupplier, Action? onFailed = null)
    {
        return new CoChainedTask<T, S>(precedeTask, result => new CoImmediateTask<T>(followerSupplier.Invoke(result)), onFailed == null ? null : () => { onFailed.Invoke(); return null; });
    }

    public static CoTask<T> ChainIf<T, S>(this CoTask<S> precedeTask, Dictionary<S,Func<CoTask<T>>> followers, Func<CoTask<T>>? defaultFollower = null, Action? onFailed = null)
    {
        return new CoChainedTask<T, S>(precedeTask, result =>
        {
            if (followers.TryGetValue(result, out var supplier))
                return supplier.Invoke();
            else
                return defaultFollower?.Invoke() ?? new CoImmediateErrorTask<T>();
            
        }, onFailed == null ? null : () => { onFailed.Invoke(); return null; });
    }

    /// <summary>
    /// 直前のタスクの実行結果をもとに変換をするタスクです。
    /// 変数の適用を自前でしなければならない点に注意してください。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="V"></typeparam>
    /// <param name="precedeTask"></param>
    /// <param name="supplier"></param>
    /// <param name="onFailed"></param>
    /// <returns></returns>
    public static CoTask<IEnumerable<T>> Select<T, V>(this CoTask<IEnumerable<V>> precedeTask, TaskSupplier<T, V> supplier, Func<CoTask<IEnumerable<T>>?>? onFailed = null)
    {
        return new CoChainedTask<IEnumerable<T>, IEnumerable<V>>(precedeTask, result =>
        {
            IEnumerator CoExecute(CoBuiltInTask<IEnumerable<T>> myResult) {
                List<T> list = new();

                foreach (var v in result ?? [])
                {
                    var task = supplier.Invoke(v);
                    yield return task.CoWait();
                    if (!task.IsFailed) list.Add(task.Result);
                }
                myResult.Result = list;
                yield break;
            }
            return new CoBuiltInTask<IEnumerable<T>>(CoExecute);
        }, onFailed);
    }

    public static CoTask<IEnumerable<T>> SelectParallel<T, V>(this CoTask<IEnumerable<V>> precedeTask, TaskSupplier<T, V> supplier, Func<CoTask<IEnumerable<T>>?>? onFailed = null)
    {
        return new CoChainedTask<IEnumerable<T>, IEnumerable<V>>(precedeTask, result =>
        {
            IEnumerator CoExecute(CoBuiltInTask<IEnumerable<T>> myResult)
            {
                var tasks = result.Select(r => supplier.Invoke(r)).ToArray();
                if (tasks.Length > 0)
                {
                    yield return tasks.Select(t => t.CoWait().HighSpeedEnumerator()).WaitAll();
                    myResult.Result = tasks.Where(t => !t.IsFailed).Select(t => t.Result);
                }
                else
                {
                    myResult.Result = [];
                }
            }
            return new CoBuiltInTask<IEnumerable<T>>(CoExecute);
        }, onFailed);
    }

    public static CoTask<IEnumerable<T>> As<T>(this CoTask<IEnumerable<ICommandToken>> precedeTask, CommandEnvironment env, Func<CoTask<IEnumerable<T>>?>? onFailed = null) => precedeTask.Select(token => token.AsValue<T>(env), onFailed);
    public static CoTask<IEnumerable<T>> AsValues<T>(this ICommandToken token, CommandEnvironment env, Func<CoTask<IEnumerable<T>>?>? onFailed = null) => token.AsEnumerable(env).As<T>(env, onFailed);
    public static CoTask<IEnumerable<T>> AsValues<T>(this CoTask<ICommandToken> precedeTask, CommandEnvironment env, Func<CoTask<IEnumerable<T>>?>? onFailed = null) => precedeTask.Chain(token => token.AsValues<T>(env));
    public static CoTask<IEnumerable<T>> AsParallel<T>(this CoTask<IEnumerable<ICommandToken>> precedeTask, CommandEnvironment env, Func<CoTask<IEnumerable<T>>?>? onFailed = null) => precedeTask.SelectParallel(token => token.AsValue<T>(env), onFailed);

    /// <summary>
    /// 条件に沿った値のみを抽出します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="precedeTask"></param>
    /// <param name="predicate"></param>
    /// <param name="onFailed"></param>
    /// <returns></returns>
    public static CoTask<IEnumerable<T>> Where<T>(this CoTask<IEnumerable<T>> precedeTask, Predicate<T> predicate, Func<CoTask<IEnumerable<T>>?>? onFailed = null)
    {
        return new CoChainedTask<IEnumerable<T>, IEnumerable<T>>(precedeTask,
            result => new CoImmediateTask<IEnumerable<T>>(result.Where(v => predicate.Invoke(v)))
        , onFailed);
    }

    public static CoTask<ICommandToken> Do<T>(this CoTask<IEnumerable<T>> precedeTask, Action<T> consumer, Func<CoTask<ICommandToken>?>? onFailed = null)
    {
        return new CoChainedTask<ICommandToken, IEnumerable<T>>(precedeTask, result =>
        {
            foreach (var v in result) consumer.Invoke(v);
            return new CoImmediateTask<ICommandToken>(new EmptyCommandToken());
        }, onFailed);
    }

    public static CoTask<ICommandToken> Do<T>(this CoTask<IEnumerable<T>> task, Func<T, CoTask<ICommandToken>> consumer, Action? onFailed = null)
    {
        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myTask)
        {
            foreach(var v in task.Result)
            {
                var elementTask = consumer.Invoke(v);

                yield return elementTask.CoWait();
            }
            myTask.Result = new EmptyCommandToken();
        }
        return new CoChainedTask<ICommandToken, IEnumerable<T>>(task, val => new CoBuiltInTask<ICommandToken>(myTask => CoExecute(myTask)));
    }

    public static CoTask<ICommandToken> DoParallel<T>(this CoTask<IEnumerable<T>> task, Func<T, CoTask<ICommandToken>> consumer, Action? onFailed = null)
    {
        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myTask)
        {
            yield return task.Result.Select(v => consumer(v).CoWait().HighSpeedEnumerator()).WaitAll();
        }
        return new CoChainedTask<ICommandToken, IEnumerable<T>>(task, val => new CoBuiltInTask<ICommandToken>(myTask => CoExecute(myTask)));
    }

    public static CoTask<ICommandToken> Do<T>(this CoTask<T> task, Func<T, CoTask<ICommandToken>>[] consumers, Action? onFailed = null)
    {
        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myTask)
        {
            foreach (var c in consumers)
            {
                var elementTask = c.Invoke(task.Result);
                yield return elementTask.CoWait();
            }
            myTask.Result = new EmptyCommandToken();
        }
        return new CoChainedTask<ICommandToken, T>(task, val => new CoBuiltInTask<ICommandToken>(myTask => CoExecute(myTask)));
    }

    public static CoTask<ICommandToken> DoParallel<T>(this CoTask<T> task, Func<T, CoTask<ICommandToken>>[] consumers, Action? onFailed = null)
    {
        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myTask)
        {
            var tasks = consumers.Select(c => c.Invoke(task.Result).CoWait().HighSpeedEnumerator()).ToArray();
            if(tasks.Length > 0) yield return tasks.WaitAll();
        }
        return new CoChainedTask<ICommandToken, T>(task, val => new CoBuiltInTask<ICommandToken>(myTask => CoExecute(myTask)));
    }

    public static CoTask<ICommandToken> Action<T>(this CoTask<T> task, Action<T> action, Action? onFailed = null)
        => task.Do([val => { action.Invoke(val); return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token); }], onFailed);

    public static CoTask<T> Discard<T, S>(this CoTask<S> task)
    {
        if (typeof(T) == typeof(ICommandToken))
            task.ChainFast(_ => EmptyCommandToken.Token);
        return task.ChainFast(_ => default(T)!);
    }
}
