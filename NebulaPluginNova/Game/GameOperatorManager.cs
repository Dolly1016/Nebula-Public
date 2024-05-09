using Virial;
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
    public void Register(Dictionary<Type, List<GameOperatorInstance>> runtimeOperators, ILifespan lifespan, IGameEntity operation)
    {
        foreach(var action in allActions)
        {
            List<GameOperatorInstance>? instanceList;
            if(!runtimeOperators.TryGetValue(action.type, out instanceList))
            {
                instanceList = new();
                runtimeOperators.Add(action.type, instanceList);
            }
            instanceList.Add(new(lifespan, action.action.Invoke(operation)));
        }
    }

    static public GameOperatorBuilder GetBuilderFromType(Type entityType)
    {
        List<(Type type, Func<object, Action<object>> action)> builderActions = new();

        foreach(var method in entityType.GetMethods())
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) continue;

            if (!parameters[0].ParameterType.IsAssignableTo(typeof(Virial.Events.Event))) continue;

            var eventType = parameters[0].ParameterType;
            builderActions.Add((eventType, (instance) => (e) => { method.Invoke(instance, [e]); }));
        }

        return new(builderActions);
    }
}



public class GameOperatorManager
{
    static private GameOperatorManager? instance;
    static public GameOperatorManager? Instance => instance;

    // 現在有効な作用素
    private Dictionary<Type, List<GameOperatorInstance>> allOperators = new();

    //特殊な作用素 (OnReleasedに限り、属性による指定なしでバインドされた寿命オブジェクトの寿命が尽きたときに個別に呼び出される。)
    private List<(ILifespan lifespan, Action action)> onReleasedOperators = new();

    // 同じ型の作用素を登録する処理を高速化するためのキャッシュ
    static private Dictionary<Type, GameOperatorBuilder> allBuildersCache = new();

    private void DoSingleOperation(object e, Type type)
    {
        if(allOperators.TryGetValue(type, out var operators))
        {
            operators.RemoveAll(o =>
            {
                if (o.lifespan.IsDeadObject)
                    return true;
                o.action.Invoke(e);
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

    public void Run<E>(E ev, bool retroactive = false) where E : class, Virial.Events.Event
    {
        if (retroactive)
            DoRetroactiveOperation(ev, ev.GetType());
        else
            DoRetroactiveOperation(ev, ev.GetType());
    }


    // 作用素の反復中にこれを呼び出さないこと(InvalidOperationExceptionが発生する)
    public void Update()
    {
        //新たな作用素を追加
        RegisterAll();

        //OnReleasedの呼び出し
        onReleasedOperators.RemoveAll(tuple =>
        {
            if (tuple.lifespan.IsDeadObject)
            {
                tuple.action.Invoke();
                return true;
            }
            return false;
        });
    }

    public void Abandon()
    {
        //コレクションの中身を全消去
        allOperators.Do(entry => entry.Value.Clear());
        allOperators.Clear();
        onReleasedOperators.Do(t => t.action.Invoke());
        onReleasedOperators.Clear();

        if (instance == this) instance = null;
    }

    // 反復中に作用素が追加されないよう、一時的に退避する
    private List<(IGameEntity entity, ILifespan lifespan)> newOperations = new();

    private void RegisterEntity(IGameEntity operation, ILifespan lifespan)
    {
        onReleasedOperators.Add((lifespan, operation.OnReleased));
        var operationType = operation.GetType();
        GameOperatorBuilder? builder;
        if(!allBuildersCache.TryGetValue(operationType, out builder))
        {
            builder = GameOperatorBuilder.GetBuilderFromType(operationType);
            allBuildersCache.Add(operationType, builder);
        }
        builder.Register(allOperators, lifespan, operation);
    }

    //退避されていた作用素をゲームに追加する
    private void RegisterAll()
    {
        foreach (var entry in newOperations) RegisterEntity(entry.entity, entry.lifespan);
        newOperations.Clear();
    }

    public void Register(IGameEntity entity, ILifespan lifespan)
    {
        newOperations.Add((entity, lifespan));
    }

    public GameOperatorManager()
    {
        instance = this;
    }
}