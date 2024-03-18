using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Game;

namespace Nebula.Game;

public class GameEntityManager
{
    static public GameEntityManager? Instance => NebulaGameManager.Instance?.GameEntityManager;

    //分類された全ゲームコンポーネント
    private HashSet<(ILifespan lifespan, IGameEntity entity)> otherGameEntities = new();
    private Dictionary<byte, HashSet<(ILifespan lifespan, IGamePlayerEntity playerEntity)>> playerGameEntities = new();

    public IEnumerable<IGameEntity> AllEntities => GetAllAsEntity();

    public IEnumerable<IGameEntity> GetAllAsEntity()
    {
        foreach (var tuple in otherGameEntities) yield return tuple.entity;
        foreach (var l in playerGameEntities.Values) foreach (var tuple in l) yield return tuple.playerEntity;

        yield break;
    }

    public IEnumerable<IGamePlayerEntity> GetPlayerEntities(byte playerId)
    {
        if (playerGameEntities.TryGetValue(playerId, out var list)) foreach (var t in list) yield return t.playerEntity;
        
        yield break;
    }

    public IEnumerable<IGamePlayerEntity> GetPlayerEntities(Virial.Game.Player player) => GetPlayerEntities(player.PlayerId);
    public IEnumerable<IGamePlayerEntity> GetPlayerEntities(PlayerControl player) => GetPlayerEntities(player.PlayerId);
    
    private bool CheckAndRelease(ILifespan lifespan, IGameEntity entity)
    {
        if (lifespan.IsDeadObject)
        {
            entity.OnReleased();
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Update()
    {
        //新たなEntityを追加
        RegisterAll();

        otherGameEntities.RemoveWhere(t => CheckAndRelease(t.lifespan,t.entity));
        foreach (var l in playerGameEntities.Values) l.RemoveWhere(t => CheckAndRelease(t.lifespan, t.playerEntity));
    }

    public void Abandon()
    {
        AllEntities.Do(e => e.OnReleased());
        otherGameEntities.Clear();
        playerGameEntities.Do(entry => entry.Value.Clear());
        playerGameEntities.Clear();
    }

    private void RegisterPlayerComponent(IGamePlayerEntity playerEntity, ILifespan lifespan)
    {
        var playerId = playerEntity.MyPlayer.PlayerId;
        if (!playerGameEntities.TryGetValue(playerId, out var list))
        {
            list = new();
            playerGameEntities.Add(playerId, list);
        }

        list.Add((lifespan, playerEntity));
    }

    private List<(IGameEntity entity, ILifespan lifespan)> newEntities = new();

    private void RegisterEntity(IGameEntity entity, ILifespan lifespan)
    {
        if (entity is IGamePlayerEntity playerEntity)
            RegisterPlayerComponent(playerEntity, lifespan);
        else
            otherGameEntities.Add((lifespan, entity));
    }

    private void RegisterAll()
    {
        foreach (var entry in newEntities) RegisterEntity(entry.entity, entry.lifespan);
        newEntities.Clear();
    }

    public void Register(IGameEntity entity, ILifespan lifespan)
    {
        newEntities.Add((entity, lifespan));
    }
}

public static class GameEntityManagerHelper
{
    static public IEnumerable<IGamePlayerEntity>? RelatedEntities(this GamePlayer player) => GameEntityManager.Instance?.GetPlayerEntities(player.PlayerId);
}