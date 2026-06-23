using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム中に毎ティック発火します。
/// </summary>
[RecyclableEvent]
public class GameUpdateEvent : AbstractGameEvent
{
    public float DeltaTime { get; private set; }
    public float GameTime { get; private set; }
    public float ProcessTime { get; private set; }
    
    private GameUpdateEvent() : base(null!) {
    }

    static GameUpdateEvent ev = new();
    static internal GameUpdateEvent Get(Virial.Game.Game game, float deltaTime, float gameTime, float processTime)
    {
        ev.Recycle(game);
        ev.DeltaTime = deltaTime;
        ev.GameTime = gameTime;
        ev.ProcessTime = processTime;
        return ev;
    }
}

/// <summary>
/// 毎ティック発火します。
/// </summary>
[RecyclableEvent]

public class UpdateEvent : Event
{
    private UpdateEvent() { }

    public float DeltaTime { get; private set; }

    static private UpdateEvent ev = new();
    static internal UpdateEvent Get(float fixedDeltaTime)
    {
        ev.DeltaTime = fixedDeltaTime;
        return ev;
    }
}

/// <summary>
/// ゲームのHUDを更新する際に発火します。
/// ここでプレイヤーの輪郭を変更すると、変更が画面に正しく反映されます。
/// </summary>
[RecyclableEvent]
public class GameHudUpdateEvent : AbstractGameEvent
{
    public float DeltaTime { get; private set; }
    public float GameTime { get; private set; }
    public float ProcessTime { get; private set; }

    private GameHudUpdateEvent() : base(null) { }
    static private GameHudUpdateEvent ev = new();
    static internal GameHudUpdateEvent Get(Virial.Game.Game game, float deltaTime, float gameTime, float processTime)
    {
        ev.Recycle(game);
        ev.DeltaTime = deltaTime;
        ev.GameTime = gameTime;
        ev.ProcessTime = processTime;
        return ev;
    }
}

/// <summary>
/// LateUpdateのタイミングで発火します。
/// </summary>
[RecyclableEvent]
public class GameLateUpdateEvent : AbstractGameEvent
{
    private GameLateUpdateEvent(): base(null!) { }
    static private GameLateUpdateEvent ev = new();
    static internal GameLateUpdateEvent Get(Virial.Game.Game game)
    {
        ev.Recycle(game);
        return ev;
    }
}
