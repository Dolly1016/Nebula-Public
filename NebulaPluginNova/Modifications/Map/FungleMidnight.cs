using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using Nebula.Modules.Logging;
using UnityEngine.Rendering;
using Virial;
using Virial.Events.Game.Minimap;

namespace Nebula.Modifications.Map;

static internal class FungleMidnight
{
    static private string MeshArchivePath => Path.Combine(PathHelpers.BepInExRootPath, "download", "FungleMesh.zip");

    static public void ModifyEarliest(ShipStatus ship)
    {
        using (var archive = OpenMeshArchive()) if (archive == null) return;

        RemoveBeachRooms(ship);
        DisableTaskConsoles(ship, "Communications", "Lookout", "UpperEngine", "MiningPit", "Greenhouse");
        DisableJungleConsoles(ship);
        RemoveMidnightTasks(ship);
    }

    static private void DisableJungleConsoles(ShipStatus ship)
    {
        foreach (var name in new[] { "CollectSamples (1g)", "CollectSamples (1d)", "HelpCritter (1a)" })
            if (ship.transform.TryFindChild(out var console, "Outside", "OutsideJungle", "TaskConsoles", name)) console.gameObject.SetActive(false);
    }

    static private void DisableTaskConsoles(ShipStatus ship, params string[] rooms)
    {
        foreach (var room in rooms)
            if (ship.transform.TryFindChild(out var consoles, "Rooms", room, "TaskConsoles")) consoles.gameObject.SetActive(false);
    }

    static public void ModifyEarlier(ShipStatus ship)
    {
        using var archive = OpenMeshArchive();
        if (archive == null) return;

        ModifyReactor(ship, archive);
        ModifyBeach(ship, archive);
        ModifyRope(ship, archive);
        MoveConsoles(ship);
    }

    static public void Modify(ShipStatus ship)
    {
        using var archive = OpenMeshArchive();
        if (archive == null) return;

        ReplaceJungleShadow(ship, archive);
        SubscribeMinimap(ship);
        DisableZipline(ship);
    }

    static internal ZipArchive? OpenMeshArchive()
    {
        try
        {
            return ZipFile.OpenRead(MeshArchivePath);
        }
        catch (Exception e)
        {
            NebulaLogger.Instance.Error($"Failed to open the mesh archive. (Path: {MeshArchivePath})\n{e}");
            return null;
        }
    }

    static private Stream? OpenData(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry == null) return null;

        var memory = new MemoryStream((int)entry.Length);
        using (var stream = entry.Open()) stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }


    #region Reactor
    static private readonly Vector2 reactorTarget = new(6.2364f, -11.5545f);

    static public void ModifyReactor(ShipStatus ship, ZipArchive archive)
    {
        ModifyReactorMesh(ship, archive);
        ModifyReactorScene(ship);
    }

    static public void ModifyReactorMesh(ShipStatus ship, ZipArchive archive)
    {
        ReplaceGround(ship, archive, "reactor");
    }

    static public void ModifyReactorScene(ShipStatus ship)
    {
        RemoveGreenhouse(ship);
        HideJungleObstacles(ship);
        MoveReactor(ship);
        MoveBottomIsland(ship);
        AddJungleWalls(ship);
        ReorderFrontWalls(ship);
    }

    /// <summary>ジャングル南の中洲のキノコ壁（mushroomjungle_bottom_island）を (13.436, -15.066) から動かす量。MonitorTree も同じだけ動かす</summary>
    static private readonly Vector3 bottomIslandShift = new(0.8014f - 13.436f, -7.8551f - -15.066f, 0f);

    static internal void MoveBottomIsland(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var island, "Outside", "OutsideJungle", "WallsAndShadows", "mushroomjungle_bottom_island")) island.position += bottomIslandShift;
        if (ship.transform.TryFindChild(out var monitor, "Outside", "OutsideJungle", "TaskConsoles", "MonitorTree")) monitor.position += bottomIslandShift;
    }

    static internal void ReplaceJungleShadow(ShipStatus ship, ZipArchive archive)
    {
        if (!ship.transform.TryFindChild(out var shadow, "FungleJungleShadow", "Shadow Mesh")) return;
        MeshData.Read(archive, "jungle/JungleShadow.bin")?.ApplyTo(shadow.GetComponent<MeshFilter>().mesh);
    }

    static internal void RemoveGreenhouse(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var room, "Rooms", "Greenhouse")) room.gameObject.SetActive(false);

        if (!ship.transform.TryFindChild(out var walls, "Outside", "OutsideJungle", "WallsAndShadows")) return;
        var doomed = new List<GameObject>();
        for (int i = 0; i < walls.childCount; i++)
        {
            var child = walls.GetChild(i);
            if (child.name.StartsWith("mushroomjungle_greenhouse_", StringComparison.Ordinal)) doomed.Add(child.gameObject);
        }
        foreach (var obj in doomed) GameObject.Destroy(obj);
    }

    static internal void HideJungleObstacles(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var obstacles, "Outside", "OutsideJungle", "JungleObstacles")) obstacles.gameObject.SetActive(false);
    }

    static internal void MoveReactor(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var reactor, "Rooms", "Reactor")) return;
        reactor.position = reactorTarget.AsVector3(reactor.position.z);
    }

    static internal void AddJungleWalls(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var walls, "Outside", "OutsideJungle", "WallsAndShadows")) return;

        foreach (var (pos, suffix) in new[] { (new Vector2(12.9627f, -14.3165f), "_added1"), (new Vector2(9.2245f, -16.5819f), "_added2") })
        {
            var wall = CopyWall(walls, "mushroomjungle_bottom_toplayer", pos.AsVector3(0f), suffix);
            if (wall != null) SetZ(wall, LadderZ(BottomY(wall.GetComponent<SpriteRenderer>())));
        }

        CopyWall(walls, "mushroomjungle_lab_bottom1", new(6.2682f, -8.5473f, 1.103f), "_mirrored", mirror: true);

        CopyWall(walls, "mushroomjungle_uppermid_bottom", new(9.565f, -9.6744f, 1.11f), "_copy");

        CopyWall(walls, "NEWmushchunk_bottomRight", new(13.4591f, -10.8173f, -1.7778f), "_copy");
        ShipModification.AddModWall([new(10.5405f, -11.08f), new(11.8494f, -11.10f), new(12.7173f, -10.38f), new(12.9185f, -10.18f), new(13.4042f, -9.86f), new(14.3065f, -9.87f), new(14.7122f, -10.32f)]);
    }

    const float ladderBand = -15f;
    static private float LadderZ(float bottomY) => Mathf.Clamp(-0.05f + 0.15f * (bottomY + 15f), -1.5f, -0.05f);
    static private float BottomY(SpriteRenderer renderer) => renderer.bounds.min.y;
    static private void SetZ(Transform transform, float z) => transform.position = ((Vector2)transform.position).AsVector3(z);

    static internal void ReorderFrontWalls(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var walls, "Outside", "OutsideJungle", "WallsAndShadows")) return;
        foreach (var renderer in walls.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null) continue;
            if (renderer.transform.position.z > 0.5f) continue;     // 奥側の板はさわらない
            float bottom = BottomY(renderer);
            if (bottom < ladderBand) SetZ(renderer.transform, LadderZ(bottom));
        }
    }

    static private Transform? CopyWall(Transform walls, string template, Vector3 position, string suffix, bool mirror = false)
    {
        if (!walls.TryFindChild(out var original, template)) return null;
        var copy = GameObject.Instantiate(original, walls);
        copy.name = template + suffix;
        if (mirror) copy.localScale = new(-1f, 1f, 1f);
        copy.position = position;
        return copy;
    }
    #endregion

    #region Beach
    static public void ModifyBeach(ShipStatus ship, ZipArchive archive)
    {
        ReplaceGround(ship, archive, "beach");
        ReplaceWaves(ship);
        AddShoreWalls();
    }

    static internal void ReplaceWaves(ShipStatus ship)
    {
        (ChildInfo[] route, string file)[] layers = [
            (["FungleWavesAnimated(Clone)", "Waves"], "_Waves"),
            (["FungleWavesAnimated(Clone)", "OverlayCoralsParent", "OverlayCorals"], "_OverlayCoralsParent_OverlayCorals"),
        ];

        bool TryReplaceWaves()
        {
            if (!ship.transform.FindChild("FungleWavesAnimated(Clone)")) return false;
            using var archive = OpenMeshArchive();
            foreach (var (route, file) in layers)
            {
                if (!ship.transform.TryFindChild(out var layer, route)) continue;
                var frames = layer.GetComponent<MeshAnimator>().sequence.Frames;
                for (int i = 0; i < frames.Length; i++)
                    MeshData.Read(archive, $"wave/{file}_{i:D3}.bin")?.ApplyTo(frames[i]);
            }

            MoveDock(ship);
            return true;
        }

        System.Collections.IEnumerator CoTryReplaceWaves()
        {
            while (!TryReplaceWaves()) yield return null;
        }

        NebulaManager.Instance.StartCoroutine(CoTryReplaceWaves().WrapToIl2Cpp());
    }

    static internal void RemoveBeachRooms(ShipStatus ship)
    {
        foreach (var name in new[] { "Cafeteria", "RecRoom", "Kitchen" })
            if (ship.transform.TryFindChild(out var room, "Rooms", name)) room.gameObject.SetActive(false);

        // 消した部屋のほうへ伸びていた影の端を落とす。0 番目の点 (-12.33, -9.94) が該当する（全 60 点）
        if (ship.transform.TryFindChild(out var shadow, "Outside", "OutsideJungle", "WallsAndShadows", "mushroomjungle_SW_left1", "Shadow (5)"))
        {
            var edge = shadow.GetComponent<EdgeCollider2D>();
            Vector2[] points = edge.points;
            edge.SetPoints(points[1..].ToIl2CppList());
        }
    }

    // FungleWavesAnimated(Clone)の生成が遅いのでCoTryReplaceWavesで呼び出す必要がある。
    static internal void MoveDock(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var dock, "Rooms", "Dock")) return;
        var delta = dockTarget - dock.position;
        dock.position += delta;

        if (ship.transform.TryFindChild(out var water, "FungleWavesAnimated(Clone)", "WaterDockParent")) water.position += delta;
    }

    static internal void AddShoreWalls()
    {
        ShipModification.AddModWall([new(-14.0063f, -11.36f), new(-14.9194f, -10.21f)]);
        ShipModification.AddModWall([
            new(-16.121f, -8.91f), new(-16.5535f, -6.56f), new(-16.2171f, -4.83f), new(-16.938f, -2.81f),
            new(-16.3613f, -0.98f), new(-16.0729f, 0.83f), new(-14.4869f, 2.85f), new(-12.8528f, 7.32f),
            new(-11.7955f, 8.24f), new(-10.69f, 8.52f)]);
    }
    #endregion

    #region Rope
    static public void ModifyRope(ShipStatus ship, ZipArchive archive)
    {
        foreach (var mesh in GroundMeshes(ship))
        {
            var add = MeshData.Read(archive, $"rope/{mesh.gameObject.name}.bin");
            if (add != null) MeshData.FromMesh(mesh.mesh).Append(add).ApplyTo(mesh.mesh);
        }
    }
    #endregion

    #region Tasks
    static internal void RemoveMidnightTasks(ShipStatus ship)
    {
        ship.ShortTasks = ship.ShortTasks.Append(ship.LongTasks[7]).Append(ship.LongTasks[4]).ToArray();
        ShipModification.RemoveTasks(ship, [1, 3, 4, 5, 6, 8, 12, 14, 15], [1, 2, 4, 5, 6, 7, 8], [1]);
    }

    static internal void OnTaskInitialized(NormalPlayerTask task)
    {
        if (task.TaskType is TaskTypes.CatchFish or TaskTypes.ReplaceWaterJug or TaskTypes.HoistSupplies)
        {
            task.MaxStep = 1;
            task.ShowTaskStep = false;
        }
        if (task.TaskType is TaskTypes.ReplaceWaterJug or TaskTypes.HoistSupplies) task.Length = NormalPlayerTask.TaskLength.Short;
    }
    #endregion

    #region Consoles
    // ロープの移動量
    static private readonly Vector3 ropeOffset = new(9.7453f - 14.37f, -1.2141f - -1.0632f, 0f);

    static internal void MoveConsoles(ShipStatus ship)
    {
        void MoveTo(Vector3 position, params ChildInfo[] route)
        {
            if (ship.transform.TryFindChild(out var obj, route)) obj.position = position;
        }

        // ジャングルのタスク
        MoveTo(new(3.1225f, -14.0932f, -0.9f), "Outside", "OutsideJungle", "TaskConsoles", "CollectSamples (1a)");
        MoveTo(new(3.231f, -9.17f, -0.9f), "Outside", "OutsideJungle", "TaskConsoles", "CollectSamples (1f)");
        MoveTo(new(11.1673f, -10.2345f, 0.3f), "Outside", "OutsideJungle", "TaskConsoles", "HelpCritter (1b)");
        // 複製したロープとの相対位置が変わらないように、ロープと同じだけずらす
        if (ship.transform.TryFindChild(out var hoist, "Outside", "OutsideJungle", "TaskConsoles", "HoistSupplies (1)")) hoist.position += ropeOffset;

        // ジャングルのベント
        MoveTo(new(12.6555f, -7.8709f, 1f), "Outside", "OutsideJungle", "Vents", "SouthEastJungleVent");
        if (ship.transform.TryFindChild(out var southWestVent, "Outside", "OutsideJungle", "Vents", "SouthWestJungleVent"))
        {
            southWestVent.position = new(4.56f, -12.515f, 1f);
            southWestVent.GetComponent<Vent>().Left = null;
        }

        // 海岸のベント
        MoveTo(new(-15.4f, -2.571f, 1f), "Outside", "OutsideBeach", "Vents", "RecRoomVent");
        MoveTo(new(-12.033f, 7.061f, 1f), "Outside", "OutsideBeach", "Vents", "MeetingRoomVent");

        // 海岸のタスクと小物
        MoveTo(new(-15.3502f, -3.849f, 1f), "Outside", "OutsideBeach", "TaskConsoles", "RoastMarshmallow Stick (1)");
        MoveTo(new(-13.8372f, -10.6934f, 1f), "Outside", "OutsideBeach", "TaskConsoles", "RoastMarshmallow Stick (2)");
        MoveTo(new(-15.0491f, -5.7491f, -0.06f), "Outside", "OutsideBeach", "BeachObstacles", "Debris (2)");

        // 高台のはしご
        if (ship.transform.TryFindChild(out var ladders, "Outside", "OutsideHighlands", "Ladders")) ladders.gameObject.SetActive(false);
    }
    #endregion

    #region TaskArrow
    static internal bool UpdateTaskArrowAndLocation(NormalPlayerTask task)
    {
        if (task.TaskType == TaskTypes.ReplaceParts) return UpdateReplacePartsArrow(task);
        return true;
    }

    static private bool UpdateReplacePartsArrow(NormalPlayerTask task)
    {
        if (!task.Arrow.AsBoolFast()) return false;
        if (!task.Owner.AmOwner || task.IsComplete)
        {
            task.Arrow.gameObject.SetActive(false);
            return false;
        }

        if (PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer)) task.arrowSuspended = true;
        else task.Arrow.gameObject.SetActive(true);

        var candidates = ShipStatus.Instance.AllConsoles.Where(c => c.ValidTasks.Length > 0 && c.ValidTasks[0].taskType == TaskTypes.ReplaceParts && c.ValidTasks[0].taskStep.Contains(task.taskStep)).ToArray();
        if (candidates.Length == 0)
        {
            task.Arrow.gameObject.SetActive(false);
            return false;
        }

        var next = candidates[System.Random.Shared.Next(candidates.Length)];
        task.Arrow.target = next.transform.position;
        task.StartAt = next.Room;
        task.Data[0] = (byte)next.ConsoleId;
        task.LocationDirty = true;
        return false;
    }
    #endregion

    #region Minimap
    /// <summary>移動前の Reactor の位置（ワールド）。移動先は reactorTarget</summary>
    static private readonly Vector2 reactorOrigin = new(22f, -7.5f);
    /// <summary>Dock の移動先と、移動前の位置（ワールド）</summary>
    static private readonly Vector3 dockTarget = new(-16.6317f, -9.2238f, 1.1f);
    static private readonly Vector2 dockOrigin = new(-21.179f, -7.202f);

    static internal void SubscribeMinimap(ShipStatus ship)
    {
        GameOperatorManager.Instance?.Subscribe<MapInstantiateEvent>(ev => ModifyMinimap(ship, MapBehaviour.Instance), NebulaAPI.CurrentGame!);
        GameOperatorManager.Instance?.Subscribe<MapOpenInternalEvent>(ev =>
        {
            // 消した部屋の分だけ寄せて、マップを画面の中ほどに表示する
            ev.map.transform.localPosition = new(0.8f, -0.1f, -50f);
            ev.map.transform.localScale = new(1f, 1f, 1f);
            ev.map.GetComponent<AspectSize>().PercentWidth = 1f;
            CenterSabotageText(ev.map);
        }, NebulaAPI.CurrentGame!);
    }

    static private void CenterSabotageText(MapBehaviour map)
    {
        var flip = MapBehaviourExtension.GetMinimapFlippedScale();
        var mapPos = map.transform.localPosition;
        var mapScale = map.transform.localScale;
        var overlay = map.countOverlay.transform;
        float x = -mapPos.x / (Mathf.Abs(mapScale.x) * Mathf.Sign(flip.x) * overlay.localScale.x);
        float y = -mapPos.y / (Mathf.Abs(mapScale.y) * Mathf.Sign(flip.y) * overlay.localScale.y);

        var original = map.countOverlay.SabotageText.transform;
        for (int i = 0; i < overlay.childCount; i++)
        {
            var text = overlay.GetChild(i);
            if (text == original || text.name.StartsWith(original.name + "(Clone)")) text.localPosition = new(x, y, text.localPosition.z);
        }
    }

    static internal void ModifyMinimap(ShipStatus ship, MapBehaviour map)
    {
        var countOverlay = map.transform.GetChild(3);
        var roomNames = map.transform.GetChild(5);
        var infectedOverlay = map.transform.GetChild(6);

        // Comms, Lookout, UpperEngine, RecRoom, Kitchen, MiningPit, Cafeteria, Greenhouse
        foreach (var i in new[] { 0, 5, 6, 8, 9, 11, 13, 12 }) countOverlay.GetChild(i).gameObject.SetActive(false);
        map.countOverlay.CountAreas = map.countOverlay.CountAreas.Where(area => area.gameObject.activeSelf).ToArray(); //実は要らない気がする
        foreach (var i in new[] { 0, 4, 5, 7, 8, 9, 10, 12 }) roomNames.GetChild(i).gameObject.SetActive(false);
        // InfectedOverlay にあるのは Comms, Lookout, Kitchen, MiningPit だけ
        foreach (var i in new[] { 0, 4, 5, 7 }) infectedOverlay.GetChild(i).gameObject.SetActive(false);

        // Jungle
        roomNames.GetChild(13).localPosition = new(-0.4363f, -2.1795f, 0f);

        // Reactor（CountOverlay 7 / RoomNames 6 / InfectedOverlay 3）と Dock（CountOverlay 10 / RoomNames 15）
        ShiftOnMinimap(ship, map, reactorTarget - reactorOrigin, countOverlay.GetChild(7), roomNames.GetChild(6), infectedOverlay.GetChild(3));
        ShiftOnMinimap(ship, map, (Vector2)dockTarget - dockOrigin, countOverlay.GetChild(10), roomNames.GetChild(15));
    }

    static private void ShiftOnMinimap(ShipStatus ship, MapBehaviour map, Vector2 worldShift, params Transform[] items)
    {
        var shift = map.transform.TransformVector(worldShift / ship.MapScale);
        foreach (var item in items) item.position += shift;
    }
    #endregion

    #region Mesh
    static internal void ReplaceGround(ShipStatus ship, ZipArchive archive, string subdir)
    {
        foreach (var mesh in GroundMeshes(ship))
            MeshData.Read(archive, $"{subdir}/{mesh.gameObject.name}.bin")?.ApplyTo(mesh.mesh);
    }

    static private IEnumerable<MeshFilter> GroundMeshes(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var baseObj, "Backgrounds", "Base")) yield break;
        for (int i = 0; i < baseObj.childCount; i++)
        {
            var filter = baseObj.GetChild(i).GetComponent<MeshFilter>();
            if (filter != null) yield return filter;
        }
    }

    private class MeshData
    {
        public Vector3[] V = null!;
        public Color32[]? C;    // nullなら触らない
        public Vector2[]? UV;   // nullなら触らない
        public int[] T = null!;

        static public MeshData? Read(ZipArchive archive, string path)
        {
            using var stream = OpenData(archive, path);
            if (stream == null) return null;
            using var reader = new BinaryReader(stream);
            var magic = reader.ReadBytes(4);
            if (magic[0] != 'F' || magic[1] != 'G' || magic[2] != 'M') return null;
            int nv = reader.ReadInt32(), ni = reader.ReadInt32();
            int flags = magic[3] == '1' ? 1 : reader.ReadInt32();

            var data = new MeshData { V = new Vector3[nv], T = new int[ni] };
            for (int i = 0; i < nv; i++) data.V[i] = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if ((flags & 1) != 0)
            {
                data.C = new Color32[nv];
                for (int i = 0; i < nv; i++) data.C[i] = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }
            if ((flags & 2) != 0)
            {
                data.UV = new Vector2[nv];
                for (int i = 0; i < nv; i++) data.UV[i] = new(reader.ReadSingle(), reader.ReadSingle());
            }
            for (int i = 0; i < ni; i++) data.T[i] = reader.ReadInt32();
            return data;
        }

        static public MeshData FromMesh(Mesh mesh) => new() { V = mesh.vertices, C = mesh.colors32, T = mesh.triangles };

        public MeshData Append(MeshData add)
        {
            int nv = V.Length;
            var data = new MeshData
            {
                V = [.. V, .. add.V],
                C = [.. C!, .. add.C!],
                T = new int[T.Length + add.T.Length],
            };
            Array.Copy(T, data.T, T.Length);
            for (int i = 0; i < add.T.Length; i++) data.T[T.Length + i] = add.T[i] + nv;
            return data;
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear(false);
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = V;
            if (C != null) mesh.colors32 = C;
            if (UV != null) mesh.uv = UV;
            mesh.triangles = T;
            mesh.RecalculateBounds();
        }
    }
    #endregion

    static internal void DisableZipline(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var zipline, "Zipline")) zipline.gameObject.SetActive(false);
    }
}
