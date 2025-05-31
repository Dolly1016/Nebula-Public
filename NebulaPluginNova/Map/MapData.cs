using MS.Internal.Xml.XPath;
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
    Dictionary<string, List<Virial.Compat.Vector2>> usedPoints = [];

    bool TryGetPoint(string tag, string? objectTag, float distance, MapObjectType type, out Virial.Compat.Vector2 point, out NebulaSyncObjectReference? reference, MapObjectCondition[] conditions)
    {
        reference = null;

        if ((unusedPoints?.Count ?? 0) == 0)
        {
            point = new();
            return false;
        }

        if (!usedPoints.TryGetValue(tag, out var used))
        {
            used = [];
            usedPoints[tag] = used;
        }

        bool Met(MapObjectCondition condition, Vector2 point)
        {
            return !usedPoints.TryGetValue(condition.Tag, out var used) || used.All(u => u.SquaredDistance(point) > condition.Distance * condition.Distance);
        }

        var squared2 = 2 * 2;
        var squaredDistance = distance * distance;
        var cand = unusedPoints!.Where(p => 
            (p.Type & type) != 0 && //タイプが一致
            used.All(u =>  p.Point.SquaredDistance(u) > squaredDistance) && //指定の距離だけ離れている
            usedPoints.Values.All(points => points.All(u => p.Point.SquaredDistance(u) > squared2)) && //使用済みの全点から多少離れている
            conditions.All(c => Met(c, p.Point)) //その他の条件をすべて満たしている
            ).ToArray();
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


    Virial.Compat.Vector2[] IMapObjectSpawner.Spawn(int num, float distance, string reason, string? objectConstructor, MapObjectType type, MapObjectCondition[]? conditions = null)
    {
        IEnumerable<Virial.Compat.Vector2> subroutineSpawn()
        {
            if (!ShipStatus.Instance) yield break;

            TryInitialize();

            for (int i = 0; i < num; i++)
            {
                if (TryGetPoint(reason, objectConstructor, distance, type, out var point, out var reference, conditions ?? [])) yield return point;
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
                used = [];
                usedPoints[reason] = used;
            }

            used.Add(selected.Point);
        }
    }

    static private readonly RemoteProcess<(int id, string reason)> RpcSpawn = new("SpawnMapObject", (message, calledByMe) =>
    {
        if(!calledByMe) NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>()?.Spawn(message.id, message.reason);
    });
}

public enum WindType
{
    NoWind,
    AirshipOutside,
    AirshipVentilation,
    AirshipGapRoom,
    FungleBeach,
    FungleHighlands,
}
public abstract class MapData
{
    public record AdditionalRoomArea(float CenterX, float CenterY, float SizeX, float SizeY)
    {
        public bool Overlaps(UnityEngine.Vector2 position) => Mathf.Abs(position.x - CenterX) < SizeX && Mathf.Abs(position.y - CenterY) < SizeY;
    }

    abstract protected Vector2[] MapArea { get; }
    abstract protected Vector2[] NonMapArea { get; }
    abstract protected (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms { get; }
    abstract protected (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms { get; }
    virtual public Vector2[][] RaiderIgnoreArea { get => []; }
    abstract protected SystemTypes[] SabotageTypes { get; }
    abstract public MapObjectPoint[] MapObjectPoints { get; }
    /// <summary>
    /// 封鎖されたベントの画像を取得します。
    /// </summary>
    /// <param name="vent"></param>
    /// <param name="level">0から3で指定</param>
    /// <returns></returns>
    virtual public Sprite GetSealedVentSprite(Vent vent, int level, bool remove) => SealedVentSprite.GetSprite(level + (remove ? (SealedVentSprite.Length / 2) : 0));
    abstract protected IDividedSpriteLoader SealedVentSprite { get; }
    virtual public Sprite GetSealedDoorSprite(OpenableDoor door, int level, bool isVert, bool remove)
    {
        IDividedSpriteLoader spriteLoader = GetSealedDoorSprite(isVert);
        if (remove && spriteLoader.Length > 8) level += 8;
        return spriteLoader.GetSprite(level);
    }
    virtual protected IDividedSpriteLoader GetSealedDoorSprite(bool isVert) => isVert ? SkeldData.SealedDoorSpriteSkeldV : SkeldData.SealedDoorSpriteSkeldH;
    virtual public Vector3 GetDoorSealingPos(OpenableDoor door, bool isVert) => isVert ? new(-0.024f,0.52f,-0.01f) : new(0f, -0.1f, -0.01f);
    virtual public bool IsSealableDoor(OpenableDoor door) => true;
    public SystemTypes[] GetSabotageSystemTypes() => SabotageTypes;
    public bool CheckMapArea(Vector2 position, float radius = 0.1f)
    {
        if (radius > 0f)
        {
            int num = Physics2D.OverlapCircleNonAlloc(position, radius, PhysicsHelpers.colliderHits, Constants.ShipAndAllObjectsMask);
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

    private static Texture2D CreateReadableTexture(Texture texture, int margin = 0)
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
        Texture2D readableTexture2D = new Texture2D(texture.width + margin * 2, texture.height + margin * 2);
        readableTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), margin, margin);
        readableTexture2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        return readableTexture2D;
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

        texture.Apply(false, false);

        return CreateReadableTexture(texture);
    }

    static private readonly MapData[] AllMapData = [new SkeldData(), new MiraData(), new PolusData(), null!, new AirshipData(), new FungleData()];
    static public MapData GetCurrentMapData() => AllMapData[AmongUsUtil.CurrentMapId];

    public string? GetOverrideMapRooms(SystemTypes room, UnityEngine.Vector2 pos)
    {
        foreach(var overrideRoom in OverrideRooms)
        {
            if (overrideRoom.room != room) continue;
            if (overrideRoom.area.Overlaps(pos)) return overrideRoom.key;
        }
        return null;
    }

    public string? GetAdditionalMapRooms(UnityEngine.Vector2 pos, bool detail)
    {
        foreach (var room in AdditionalRooms)
        {
            if (!detail && room.detailRoom) continue;
            if (room.area.Overlaps(pos)) return room.key;
        }
        return null;
    }

    public virtual WindType GetWindType(Vector2 position) => WindType.NoWind;

    static public Vector2 CalcWind(Vector2 position, WindType wind, float time)
    {
        switch (wind)
        {
            case WindType.AirshipOutside:
                return new Vector2(6f + (float)Math.Cos(time * 5f) * 0.7f, (float)Math.Cos(time * 17f) * 1.5f);
            case WindType.AirshipVentilation:
                if(position.x < 27.5f)
                    return new Vector2(4f + (float)Math.Cos(time * 11f) * 0.7f, 3f + (float)Math.Cos(time * 24.3f) * 2.1f);
                else
                    return new Vector2(-4f + (float)Math.Cos(time * 11f) * 0.7f, 3f + (float)Math.Cos(time * 24.3f) * 2.1f);
            case WindType.AirshipGapRoom:
                position.x -= 7.8f; position.y -= 4.3f;
                var mag = Math.Max(0f, 4.2f - Mathf.Abs(position.x));
                return position.normalized * mag * (1.4f + (float)Math.Cos(time * 21f) * 1.2f);

            case WindType.FungleBeach:
                return new Vector2(2.6f + (float)Math.Cos(time * 6.1f) * 1.1f, 1f + (float)Math.Cos(time * 15.3f) * 0.8f);
            case WindType.FungleHighlands:
                bool high = position.y > 8f;
                return new Vector2((high ? 11f : 5f) + (float)Math.Cos(time * 5f) * 0.7f, (float)Math.Cos(time * 17f) * (high ? 3.5f : 1.5f));
            default:
                return Vector2.zero;
        }
    }
}
