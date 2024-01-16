using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;
using Virial;

namespace Nebula.Events;

public class EventManager
{
    public record EventHandler(ILifespan lifespan, Action<object> handler);

    public static Dictionary<Type, List<EventHandler>> allHandlers = new();

    private static void RegisterEvent(ILifespan lifespan,Type eventType, Action<object> handler)
    {
        if(!allHandlers.TryGetValue(eventType,out var handlers)) {
            handlers = new List<EventHandler>();
            allHandlers[eventType] = handlers;
        }

        handlers.Add(new(lifespan, handler));
    }
    public static void RegisterEvent(ILifespan lifespan, object? handler)
    {
        if (handler == null) return;
        foreach (var method in handler.GetType().GetMethods().Where(method => !method.IsStatic && method.IsDefined(typeof(EventHandlerAttribute),true) && method.GetParameters().Length == 1))
        {
            RegisterEvent(lifespan, method.GetParameters()[0].ParameterType, (obj) => method.Invoke(handler, new object[] { obj }));
        }
    }

    public static Event HandleEvent<Event>(Event targetEvent) where Event : class
    {
        HandleEvent(typeof(Event), targetEvent);
        return targetEvent;
    }

    public static void HandleEvent(Type? eventType, object targetEvent)
    {
        if(eventType == null) return;

        HandleEvent(eventType.BaseType, targetEvent);
        
        if(allHandlers.TryGetValue(eventType, out var handlers)) {
            handlers.RemoveAll(handler =>
            {
                if (handler.lifespan.IsDeadObject) return true;

                if (handler.lifespan.IsAliveObject) handler.handler.Invoke(targetEvent);

                return false;
            });
        }
    }
}
