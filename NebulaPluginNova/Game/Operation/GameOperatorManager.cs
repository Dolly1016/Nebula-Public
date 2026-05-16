using Hazel;
using Il2CppSystem.Xml;
using Nebula.Modules.Logging;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering.VirtualTexturing;
using Virial;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Game;
using static Il2CppMono.Net.Security.MobileAuthenticatedStream;

namespace Nebula.Game;

internal record GameOperatorInstance(ILifespan lifespan, Action<object> action, int Priority);
internal class GameOperatorBuilder
{
    List<(Type type, Func<object, (Action<object>? generator, int priority)> action)> allActions;
    private Type entityType;

    private GameOperatorBuilder(Type entityType, List<(Type type, Func<object, (Action<object>? generator, int priority)> action)> actions)
    {
        this.entityType = entityType;
        this.allActions = actions;
    }

    private record AttributeInfo(MethodInfo Method)
    {
        public bool HasOnlyMyPlayerAttr = Method.GetCustomAttribute<OnlyMyPlayer>() != null;
        public bool HasLocalAttr = Method.GetCustomAttribute<Local>() != null;
        public bool HasOnlyLocalPlayerAttr = Method.GetCustomAttribute<OnlyLocalPlayer>() != null;
        public bool HasOnlyHostAttr = Method.GetCustomAttribute<OnlyHost>() != null;
    }
    static private Action<object>? BuildAction(object instance, Action<object, object> procedure, AttributeInfo attributes)
    {
        var pBinder = instance as IBindPlayer;
        Action<object>? myProcedure = e => procedure(instance, e);

        if (attributes.HasOnlyMyPlayerAttr)
        {
            var lastProcedure = myProcedure;
            myProcedure = e =>
            {
                byte p = Unsafe.As<AbstractPlayerEvent>(e).Player.PlayerId;
                if (p == (pBinder?.MyPlayer.PlayerId ?? byte.MaxValue)) lastProcedure.Invoke(e);
            };
        }

        if (attributes.HasLocalAttr && !(pBinder?.AmOwner ?? false)) return null;

        if (attributes.HasOnlyLocalPlayerAttr)
        {
            var lastProcedure = myProcedure;
            myProcedure = e =>
            {
                if (Unsafe.As<AbstractPlayerEvent>(e).Player.AmOwner) lastProcedure.Invoke(e);
            };
        }

        if (attributes.HasOnlyHostAttr)
        {
            var lastProcedure = myProcedure;
            myProcedure = e =>
            {
                if (AmongUsClient.Instance.AmHost) lastProcedure.Invoke(e);
            };
        }
        return myProcedure;
    }

    /// <summary>
    /// ゲーム作用素を拡張します。
    /// </summary>
    /// <typeparam name="T">entityTypeと同じものである必要があります。</typeparam>
    /// <typeparam name="E"></typeparam>
    /// <param name="operation"></param>
    /// <param name="priority"></param>
    public void Extend<T, E>(Action<T, E> operation, int priority) where E : Virial.Events.Event {
        var eventType = typeof(E);

        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var eventParam = Expression.Parameter(typeof(object), "ev");
        var operationConstant = Expression.Constant(operation, typeof(Action<T,E>));
        var convertEv = Expression.Convert(eventParam, eventType);
        var convertInstance = Expression.Convert(instanceParam, entityType);
        var call = Expression.Invoke(operationConstant, [convertInstance, convertEv]);
        var exp = Expression.Lambda<Action<object, object>>(call, instanceParam, eventParam).Compile();
        AttributeInfo attributes = new(operation.Method);

        allActions.Add((eventType, (instance) => (BuildAction(instance, exp, attributes), priority)));
    }

    /// <summary>
    /// ゲーム作用素を登録します。
    /// </summary>
    public void Register(Action<Type, GameOperatorInstance> registerFunc, ILifespan lifespan, IGameOperator operation)
    {
        foreach (var action in allActions) {
            var result = action.action.Invoke(operation);
            if (result.generator == null) continue;
            registerFunc.Invoke(action.type, new(lifespan, result.generator, result.priority));
        }
    }

    static public GameOperatorBuilder GetBuilderFromType(Type entityType)
    {
        List<(Type type, Func<object, (Action<object>? generator, int priority)> action)> builderActions = [];

        //公開メソッドをすべて拾い上げる
        IEnumerable<MethodInfo> methods = entityType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        //親に遡及して非公開メソッドを拾い上げる
        Type? baseType = entityType;
        while (baseType != null)
        {
            methods = methods.Concat(baseType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            baseType = baseType.BaseType;
        }

        //LogUtils.WriteToConsole($"Type: {entityType.Name}");

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) continue;

            if (!parameters[0].ParameterType.IsAssignableTo(typeof(Virial.Events.Event))) continue;

            //LogUtils.WriteToConsole($"{method.Name} (Event: {parameters[0].ParameterType.Name})");

            //メソッドとして抽出された匿名関数を除外 (意図して作ったメソッドではない)
            if (method.Name.StartsWith("<"))
            {
                //LogUtils.WriteToConsole($" -Excluded!");
                continue;
            }

            var eventType = parameters[0].ParameterType;

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var eventParam = Expression.Parameter(typeof(object), "ev");
            var convertEv = Expression.Convert(eventParam, eventType);
            var convertInstance = Expression.Convert(instanceParam, entityType);
            var call = Expression.Call(convertInstance, method, convertEv);
            var exp = Expression.Lambda<Action<object, object>>(call, instanceParam, eventParam).Compile();
            AttributeInfo attributes = new(method);
            int priority = method.GetCustomAttribute<EventPriority>()?.Priority ?? EventPriority.Default;

            builderActions.Add((eventType, (instance) => (BuildAction(instance, exp, attributes), priority)));
        }

        return new(entityType, builderActions);
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
    private Dictionary<Type, List<GameOperatorInstance>> allOperatorInstance = [];

    //特殊な作用素 (OnReleasedに限り、属性による指定なしでバインドされた寿命オブジェクトの寿命が尽きたときに個別に呼び出される。)
    private List<(ILifespan lifespan, IGameOperator operation)> allOperators = [];

    // 同じ型の作用素を登録する処理を高速化するためのキャッシュ
    static private Dictionary<Type, GameOperatorBuilder> allBuildersCache = [];

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
                    NebulaLogger.Instance.Error(ex.ToString());
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

    public E Run<E>(E ev, bool retroactive = false, bool shouldNotCheckGameEnd = true) where E : class, Virial.Events.Event
    {
        if (retroactive)
            DoRetroactiveOperation(ev, ev.GetType());
        else
            DoSingleOperation(ev, ev.GetType());

        //イベントを通して発生したゲーム終了をチェックする。
        if (!shouldNotCheckGameEnd) WinCheckBlocker.TryCheckGameEnd();

        return ev;
    }

    // 作用素の反復中にこれを呼び出さないこと(InvalidOperationExceptionが発生する)
    public void Update()
    {
        //新たな作用素を追加
        RegisterAll();

        WrapUpDeadLifespans();
    }

    public void WrapUpDeadLifespans()
    {
        //OnReleasedの呼び出し
        allOperators.RemoveAll(tuple =>
        {
            if (tuple.lifespan.IsDeadObject)
            {
                tuple.operation.OnReleased();
                operatorsDic.Remove(tuple.operation);
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
    private List<(IGameOperator entity, ILifespan lifespan, Action? onSubscribed)> newOperations = [];
    private List<(Type eventType, Action<object> operation, ILifespan lifespan, int priority)> newFuncOperations = [];

    static private GameOperatorBuilder GetBuilder(Type type)
    {
        if (!allBuildersCache.TryGetValue(type, out var builder))
        {
            builder = GameOperatorBuilder.GetBuilderFromType(type);
            allBuildersCache.Add(type, builder);
        }
        return builder;
    }

    static public void RegisterExtension<T, E>(Action<T, E> operation, int priority = EventPriority.Default) where E : Virial.Events.Event
    {
        GameOperatorBuilder builder = GetBuilder(typeof(T));
        builder.Extend<T, E>(operation, priority);

    }

    private void RegisterEntity(IGameOperator operation, ILifespan lifespan)
    {
        allOperators.Add((lifespan, operation));
        GameOperatorBuilder builder = GetBuilder(operation.GetType());
        builder.Register(RegisterImpl, lifespan, operation);
    }

    private void RegisterImpl(Type eventType, GameOperatorInstance instance) {
        List<GameOperatorInstance>? instanceList;
        if (!allOperatorInstance.TryGetValue(eventType, out instanceList))
        {
            instanceList = new();
            allOperatorInstance.Add(eventType, instanceList);
        }

        int index = instanceList.FindIndex(0, existed => existed.Priority < instance.Priority);
        if (index != -1)
        {
            instanceList.Insert(index, instance);
        }
        else
        {
            instanceList.Add(instance);
        }
    }
    

    //退避されていた作用素をゲームに追加する
    private void RegisterAll()
    {
        foreach (var entry in newOperations)
        {
            RegisterEntity(entry.entity, entry.lifespan);
            entry.onSubscribed?.Invoke();
        }
        foreach (var op in newFuncOperations) RegisterImpl(op.eventType, new(op.lifespan, op.operation, op.priority));
        
        newOperations.Clear();
        newFuncOperations.Clear();
    }

    private class ReleaseAction : IGameOperator
    {
        public ReleaseAction(Action action) => releaseAction = action;
        private Action releaseAction;
        void IGameOperator.OnReleased() => releaseAction?.Invoke();
    }
    public void RegisterOnReleased(Action onReleased, ILifespan lifespan, Action? onSubscribed = null) => Subscribe(new ReleaseAction(onReleased), lifespan, onSubscribed);

    public void Subscribe(IGameComponent component, ILifespan lifespan, Action? onSubscribed = null)
    {
        if (component is INestedLifespan nl && nl != lifespan)
        {
            //入れ子にするタイプの寿命オブジェクトなら指定した寿命オブジェクトの入れ子にする。
            if(nl.Bind(lifespan)) lifespan = nl;
        }
        if (component is IGameOperator gameOperator)
        {
            newOperations.Add((gameOperator, lifespan, onSubscribed));
            operatorsDic[gameOperator] = lifespan;
        }
    }

    Dictionary<IGameOperator, ILifespan> operatorsDic = [];
    public bool CheckAndRegister(IGameOperator entity, ILifespan lifespan, Action? onSubscribed = null)
    {
        if (operatorsDic.ContainsKey(entity)) return false;
        Subscribe(entity, lifespan, onSubscribed);
        return true;
    }

    /// <summary>
    /// 親の作用素と同じ寿命で作用素を登録します。
    /// 作用素は多重で登録されません。
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="parent"></param>
    /// <returns></returns>
    public bool SearchAndSubscribe(IGameOperator entity, IGameOperator parent)
    {
        if (operatorsDic.TryGetValue(parent, out var lifespan))
        {
            return CheckAndRegister(entity, lifespan);
        }
        return false;
    }

    public void Subscribe<Event>(Action<Event> operation, ILifespan lifespan, int priority = 100)
    {
        newFuncOperations.Add((typeof(Event), obj => operation.Invoke((Event)obj), lifespan, priority));
    }

    public void SubscribeAchievement<Event>(string achievement, Func<Event, bool> predicate, ILifespan lifespan, int priority = 100) =>
        Subscribe<Event>(ev =>
        {
            if (predicate.Invoke(ev))
            {
                new StaticAchievementToken(achievement);
            }
        }, lifespan, priority);
    public void SubscribeSingleListener<Event>(Action<Event> operation, ILifespan? lifespan = null, int priority = 100)
    {
        bool called = false;
        newFuncOperations.Add((typeof(Event), obj =>
        {
            operation.Invoke((Event)obj);
            called = true;
        }, new FunctionalLifespan(()=> (lifespan?.IsAliveObject ?? true) && !called), priority));
    }

    public void RegisterReleasedAction(Action onReleased, ILifespan lifespan)
    {
        newOperations.Add((new LambdaOnReleaseOperator(onReleased), lifespan, null));
    }
    public GameOperatorManager()
    {
        instance = this;
    }

    
    public IEnumerable<IGameOperator> AllOperators  { get { foreach (var op in allOperators) yield return op.operation; } }
}

public static class GameOperatorHelpers
{
    static public GameObject BindGameObject(this ILifespan lifespan, GameObject obj)
    {
        GameOperatorManager.Instance?.RegisterOnReleased(() => { if (obj) GameObject.Destroy(obj); }, lifespan, null);
        return obj;
    }
}

public static class WinCheckBlocker
{
    public class WinCheckSection : IDisposable
    {
        public string Name;
        public bool blockedCheckWinning = false;
        public void Dispose()
        {
            if (currentSection != this) return;
            currentSection = null;
            if (blockedCheckWinning) CheckGameEnd();
        }

        public WinCheckSection(string? name = null)
        {
            Name = name ?? "Untitled";
            if (currentSection == null) currentSection = this;
        }
    }

    static public WinCheckSection CreateSection(string? label = null) => new WinCheckSection(label);

    static WinCheckSection? currentSection = null;

    internal static void TryCheckGameEnd()
    {
        if (currentSection != null)
        {
            currentSection.blockedCheckWinning = true;
        }
        else
        {
            CheckGameEnd();
        }
    }

    //単純にゲーム終了のチェックを実行します。
    private static void CheckGameEnd()
    {
        NebulaGameManager.Instance?.CriteriaManager.CheckAndTriggerGameEnd();
    }

}
