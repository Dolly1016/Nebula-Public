using Nebula.Roles.Impostor;
using Virial;
using Virial.DI;
using Virial.Game;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace Nebula.Map;

/// <summary>
/// マップ上に出現するオブジェクトのスポーン位置
/// </summary>
public class MapObjectPoint
{
    Virial.Compat.Vector2 point;
    MapObjectType type;

    public MapObjectPoint(float x, float y, MapObjectType type)
    {
        point = new(x,y);
        this.type = type;
    }

    public Virial.Compat.Vector2 Point => point;
    public MapObjectType Type => type;
    public int Id = -1;
}

[NebulaRPCHolder]
internal class MapObjectSpawner : AbstractModule<Virial.Game.Game>, IMapObjectSpawner
{
    static MapObjectSpawner() => DIManager.Instance.RegisterModule(() => new MapObjectSpawner());

    List<MapObjectPoint>? unusedPoints;
    Dictionary<string, List<Virial.Compat.Vector2>> usedPoints = new();

    bool TryGetPoint(string tag, string? objectTag, float distance, MapObjectType type, out Virial.Compat.Vector2 point, out NebulaSyncObjectReference? reference)
    {
        reference = null;

        if ((unusedPoints?.Count ?? 0) == 0)
        {
            point = new();
            return false;
        }

        if (!usedPoints.TryGetValue(tag, out var used))
        {
            used = new();
            usedPoints[tag] = used;
        }

        var squaredDistance = distance * distance;
        var cand = unusedPoints!.Where(p => (p.Type & type) != 0 && used.All(u =>  p.Point.SquaredDistance(u) > squaredDistance)).ToArray();
        if (cand.Length == 0)
        {
            point = new();
            return false;
        }

        int random = System.Random.Shared.Next(cand.Length);

        var selected = cand[random];

        unusedPoints?.Remove(selected);
        used.Add(selected.Point);

        using (RPCRouter.CreateSection("MapObject"))
        {
            RpcSpawn.Invoke((selected.Id, tag));
            if (objectTag != null) reference = NebulaSyncObject.RpcInstantiate(objectTag!, [selected.Point.x, selected.Point.y]);
        }

        point = selected.Point;
        
        return true;
    }

    void TryInitialize()
    {
        if (!ShipStatus.Instance) return;

        if (unusedPoints == null)
        {
            unusedPoints = new(MapData.GetCurrentMapData().MapObjectPoints);
            for (int i = 0; i < unusedPoints.Count; i++) unusedPoints[i].Id = i;
        }
    }

    Virial.Compat.Vector2[] IMapObjectSpawner.Spawn(int num, float distance, string reason, string? objectConstructor, MapObjectType type)
    {
        IEnumerable<Virial.Compat.Vector2> subroutineSpawn()
        {
            if (!ShipStatus.Instance) yield break;

            TryInitialize();

            for (int i = 0; i < num; i++)
            {
                if (TryGetPoint(reason, objectConstructor, distance, type, out var point, out var reference)) yield return point;
                else yield break;
            }
        }

        var result = subroutineSpawn().ToArray();
        return result;
    }

    void IMapObjectSpawner.Spawn(int id, string reason)
    {
        TryInitialize();

        var selected = unusedPoints?.FirstOrDefault(p => p.Id == id);
        if (selected != null)
        {
            unusedPoints?.Remove(selected);

            if (!usedPoints.TryGetValue(reason, out var used))
            {
                used = new();
                usedPoints[reason] = used;
            }

            used.Add(selected.Point);
        }
    }

    static RemoteProcess<(int id, string reason)> RpcSpawn = new("SpawnMapObject", (message, calledByMe) =>
    {
        if(!calledByMe) NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>()?.Spawn(message.id, message.reason);
    });
}

public abstract class MapData
{
    abstract protected Vector2[] MapArea { get; }
    abstract protected Vector2[] NonMapArea { get; }
    virtual public Vector2[][] RaiderIgnoreArea { get => []; }
    abstract protected SystemTypes[] SabotageTypes { get; }
    abstract public MapObjectPoint[] MapObjectPoints { get; }

    public SystemTypes[] GetSabotageSystemTypes() => SabotageTypes;
    public bool CheckMapArea(Vector2 position, float radious = 0.1f)
    {
        if (radious > 0f)
        {
            int num = Physics2D.OverlapCircleNonAlloc(position, radious, PhysicsHelpers.colliderHits, Constants.ShipAndAllObjectsMask);
            if (num > 0) for (int i = 0; i < num; i++) if (!PhysicsHelpers.colliderHits[i].isTrigger) return false;
        }

        return CheckMapAreaInternal(position);
    }

    public bool CheckMapAreaInternal(Vector2 position)
    {
        Vector2 vector;
        float magnitude;

        foreach (Vector2 p in NonMapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 6.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) return false;
        }

        foreach (Vector2 p in MapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 12.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) return true;
        }

        return false;
    }

    public int CheckMapAreaDebug(Vector2 position)
    {
        Vector2 vector;
        float magnitude;
        int count = 0;

        foreach (Vector2 p in NonMapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 6.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) return 0;
        }

        foreach (Vector2 p in MapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 12.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) count++;
        }

        return count;
    }

    private static Texture2D CreateReadabeTexture(Texture texture, int margin = 0)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

        Graphics.Blit(texture, renderTexture);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D readableTextur2D = new Texture2D(texture.width + margin * 2, texture.height + margin * 2);
        readableTextur2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), margin, margin);
        readableTextur2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        return readableTextur2D;
    } 
    public Texture2D OutputMap(Vector2 center, Vector2 size, float resolution = 10f)
    {
        int x1, y1, x2, y2;
        x1 = (int)((center.x - size.x * 0.5f) * resolution);
        y1 = (int)((center.y - size.y * 0.5f) * resolution);
        x2 = (int)((center.x + size.x * 0.5f) * resolution);
        y2 = (int)((center.y + size.y * 0.5f) * resolution);
        int temp;
        if (x1 > x2)
        {
            temp = x1;
            x1 = x2;
            x2 = temp;
        }
        if (y1 > y2)
        {
            temp = y1;
            y1 = y2;
            y2 = temp;
        }

        Color color = new Color(40 / 255f, 40 / 255f, 40 / 255f);
        var texture = new Texture2D(x2 - x1, y2 - y1, TextureFormat.RGB24, false);

        int num;
        int r = 0;
        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                num = CheckMapAreaDebug(new Vector2(((float)x) / resolution, ((float)y) / resolution));
                texture.SetPixel(x - x1, y - y1, (num == 0) ? color : new Color((num > 1 ? 100 : 0) / 255f, (150 + (num * 5)) / 255f, 0));
                if (num > 0) r++;
            }
        }

        texture.Apply();

        return CreateReadabeTexture(texture);
    }

    static private MapData[] AllMapData = new MapData[] { new SkeldData(), new MiraData(), new PolusData(), null!, new AirshipData(), new FungleData() };
    static public MapData GetCurrentMapData() => AllMapData[AmongUsUtil.CurrentMapId];
}
