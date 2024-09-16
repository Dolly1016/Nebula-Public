﻿using System.Reflection;
using Virial;
using Virial.Attributes;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Game;

internal record GameOperatorInstance(ILifespan lifespan, Action<object> action);
internal class GameOperatorBuilder
{
    List<(Type type, Func<object, Action<object>> action)> allActions;

    private GameOperatorBuilder(List<(Type type, Func<object, Action<object>> action)> actions)
    {
        this.allActions = actions;
    }


    /// <summary>
    /// ゲーム作用素を登録します。
    /// </summary>
    public void Register(Action<Type, GameOperatorInstance> registerFunc, ILifespan lifespan, IGameOperator operation)
    {
        foreach(var action in allActions) registerFunc.Invoke(action.type, new(lifespan, action.action.Invoke(operation)));
    }

    static public GameOperatorBuilder GetBuilderFromType(Type entityType)
    {
        List<(Type type, Func<object, Action<object>> action)> builderActions = new();

        //公開メソッドをすべて拾い上げる
        IEnumerable<MethodInfo> methods = entityType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        //親に遡及して非公開メソッドを拾い上げる
        Type? baseType = entityType;
        while (baseType != null)
        {
            methods = methods.Concat(baseType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            baseType = baseType.BaseType;
        }
        

        foreach(var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) continue;

            if (!parameters[0].ParameterType.IsAssignableTo(typeof(Virial.Events.Event))) continue;

            var eventType = parameters[0].ParameterType;


            Action<object, object> procedure = (instance, e) => method.Invoke(instance, [e]);


            if (method.GetCustomAttribute<OnlyMyPlayer>() != null)
            {
                var lastAction = procedure;
                procedure = (instance, e) =>
                {
                    if ((e as AbstractPlayerEvent)?.Player == (instance as IBindPlayer)?.MyPlayer) lastAction.Invoke(instance, e);
                };
            }

            if (method.GetCustomAttribute<Local>() != null)
            {
                var lastAction = procedure;
                procedure = (instance, e) =>
                {
                    if ((instance as IBindPlayer)?.AmOwner ?? false) lastAction.Invoke(instance, e);
                };
            }

            if (method.GetCustomAttribute<OnlyHost>() != null)
            {
                var lastAction = procedure;
                procedure = (instance, e) =>
                {
                    if (AmongUsClient.Instance.AmHost) lastAction.Invoke(instance, e);
                };
            }

            builderActions.Add((eventType, (instance) => (e) => procedure.Invoke(instance,e)));
        }

        return new(builderActions);
    }
}



public class GameOperatorManager
{
    private class LambdaOnReleaseOperator : IGameOperator
    {
        Action onReleased;
        void IGameOperator.OnReleased() => onReleased?.Invoke();
        public LambdaOnReleaseOperator(Action onReleased)
        {
            this.onReleased = onReleased;
        }
    }

    static private GameOperatorManager? instance;
    static public GameOperatorManager? Instance => instance;

    // 現在有効な作用素
    private Dictionary<Type, List<GameOperatorInstance>> allOperatorInstance = new();

    //特殊な作用素 (OnReleasedに限り、属性による指定なしでバインドされた寿命オブジェクトの寿命が尽きたときに個別に呼び出される。)
    private List<(ILifespan lifespan, IGameOperator operation)> allOperators = new();

    // 同じ型の作用素を登録する処理を高速化するためのキャッシュ
    static private Dictionary<Type, GameOperatorBuilder> allBuildersCache = new();

    private void DoSingleOperation(object e, Type type)
    {
        if (allOperatorInstance.TryGetValue(type, out var operators))
        {
            operators.RemoveAll(o =>
            {
                try
                {
                    if (o.lifespan.IsDeadObject) return true;
                }
                catch { return true; }

                try
                {
                    o.action.Invoke(e);
                }catch (Exception ex)
                {
                    NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, ex.ToString());
                }
                return false;
            });
        }
    }

    private void DoRetroactiveOperation(object e, Type type)
    {
        while (type != null)
        {
            DoSingleOperation(e, type);
            type = type.BaseType!;
        }
    }

    public E Run<E>(E ev, bool retroactive = false) where E : class, Virial.Events.Event
    {
        if (retroactive)
            DoRetroactiveOperation(ev, ev.GetType());
        else
            DoSingleOperation(ev, ev.GetType());

        //イベントを通して発生したゲーム終了をチェックする。
        NebulaGameManager.Instance?.CriteriaManager.CheckAndTriggerGameEnd();

        return ev;
    }


    // 作用素の反復中にこれを呼び出さないこと(InvalidOperationExceptionが発生する)
    public void Update()
    {
        //新たな作用素を追加
        RegisterAll();

        //OnReleasedの呼び出し
        allOperators.RemoveAll(tuple =>
        {
            if (tuple.lifespan.IsDeadObject)
            {
                tuple.operation.OnReleased();
                return true;
            }
            return false;
        });
    }

    public void Abandon()
    {
        //コレクションの中身を全消去
        allOperatorInstance.Do(entry => entry.Value.Clear());
        allOperatorInstance.Clear();
        allOperators.Do(t =>
        {
            try
            {
                t.operation.OnReleased();
            }
            catch(Exception e) {
                Debug.LogError(e.ToString());
            }
        });
        allOperators.Clear();

        if (instance == this) instance = null;
    }

    // 反復中に作用素が追加されないよう、一時的に退避する
    private List<(IGameOperator entity, ILifespan lifespan)> newOperations = new();
    private List<(Type eventType, Action<object> operation, ILifespan lifespan)> newFuncOperations = new();

    private void RegisterEntity(IGameOperator operation, ILifespan lifespan)
    {
        allOperators.Add((lifespan, operation));

        var operationType = operation.GetType();
        GameOperatorBuilder? builder;
        if (!allBuildersCache.TryGetValue(operationType, out builder))
        {
            builder = GameOperatorBuilder.GetBuilderFromType(operationType);
            allBuildersCache.Add(operationType, builder);
        }
        builder.Register(RegisterImpl, lifespan, operation);
    }

    private void RegisterImpl(Type eventType, GameOperatorInstance instance) {
        List<GameOperatorInstance>? instanceList;
        if (!allOperatorInstance.TryGetValue(eventType, out instanceList))
        {
            instanceList = new();
            allOperatorInstance.Add(eventType, instanceList);
        }
        instanceList.Add(instance);
    }
    

    //退避されていた作用素をゲームに追加する
    private void RegisterAll()
    {
        foreach (var entry in newOperations) RegisterEntity(entry.entity, entry.lifespan);
        foreach (var op in newFuncOperations) RegisterImpl(op.eventType, new(op.lifespan, op.operation));
        newOperations.Clear();
        newFuncOperations.Clear();
    }

    public void Register(IGameOperator entity, ILifespan lifespan)
    {
        newOperations.Add((entity, lifespan));
    }

    public void Register<Event>(Action<Event> operation, ILifespan lifespan)
    {
        newFuncOperations.Add((typeof(Event), obj => operation.Invoke((Event)obj), lifespan));
    }

    public void RegisterReleasedAction(Action onReleased, ILifespan lifespan)
    {
        newOperations.Add((new LambdaOnReleaseOperator(onReleased), lifespan));
    }
    public GameOperatorManager()
    {
        instance = this;
    }

    
    public IEnumerable<IGameOperator> AllOperators  { get { foreach (var op in allOperators) yield return op.operation; } }
}