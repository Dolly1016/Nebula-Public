using Nebula.Modules.Cosmetics;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

/// <summary>
/// GameObjectに紐づいたMod製のスクリプトを紐づけられるマップ。
/// イベントハンドラと無関係で、より低レベルなAPIです。
/// </summary>
/// <typeparam name="T"></typeparam>
internal class CustomHooksManager<T> where T : class
{
    private class CustomHooks<T>
    {
        
        public CustomHooks(GameObject obj, T hooks)
        {
            Obj = obj;
            Hooks = hooks;
        }

        public GameObject Obj { get; }
        public T Hooks { get; }
    }

    private Dictionary<int, CustomHooks<T>> allHooks = [];
    private Func<T> constructor;

    public CustomHooksManager(Func<T> constructor)
    {
        this.constructor = constructor;
    }

    public bool TryGet(GameObject obj, [MaybeNullWhen(false)] out T found)
    {
        if(allHooks.TryGetValue(obj.GetInstanceID(), out var wrapped))
        {
            found = wrapped.Hooks;
            return true;
        }
        found = null;
        return false;
    }


    public T Get(GameObject obj)
    {
        if (allHooks.TryGetValue(obj.GetInstanceID(), out var found) && found.Obj == obj) return found.Hooks;
        var generated = constructor.Invoke();
        allHooks[obj.GetInstanceID()] = new(obj, generated);
        return generated;

    }
}

/// <summary>
/// CustomHooksManagerで使用する、単純なアクションのリストです。
/// </summary>
internal class SimpleActionList
{
    List<Func<bool>> actions = [];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="action">有効な間trueを返し続けるアクション</param>
    public void AddAction(Func<bool> action)
    {
        actions.Add(action);
    }

    public void Update()
    {
        actions.RemoveAll(action => !action.Invoke());
    }
}