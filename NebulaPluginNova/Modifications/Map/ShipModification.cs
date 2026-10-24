using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modifications.Map;

static internal class ShipModification
{

    public static string ReverseScalerObjName = "ReverseScaler";
    internal static Transform ReverseScaler { get; private set; } = null!;
    public static void PatchModification(byte mapId, ShipStatus ship)
    {
        switch (mapId)
        {
            case 0:
                SkeldModification.Modify(ship);
                break;
            case 1:
                MIRAModification.Modify(ship);
                break;
            case 2:
                PolusModification.Modify(ship);
                break;
            case 4:
                AirshipModification.Modify(ship);
                break;
            case 5:
                FungleModification.Modify(ship);
                break;
        }
    }

    public static void PatchEarliestModification(byte mapId, ShipStatus ship)
    {
        switch (mapId)
        {
            case 0:
                //SkeldModification.ModifyEarliest(ship);
                break;
            case 1:
                //MIRAModification.ModifyEarliest(ship);
                break;
            case 2:
                //PolusModification.ModifyEarliest(ship);
                break;
            case 4:
                //AirshipModification.ModifyEarliest(ship);
                break;
            case 5:
                FungleModification.ModifyEarliest(ship);
                break;
        }
    }

    public static void PatchEarlierModification(byte mapId, ShipStatus ship)
    {
        var shipTransform = ship.transform;
        var obj = UnityHelper.CreateObject(ReverseScalerObjName, shipTransform, Vector3.zero);
        obj.transform.localScale = new(1f / shipTransform.localScale.x, 1f / shipTransform.localScale.y, 1f);
        ReverseScaler = obj.transform;

        switch (mapId)
        {
            case 0:
                //ModifyEarlierSkeld();
                break;
            case 1:
                //ModifyEarierMira();
                break;
            case 2:
                //ModifyEarlierPolus();
                break;
            case 4:
                AirshipModification.ModifyEarlier(ship);
                break;
            case 5:
                FungleModification.ModifyEarlier(ship);
                break;
        }

        foreach (var vectors in MapData.GetCurrentMapData().RaiderIgnoreArea)
        {
            var collider = UnityHelper.CreateObject<PolygonCollider2D>("RaiderIgnoreArea", ReverseScaler, Vector3.zero, LayerExpansion.GetRaiderColliderLayer());
            collider.SetPath(0, vectors);
            collider.isTrigger = true;
        }
    }

    internal static void CreateAxeWall(string name, Vector2[] points)
    {
        var collider = UnityHelper.CreateObject<EdgeCollider2D>(name, ReverseScaler, Vector3.zero, LayerExpansion.GetHookshotWallLayer());
        collider.SetPoints(points.ToIl2CppList());
    }

    internal static Vent CreateVent(SystemTypes room, string ventName, Vector2 position)
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        var referenceVent = ship.AllVents[0];
        Vent vent = UnityEngine.Object.Instantiate<Vent>(referenceVent, ship.FastRooms[room].transform);
        vent.transform.localPosition = new Vector3(position.x, position.y, -1);
        vent.Left = null;
        vent.Right = null;
        vent.Center = null;
        vent.Id = ship.AllVents.Select(x => x.Id).Max() + 1; // Make sure we have a unique id

        var allVentsList = ship.AllVents.ToList();
        allVentsList.Add(vent);
        ship.AllVents = allVentsList.ToArray();

        vent.gameObject.SetActive(true);
        vent.name = ventName;
        vent.gameObject.name = ventName;

        var console = vent.GetComponent<VentCleaningConsole>();
        if (console.AsBoolFast())
        {
            console.Room = room;
            console.ConsoleId = ship.AllVents.Length;

            var allConsolesList = ship.AllConsoles.ToList();
            allConsolesList.Add(console);
            ship.AllConsoles = allConsolesList.ToArray();
        }

        return vent;
    }

    internal static void EditConsole(SystemTypes room, string objectName, Action<Console> action)
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        if (!ship.FastRooms.ContainsKey(room)) return;
        PlainShipRoom shipRoom = ship.FastRooms[room];
        Transform transform = shipRoom.transform.FindChild(objectName);
        if (!transform.AsBoolFast()) return;
        GameObject obj = transform.gameObject;
        if (!obj.AsBoolFast()) return;

        Console c = obj.GetComponent<Console>();
        if (c.AsBoolFast()) action.Invoke(c);
    }

    internal static Console ActivateWiring(string consoleName, int consoleId)
    {
        Console console = ActivateConsole(consoleName);

        if (!console.TaskTypes.Contains(TaskTypes.FixWiring))
        {
            var list = console.TaskTypes.ToList();
            list.Add(TaskTypes.FixWiring);
            console.TaskTypes = list.ToArray();
        }
        console.ConsoleId = consoleId;
        return console;
    }

    internal static Console CreateConsole(SystemTypes room, string objectName, Sprite sprite, Vector2 pos, float z = 0f)
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(ship.FastRooms.TryGetValue(room, out var roomObj) ? roomObj.transform : ship.transform);
        obj.transform.localPosition = pos.AsVector3(z);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        Console c = Consolize<Console>(obj);
        c.Room = room;
        c.Image = renderer;
        return c;
    }

    internal static Console ActivateConsole(string objectName)
    {
        GameObject obj = UnityEngine.GameObject.Find(objectName);
        return Consolize<Console>(obj);
    }

    private static Material? highlightMaterial = null;
    internal static Material GetHighlightMaterial()
    {
        if (highlightMaterial != null) return new Material(highlightMaterial);
        foreach (var mat in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            if (mat.name == "HighlightMat")
            {
                highlightMaterial = mat.TryCast<Material>();
                break;
            }
        }
        return new Material(highlightMaterial);
    }

    internal static Console Consolize<C>(GameObject obj, SpriteRenderer? renderer = null) where C : Console
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;

        obj.layer = LayerMask.NameToLayer("ShortObjects");
        Console console = obj.GetComponent<Console>();
        PassiveButton button = obj.GetComponent<PassiveButton>();
        Collider2D collider = obj.GetComponent<Collider2D>();
        if (!console.AsBoolFast())
        {
            console = obj.AddComponent<C>();
            console.checkWalls = true;
            console.usableDistance = 0.7f;
            console.TaskTypes = new TaskTypes[0];
            console.ValidTasks = new Il2CppReferenceArray<TaskSet>(0);
            var list = ship.AllConsoles.ToList();
            list.Add(console);
            ship.AllConsoles = new Il2CppReferenceArray<Console>(list.ToArray());
        }
        if (console.Image == null)
        {
            if (renderer != null)
            {
                console.Image = renderer;
            }
            else
            {
                console.Image = obj.GetComponent<SpriteRenderer>();
                console.Image.material = GetHighlightMaterial();
            }
        }
        if (!button.AsBoolFast())
        {
            button = obj.AddComponent<PassiveButton>();
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button._CachedZ_k__BackingField = 0.1f;
            button.CachedZ = 0.1f;
        }

        if (!collider.AsBoolFast())
        {
            var cCollider = obj.AddComponent<CircleCollider2D>();
            cCollider.radius = 0.4f;
            cCollider.isTrigger = true;
        }

        return console;
    }

    internal static void AddModShadow(IEnumerable<Vector2> points)
    {
        var edge = UnityHelper.CreateObject<EdgeCollider2D>("ModShadow", ReverseScaler, Vector3.zero, LayerExpansion.GetShadowLayer());
        edge.SetPoints(points.ToList().ToIl2CppList());
    }

    internal static void AddModWall(IEnumerable<Vector2> points)
    {
        var edge = UnityHelper.CreateObject<EdgeCollider2D>("ModWall", ReverseScaler, Vector3.zero, LayerExpansion.GetShipLayer());
        edge.SetPoints(points.ToList().ToIl2CppList());
    }

    static internal void RemoveTasks(ShipStatus ship, int[] shortTasks, int[] longTasks, int[] commonTasks)
    {
        ship.ShortTasks = ship.ShortTasks.Where((_, i) => !shortTasks.Contains(i)).ToArray();
        ship.LongTasks = ship.LongTasks.Where((_, i) => !longTasks.Contains(i)).ToArray();
        ship.CommonTasks = ship.CommonTasks.Where((_, i) => !commonTasks.Contains(i)).ToArray();
    }
}
