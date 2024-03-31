using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppSystem.CodeDom.Compiler;

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
public class CoImmediateErrorTask<T> : CoTask<T>
{
    ICommandLogger logger;
    string? errorMessage;
    public IEnumerator CoWait()
    {
        if (errorMessage != null) logger.PushError(errorMessage);
        yield break;
    }
    public bool IsCompleted => true;
    public bool IsFailed => true;
    public T Result { get => throw new Exception("It is failed task."); }

    public CoImmediateErrorTask(ICommandLogger logger, string? errorMessage = null)
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
    Action? onFailed;

    public IEnumerator CoWait()
    {
        yield return precedeTask.CoWait();
        if (precedeTask.IsFailed)
        {
            IsCompleted = true;
            IsFailed = true;
            onFailed?.Invoke();
            yield break;
        }

        var follower = followerSupplier.Invoke(precedeTask.Result);
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

    public CoChainedTask(CoTask<S> precedeTask, TaskSupplier<T, S> followerSupplier, Action? onFailed = null)
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
    public static CoTask<T> Chain<T, S>(this CoTask<S> precedeTask, TaskSupplier<T, S> followerSupplier, Action? onFailed = null)
    {
        return new CoChainedTask<T, S>(precedeTask, result => followerSupplier(result), onFailed);
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
    public static CoTask<IEnumerable<T>> Select<T, V>(this CoTask<IEnumerable<V>> precedeTask, TaskSupplier<T, V> supplier, Action? onFailed = null)
    {
        return new CoChainedTask<IEnumerable<T>, IEnumerable<V>>(precedeTask, result =>
        {
            IEnumerator CoExecute(CoBuiltInTask<IEnumerable<T>> myResult) {
                List<T> list = new();
                myResult.Result = list;

                foreach (var v in result ?? [])
                {
                    var task = supplier.Invoke(v);
                    yield return task.CoWait();
                    if (!task.IsFailed) list.Add(task.Result);
                }
                yield break;
            }
            return new CoBuiltInTask<IEnumerable<T>>(CoExecute);
        }, onFailed);
    }

    public static CoTask<IEnumerable<T>> As<T>(this CoTask<IEnumerable<ICommandToken>> precedeTask, ICommandLogger logger, ICommandModifier argumentTable, ICommandExecutor executor, Action? onFailed = null) => precedeTask.Select(token => token.AsValue<T>(logger, executor, argumentTable), onFailed);

    /// <summary>
    /// 条件に沿った値のみを抽出します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="precedeTask"></param>
    /// <param name="predicate"></param>
    /// <param name="onFailed"></param>
    /// <returns></returns>
    public static CoTask<IEnumerable<T>> Where<T>(this CoTask<IEnumerable<T>> precedeTask, Predicate<T> predicate, Action? onFailed = null)
    {
        return new CoChainedTask<IEnumerable<T>, IEnumerable<T>>(precedeTask,
            result => new CoImmediateTask<IEnumerable<T>>(result.Where(v => predicate.Invoke(v)))
        , onFailed);
    }

    public static CoTask<ICommandToken> Do<T>(this CoTask<IEnumerable<T>> precedeTask, Action<T> consumer, Action? onFailed = null)
    {
        return new CoChainedTask<ICommandToken, IEnumerable<T>>(precedeTask, result =>
        {
            foreach (var v in result) consumer.Invoke(v);
            return new CoImmediateTask<ICommandToken>(new EmptyCommandToken());
        }, onFailed);
    }
}
