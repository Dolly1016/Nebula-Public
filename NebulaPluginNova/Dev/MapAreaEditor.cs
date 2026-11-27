﻿using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Map;
using Nebula.Modules.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Virial;
using Virial.Events.Game;
using Virial.Runtime;

namespace Nebula.Dev;

/// <summary>
/// MapArea / NonMapAreaの代表点を編集し、マップエリアと判定される領域を可視化するデバッグツールです。
/// 各代表点から障害物を跨がずに到達できる領域を、LightSourceRaycastRendererと同様の方法(コライダーの頂点へ向けたレイ)で多角形として求めます。
/// </summary>
/// <remarks>
/// 操作方法
/// <list type="bullet">
/// <item>Ctrl + 左クリック: MapAreaの点を追加</item>
/// <item>Ctrl + Shift + 左クリック: NonMapAreaの点を追加</item>
/// <item>左クリック(点の上): 点を選択 / ドラッグで移動</item>
/// <item>右クリック(点の上) / Delete: 点を削除</item>
/// <item>M: 選択中の点のミュート切替 (領域を一時的に無効化。出力には残る)</item>
/// <item>G: ドアを無視するか切り替え</item>
/// <item>J: すべて再計算</item>
/// <item>K: 編集結果をクリップボードにコピー</item>
/// </list>
/// </remarks>
[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class MapAreaEditor : DevTool
{
    static public void Preprocess(NebulaPreprocessor preprocess) => RegisterButton("MapArea Editor", () => new MapAreaEditor());

    private const double ComputeBudgetMs = 8.0;
    private const float PointDiameter = 0.3f;
    /// <summary>円弧部分を近似するレイの間隔(度)</summary>
    private const float ArcStepDegree = 2f;
    /// <summary>頂点の両脇にレイを飛ばす際のずらし幅(ワールド座標)。LightSource.RaycastToleranceに相当します。</summary>
    private const float VertexTolerance = 0.001f;

    private const float MapFillZ = -9.0f;
    private const float SelectedFillZ = -9.05f;
    private const float NonMapFillZ = -9.1f;
    private const float OutlineZ = -9.2f;
    private const float PointZ = -9.5f;

    //塗りの色。半透明の塗りが重なるほど濃くなるので、1つの点しかカバーしない領域は最も薄く見える。
    static private readonly Color MapFillColor = new(0.2f, 1f, 0.35f, 0.2f);
    static private readonly Color SelectedMapFillColor = new(0.25f, 0.55f, 1f, 0.4f);
    static private readonly Color NonMapFillColor = new(1f, 0.15f, 0.15f, 0.4f);
    static private readonly Color SelectedNonMapFillColor = new(1f, 0.55f, 0.1f, 0.5f);
    static private readonly Color MapOutlineColor = new(0.2f, 1f, 0.35f, 0.55f);
    static private readonly Color NonMapOutlineColor = new(1f, 0.3f, 0.3f, 0.7f);
    static private readonly Color SelectedOutlineColor = new(1f, 1f, 0.2f, 1f);

    static private readonly Image circleImage = SpriteLoader.FromResource("Nebula.Resources.WhiteCircle.png", 100f);

    private class EditPoint
    {
        public VVector2 Position;
        public readonly bool IsNonMap;
        /// <summary>
        /// 元データ上のインデックス。追加した点は-1。
        /// Fungleでは<see cref="FungleData.RawMapPositions"/>上のインデックス、それ以外では MapArea / NonMapArea 上のインデックスです。
        /// </summary>
        public readonly int OriginIndex;
        public bool Moved = false;
        /// <summary>ミュート中の点は領域を描画せず、カーソル位置の判定にも使いません。出力には含まれます。</summary>
        public bool Muted = false;

        public SpriteRenderer Renderer = null!;
        public MeshRenderer FillRenderer = null!;
        public MeshFilter FillFilter = null!;
        public LineRenderer Outline = null!;

        /// <summary>到達可能領域の多角形(ワールド座標、角度順)。未計算ならnull。</summary>
        public Vector2[]? Polygon = null;
        public float Area = 0f;
        /// <summary>点がコライダーの内側にあり、到達可能領域がほとんど無い。</summary>
        public bool IsBuried = false;

        public bool IsAdded => OriginIndex < 0;
        public float Range => IsNonMap ? MapData.NonMapAreaRange : MapData.MapAreaRange;

        public EditPoint(VVector2 position, bool isNonMap, int originIndex)
        {
            Position = position;
            IsNonMap = isNonMap;
            OriginIndex = originIndex;
        }

        public bool Contains(Vector2 p)
        {
            if (Polygon == null || Polygon.Length < 3) return false;
            if ((p - (Vector2)Position).sqrMagnitude > Range * Range) return false;
            bool inside = false;
            for (int i = 0, j = Polygon.Length - 1; i < Polygon.Length; j = i++)
            {
                var a = Polygon[i];
                var b = Polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }

    private readonly byte mapId;
    private readonly int inMapId;
    private readonly MapData mapData;
    private readonly bool isFungle;

    private readonly GameObject root;
    private readonly Shader spriteShader;

    private readonly List<EditPoint> points = [];
    private EditPoint? selected = null;

    private readonly List<EditPoint> pending = [];
    private bool ignoreDoors = true;
    private readonly HashSet<int> doorColliders = [];

    //ドラッグ
    private EditPoint? dragging = null;
    private VVector2 dragStartMouse, dragStartPos;
    private bool dragMoved = false;

    /// <summary>既存の点からこの距離未満の位置には点を追加しません。</summary>
    private const float MinPointDistance = 0.25f;

    private string hoverText = "";

    public MapAreaEditor()
    {
        mapId = (byte)NebulaAPI.AmongUs.MapId;
        inMapId = NebulaAsset.GetInMapId(mapId);
        mapData = MapData.GetMapData(mapId);
        isFungle = mapId == 5;

        root = UnityHelper.CreateObject("MapAreaEditor", null, Vector3.zero);
        spriteShader = Shader.Find("Sprites/Default");

        CollectDoorColliders();

        //代表点の読み込み
        if (isFungle)
        {
            //FungleのMapAreaは inMapId & ~1 のビットで選ばれる (FungleData.MapAreaを参照)
            int bit = 1 << (inMapId & ~1);
            var raw = FungleData.RawMapPositions;
            for (int i = 0; i < raw.Count; i++) if ((raw[i].inMapFlag & bit) != 0) AddPointInternal(raw[i].pos, false, i);
        }
        else
        {
            var mapArea = mapData.MapArea;
            for (int i = 0; i < mapArea.Count; i++) AddPointInternal(mapArea[i], false, i);
        }
        var nonMapArea = mapData.NonMapArea;
        for (int i = 0; i < nonMapArea.Count; i++) AddPointInternal(nonMapArea[i], true, i);

        foreach (var p in points) pending.Add(p);

        DebugScreen.Push(new FunctionalDebugTextContent(GetStatusText, this));
        DebugScreen.Push("Ctrl+クリック: MapArea追加 / Ctrl+Shift+クリック: NonMapArea追加<br>ドラッグ: 移動 / 右クリック・Delete: 削除<br>M: ミュート切替 / G: ドア無視の切替 / J: 再計算 / K: 出力をコピー", 8f);
    }

    protected override void OnClosed()
    {
        foreach (var p in points) if (p.FillFilter && p.FillFilter.mesh) GameObject.Destroy(p.FillFilter.mesh);
        if (root) GameObject.Destroy(root);
    }

    private void CollectDoorColliders()
    {
        doorColliders.Clear();
        var ship = AmongUsLLImpl.ShipStatusInstance;
        if (!ship) return;
        foreach (var door in ship.AllDoors)
        {
            if (!door) continue;
            foreach (var c in door.GetComponentsInChildren<Collider2D>(true)) doorColliders.Add(c.GetInstanceID());
        }
    }

    // ---------------------------------------------------------------- 点の管理

    private EditPoint AddPointInternal(VVector2 position, bool isNonMap, int originIndex)
    {
        var point = new EditPoint(position, isNonMap, originIndex);

        point.Renderer = UnityHelper.CreateObject<SpriteRenderer>(isNonMap ? "NonMapPoint" : "MapPoint", root.transform, position.AsVector3(PointZ));
        point.Renderer.sprite = circleImage.GetSprite();

        (point.FillRenderer, point.FillFilter) = UnityHelper.CreateMeshRenderer("Fill", root.transform, new Vector3(0f, 0f, isNonMap ? NonMapFillZ : MapFillZ), null, null, new Material(spriteShader));

        point.Outline = UnityHelper.SetUpLineRenderer("Outline", root.transform, new Vector3(0f, 0f, OutlineZ), null, 0.03f);
        point.Outline.loop = true;
        point.Outline.positionCount = 0;

        points.Add(point);
        UpdatePointAppearance(point);
        return point;
    }

    private void AddPoint(VVector2 position, bool isNonMap)
    {
        if (points.Any(p => (p.Position - position).Magnitude < MinPointDistance))
        {
            DebugScreen.Push($"既存の点から{MinPointDistance}未満の位置には追加できません。", 2f);
            return;
        }
        var point = AddPointInternal(position, isNonMap, -1);
        Select(point);
        Recompute(point);
    }

    private void RemovePoint(EditPoint point)
    {
        pending.Remove(point);
        points.Remove(point);
        if (point.FillFilter && point.FillFilter.mesh) GameObject.Destroy(point.FillFilter.mesh);
        if (point.Renderer) GameObject.Destroy(point.Renderer.gameObject);
        if (point.FillRenderer) GameObject.Destroy(point.FillRenderer.gameObject);
        if (point.Outline) GameObject.Destroy(point.Outline.gameObject);
        if (selected == point) Select(null);
        if (dragging == point) dragging = null;
    }

    private void Select(EditPoint? point)
    {
        var last = selected;
        selected = point;
        if (last != null) UpdatePointAppearance(last);
        if (point != null) UpdatePointAppearance(point);
    }

    private EditPoint? FindPoint(VVector2 position)
    {
        float pickRadius = PointDiameter * 0.75f * Mathf.Max(1f, Camera.main.orthographicSize / 3f);
        EditPoint? found = null;
        float min = pickRadius;
        foreach (var p in points)
        {
            float d = (p.Position - position).Magnitude;
            if (d < min)
            {
                min = d;
                found = p;
            }
        }
        return found;
    }

    private void UpdatePointAppearance(EditPoint point)
    {
        bool isSelected = point == selected;

        if (point.Renderer)
        {
            Color color;
            if (isSelected) color = Color.yellow;
            else if (point.IsBuried) color = new Color(1f, 0f, 1f);
            else if (point.IsNonMap) color = new Color(1f, 0.25f, 0.25f);
            else color = point.IsAdded || point.Moved ? new Color(0.3f, 0.9f, 1f) : new Color(0.2f, 1f, 0.3f);
            if (point.Muted) color.a = 0.35f;
            point.Renderer.color = color;
            point.Renderer.transform.localScale = Vector3.one * (PointDiameter * (isSelected ? 1.4f : 1f) / point.Renderer.sprite.bounds.size.x);
            point.Renderer.transform.localPosition = point.Position.AsVector3(PointZ);
        }

        if (point.FillRenderer)
        {
            point.FillRenderer.material.color = point.IsNonMap ?
                (isSelected ? SelectedNonMapFillColor : NonMapFillColor) :
                (isSelected ? SelectedMapFillColor : MapFillColor);
            var pos = point.FillRenderer.transform.localPosition;
            pos.z = point.IsNonMap ? NonMapFillZ : isSelected ? SelectedFillZ : MapFillZ;
            point.FillRenderer.transform.localPosition = pos;
        }

        if (point.Outline)
        {
            var color = isSelected ? SelectedOutlineColor : point.IsNonMap ? NonMapOutlineColor : MapOutlineColor;
            point.Outline.SetColors(color, color);
            float width = isSelected ? 0.07f : 0.03f;
            point.Outline.SetWidth(width, width);
            var pos = point.Outline.transform.localPosition;
            pos.z = isSelected ? OutlineZ - 0.05f : OutlineZ;
            point.Outline.transform.localPosition = pos;
        }
    }

    // ---------------------------------------------------------------- 到達可能領域の計算

    private readonly Il2CppReferenceArray<Collider2D> overlapBuffer = new(1024);
    private readonly Il2CppStructArray<RaycastHit2D> castBuffer = new(64);
    private readonly List<Vector2> vertexBuffer = new(1024);
    private readonly List<(Vector2 a, Vector2 b)> segmentBuffer = new(1024);
    private readonly List<float> angleBuffer = new(4096);

    private bool IsBlocking(Collider2D collider, bool isNonMap)
    {
        if (collider.isTrigger) return false;
        //MapAreaの判定ではNonMapコライダーを障害物とみなさない (MapData.IsReachableFromMapAreaPointと同じ)
        if (!isNonMap && collider.name == MapData.NonMapColliderName) return false;
        if (ignoreDoors && doorColliders.Contains(collider.GetInstanceID())) return false;
        return true;
    }

    /// <summary>
    /// コライダーの頂点と辺を集めます。LightSourceRaycastRendererと異なり、複数パス・回転したBox・Circle等も考慮します。
    /// </summary>
    private void CollectShape(Collider2D collider)
    {
        var transform = collider.transform;
        Vector2 offset = collider.offset;
        Vector2 World(Vector2 local) => transform.TransformPoint(local + offset);

        void AddLoop(Il2CppStructArray<Vector2> localPoints, bool closed)
        {
            int n = localPoints.Length;
            if (n == 0) return;
            Vector2 first = World(localPoints[0]);
            Vector2 last = first;
            vertexBuffer.Add(first);
            for (int i = 1; i < n; i++)
            {
                var p = World(localPoints[i]);
                vertexBuffer.Add(p);
                segmentBuffer.Add((last, p));
                last = p;
            }
            if (closed && n > 2) segmentBuffer.Add((last, first));
        }

        void AddWorldLoop(Vector2[] worldPoints)
        {
            for (int i = 0; i < worldPoints.Length; i++)
            {
                vertexBuffer.Add(worldPoints[i]);
                segmentBuffer.Add((worldPoints[i], worldPoints[(i + 1) % worldPoints.Length]));
            }
        }

        var edge = collider.TryCast<EdgeCollider2D>();
        if (edge != null)
        {
            AddLoop(edge.points, false);
            return;
        }

        var polygon = collider.TryCast<PolygonCollider2D>();
        if (polygon != null)
        {
            for (int i = 0; i < polygon.pathCount; i++) AddLoop(polygon.GetPath(i), true);
            return;
        }

        var box = collider.TryCast<BoxCollider2D>();
        if (box != null)
        {
            var half = box.size * 0.5f;
            AddWorldLoop([World(new(-half.x, -half.y)), World(new(half.x, -half.y)), World(new(half.x, half.y)), World(new(-half.x, half.y))]);
            return;
        }

        var circle = collider.TryCast<CircleCollider2D>();
        if (circle != null)
        {
            Vector2 center = World(Vector2.zero);
            var scale = transform.lossyScale;
            float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            const int div = 24;
            var loop = new Vector2[div];
            for (int i = 0; i < div; i++)
            {
                float a = Mathf.PI * 2f * i / div;
                loop[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            AddWorldLoop(loop);
            return;
        }

        //その他(Capsule等)はAABBで近似
        //(CompositeCollider2D等、ゲーム側のIl2Cppに型が含まれていないものはTryCastすると例外になるため扱わない)
        var bounds = collider.bounds;
        AddWorldLoop([new(bounds.min.x, bounds.min.y), new(bounds.max.x, bounds.min.y), new(bounds.max.x, bounds.max.y), new(bounds.min.x, bounds.max.y)]);
    }

    private float CastRay(Vector2 origin, Vector2 dir, float range, bool isNonMap)
    {
        int num = FastMethods.RaycastNonAllocFast(origin, dir, castBuffer, range, Constants.ShipAndAllObjectsMask);
        float min = range;
        for (int i = 0; i < num; i++)
        {
            var hit = castBuffer[i];
            if (hit.distance < min && IsBlocking(hit.collider, isNonMap)) min = hit.distance;
        }
        return min;
    }

    /// <summary>
    /// 点から障害物を跨がずに到達できる領域を多角形として求めます。
    /// </summary>
    private void ComputePolygon(EditPoint point)
    {
        Vector2 origin = point.Position;
        float range = point.Range;
        bool isNonMap = point.IsNonMap;

        vertexBuffer.Clear();
        segmentBuffer.Clear();
        angleBuffer.Clear();

        int num = FastMethods.OverlapCircleNonAllocFast(origin, range, overlapBuffer, Constants.ShipAndAllObjectsMask);
        for (int i = 0; i < num; i++)
        {
            var collider = overlapBuffer[i];
            if (!IsBlocking(collider, isNonMap)) continue;
            try
            {
                CollectShape(collider);
            }
            catch (Exception ex)
            {
                //形状を取得できないコライダーはAABBで近似する
                NebulaLogger.Instance.Error($"MapAreaEditor: Failed to collect the shape of {collider.name}. ({ex.Message})");
                var bounds = collider.bounds;
                Vector2[] corners = [new(bounds.min.x, bounds.min.y), new(bounds.max.x, bounds.min.y), new(bounds.max.x, bounds.max.y), new(bounds.min.x, bounds.max.y)];
                for (int c = 0; c < 4; c++)
                {
                    vertexBuffer.Add(corners[c]);
                    segmentBuffer.Add((corners[c], corners[(c + 1) % 4]));
                }
            }
        }

        void AddAngleAround(Vector2 target)
        {
            var del = target - origin;
            float dist = del.magnitude;
            if (dist < 1e-4f) return;
            float angle = Mathf.Atan2(del.y, del.x);
            float eps = Mathf.Max(VertexTolerance / dist, 1e-5f);
            angleBuffer.Add(angle - eps);
            angleBuffer.Add(angle + eps);
        }

        //頂点の両脇
        foreach (var v in vertexBuffer) if ((v - origin).sqrMagnitude <= range * range) AddAngleAround(v);

        //辺と到達範囲の円との交点の両脇
        foreach (var (a, b) in segmentBuffer)
        {
            var d = b - a;
            var f = a - origin;
            float qa = Vector2.Dot(d, d);
            if (qa < 1e-8f) continue;
            float qb = 2f * Vector2.Dot(f, d);
            float qc = Vector2.Dot(f, f) - range * range;
            float disc = qb * qb - 4f * qa * qc;
            if (disc < 0f) continue;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-qb - sq) / (2f * qa), t2 = (-qb + sq) / (2f * qa);
            if (t1 >= 0f && t1 <= 1f) AddAngleAround(a + d * t1);
            if (t2 >= 0f && t2 <= 1f) AddAngleAround(a + d * t2);
        }

        //円弧部分
        int arcRays = Mathf.CeilToInt(360f / ArcStepDegree);
        for (int i = 0; i < arcRays; i++) angleBuffer.Add(-Mathf.PI + Mathf.PI * 2f * i / arcRays);

        //角度を[-π, π)に正規化して整列
        for (int i = 0; i < angleBuffer.Count; i++)
        {
            float a = angleBuffer[i];
            if (a < -Mathf.PI) a += Mathf.PI * 2f;
            else if (a >= Mathf.PI) a -= Mathf.PI * 2f;
            angleBuffer[i] = a;
        }
        angleBuffer.Sort();

        var polygon = new List<Vector2>(angleBuffer.Count);
        float lastAngle = float.NegativeInfinity;
        float maxDistance = 0f;
        foreach (var angle in angleBuffer)
        {
            if (angle - lastAngle < 1e-6f) continue;
            lastAngle = angle;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float dist = CastRay(origin, dir, range, isNonMap);
            if (dist > maxDistance) maxDistance = dist;
            polygon.Add(origin + dir * dist);
        }

        point.Polygon = polygon.ToArray();
        point.IsBuried = maxDistance < 0.05f;
        point.Area = 0f;
        for (int i = 0, j = point.Polygon.Length - 1; i < point.Polygon.Length; j = i++)
            point.Area += point.Polygon[j].x * point.Polygon[i].y - point.Polygon[i].x * point.Polygon[j].y;
        point.Area = Mathf.Abs(point.Area) * 0.5f;

        UpdateMesh(point);
        UpdatePointAppearance(point);
    }

    private void UpdateMesh(EditPoint point)
    {
        var polygon = point.Polygon!;
        Vector2 origin = point.Position;
        int n = polygon.Length;

        //扇形のメッシュ (頂点0が中心)
        var vertices = new Vector3[n + 1];
        var colors = new Color[n + 1];
        vertices[0] = new Vector3(origin.x, origin.y, 0f);
        colors[0] = Color.white;
        for (int i = 0; i < n; i++)
        {
            vertices[i + 1] = new Vector3(polygon[i].x, polygon[i].y, 0f);
            colors[i + 1] = Color.white;
        }
        var triangles = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % n + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        var mesh = point.FillFilter.mesh;
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        point.Outline.positionCount = n;
        point.Outline.SetPositions(polygon.Select(p => new Vector3(p.x, p.y, 0f)).ToArray());
    }

    private void Recompute(EditPoint point)
    {
        pending.Remove(point);
        ComputePolygon(point);
    }

    private void RecomputeAll()
    {
        pending.Clear();
        foreach (var p in points)
        {
            p.Polygon = null;
            pending.Add(p);
        }
    }

    private void ProcessJobs()
    {
        if (pending.Count == 0) return;
        var stopwatch = Stopwatch.StartNew();
        while (pending.Count > 0 && stopwatch.Elapsed.TotalMilliseconds < ComputeBudgetMs)
        {
            var next = pending[0];
            pending.RemoveAt(0);
            ComputePolygon(next);
        }
    }

    // ---------------------------------------------------------------- 表示

    private string GetStatusText()
    {
        var builder = new StringBuilder();
        builder.Append($"<b>MapArea Editor</b> (map: {mapId}, inMapId: {inMapId})  MapArea: {points.Count(p => !p.IsNonMap)}, NonMapArea: {points.Count(p => p.IsNonMap)}");
        builder.Append($"  ドア: {(ignoreDoors ? "無視" : "障害物")}");
        int muted = points.Count(p => p.Muted);
        if (muted > 0) builder.Append($"  ミュート中: {muted} 点");
        if (pending.Count > 0) builder.Append($"  <color=#FFCC44>計算中... 残り {pending.Count} 点</color>");
        if (selected != null)
        {
            builder.Append("<br>選択中: ");
            builder.Append(selected.IsNonMap ? "NonMapArea" : "MapArea");
            builder.Append($" ({Format(selected.Position.x)}, {Format(selected.Position.y)})");
            if (selected.IsAdded) builder.Append(" [追加]");
            else if (selected.Moved) builder.Append(" [移動]");
            if (selected.Muted) builder.Append(" <color=#AAAAAA>[ミュート中]</color>");
            if (selected.Polygon == null) builder.Append(" 計算待ち");
            else
            {
                builder.Append($" 面積: {selected.Area:F1}, 頂点数: {selected.Polygon.Length}");
                if (selected.IsBuried) builder.Append(" <color=#FF44FF>コライダーに埋まっています</color>");
            }
        }
        if (hoverText.Length > 0) builder.Append("<br>" + hoverText);
        return builder.ToString();
    }

    private void UpdateDisplay()
    {
        foreach (var p in points)
        {
            bool visible = p.Polygon != null && !p.Muted;
            if (p.FillRenderer) p.FillRenderer.enabled = visible;
            if (p.Outline) p.Outline.enabled = visible;
        }
    }

    // ---------------------------------------------------------------- 入力

    void OnUpdate(GameHudUpdateEvent ev)
    {
        HandleInput();
        ProcessJobs();
        UpdateDisplay();
    }

    private void HandleInput()
    {
        if (NebulaInput.SomeUiIsActive || MeetingHud.Instance)
        {
            hoverText = "";
            return;
        }

        VVector2 mouse = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        //カーソル位置の情報 (描画している多角形に基づく)
        {
            int m = 0, n = 0;
            foreach (var p in points)
            {
                if (p.Muted || !p.Contains(mouse)) continue;
                if (p.IsNonMap) n++; else m++;
            }
            string state = n > 0 ? "<color=#FF6666>エリア外 (NonMapArea)</color>" : m > 0 ? "<color=#66FF66>エリア内</color>" : "エリア外";
            hoverText = $"カーソル: ({Format(mouse.x)}, {Format(mouse.y)}) {state}  MapArea: {m}, NonMapArea: {n}";
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (ctrl)
            {
                AddPoint(mouse, shift);
            }
            else
            {
                var hit = FindPoint(mouse);
                Select(hit);
                if (hit != null)
                {
                    dragging = hit;
                    dragStartMouse = mouse;
                    dragStartPos = hit.Position;
                    dragMoved = false;
                }
            }
        }

        if (dragging != null)
        {
            if (Input.GetMouseButton(0))
            {
                var diff = mouse - dragStartMouse;
                if (!dragMoved && diff.Magnitude > 0.05f) dragMoved = true;
                if (dragMoved)
                {
                    var newPos = dragStartPos + diff;
                    if ((newPos - dragging.Position).Magnitude > 0.001f)
                    {
                        dragging.Position = newPos;
                        if (!dragging.IsAdded) dragging.Moved = true;
                        Recompute(dragging);
                    }
                }
            }
            else
            {
                dragging = null;
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            var hit = FindPoint(mouse);
            if (hit != null) RemovePoint(hit);
        }

        if (Input.GetKeyDown(KeyCode.Delete) && selected != null) RemovePoint(selected);

        if (Input.GetKeyDown(KeyCode.M) && selected != null)
        {
            selected.Muted = !selected.Muted;
            UpdatePointAppearance(selected);
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            ignoreDoors = !ignoreDoors;
            CollectDoorColliders();
            RecomputeAll();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            CollectDoorColliders();
            RecomputeAll();
            DebugScreen.Push("すべての点を再計算します。", 2f);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            var output = GenerateOutput();
            ClipboardHelper.PutClipboardString(output);
            NebulaLogger.Instance.Message("MapArea Editor output:\n" + output);
            DebugScreen.Push("編集結果をクリップボードにコピーしました。", 3f);
        }
    }

    // ---------------------------------------------------------------- 出力

    static private string Format(float value) => value.ToString("0.0##", CultureInfo.InvariantCulture);
    static private string FormatVector(VVector2 v) => $"new({Format(v.x)}f, {Format(v.y)}f)";

    /// <summary>
    /// 代表点の配列を元データの順序で出力します。削除した点はコメントアウトし、移動した点は元の位置を併記し、追加した点は末尾に並べます。
    /// </summary>
    /// <param name="originals">元データの (インデックス, 位置) の組。インデックスはEditPoint.OriginIndexに対応します。</param>
    static private void AppendVectorArray(StringBuilder builder, IEnumerable<(int index, VVector2 pos)> originals, EditPoint[] edited)
    {
        var byOrigin = new Dictionary<int, EditPoint>();
        foreach (var p in edited) if (!p.IsAdded) byOrigin[p.OriginIndex] = p;

        builder.Append("[\n");
        foreach (var (index, pos) in originals)
        {
            if (!byOrigin.TryGetValue(index, out var p))
                builder.Append($"    //{FormatVector(pos)}, //削除\n");
            else if (p.Moved)
                builder.Append($"    {FormatVector(p.Position)}, //移動 (元: {FormatVector(pos)})\n");
            else
                builder.Append($"    {FormatVector(p.Position)},\n");
        }
        foreach (var p in edited.Where(p => p.IsAdded)) builder.Append($"    {FormatVector(p.Position)}, //追加\n");
        builder.Append("]\n");
    }

    /// <summary>
    /// 編集前のMapAreaの代表点を (EditPoint.OriginIndexに対応するインデックス, 位置) の組で返します。
    /// </summary>
    private IEnumerable<(int index, VVector2 pos)> GetOriginalMapArea()
    {
        if (isFungle)
        {
            int bit = 1 << (inMapId & ~1);
            var raw = FungleData.RawMapPositions;
            for (int i = 0; i < raw.Count; i++) if ((raw[i].inMapFlag & bit) != 0) yield return (i, raw[i].pos);
        }
        else
        {
            var mapArea = mapData.MapArea;
            for (int i = 0; i < mapArea.Count; i++) yield return (i, mapArea[i]);
        }
    }

    private string GenerateOutput()
    {
        var builder = new StringBuilder();
        var mapPoints = points.Where(p => !p.IsNonMap).ToArray();
        var nonMapPoints = points.Where(p => p.IsNonMap).ToArray();

        int originalMapCount = isFungle ? FungleData.RawMapPositions.Count(e => (e.inMapFlag & (1 << (inMapId & ~1))) != 0) : mapData.MapArea.Count;
        int originalNonMapCount = mapData.NonMapArea.Count;

        builder.Append($"// MapArea Editor output (mapId: {mapId}, inMapId: {inMapId})\n");
        builder.Append($"// MapArea: {mapPoints.Length} 点 (元: {originalMapCount} 点, 追加: {mapPoints.Count(p => p.IsAdded)}, 移動: {mapPoints.Count(p => p.Moved)}, 削除: {originalMapCount - mapPoints.Count(p => !p.IsAdded)})\n");
        builder.Append($"// NonMapArea: {nonMapPoints.Length} 点 (元: {originalNonMapCount} 点, 追加: {nonMapPoints.Count(p => p.IsAdded)}, 移動: {nonMapPoints.Count(p => p.Moved)}, 削除: {originalNonMapCount - nonMapPoints.Count(p => !p.IsAdded)})\n\n");

        builder.Append("// MapArea\n");
        AppendVectorArray(builder, GetOriginalMapArea(), mapPoints);
        builder.Append("\n// NonMapArea\n");
        AppendVectorArray(builder, mapData.NonMapArea.Select((pos, i) => (i, pos)), nonMapPoints);

        if (isFungle) AppendFungleMapPositions(builder, mapPoints);

        return builder.ToString();
    }

    /// <summary>
    /// FungleData.MapPositionsの形式で出力します。
    /// 削除・移動した点は元の項目から編集中のバリエーションのビットを外し、追加・移動後の点は編集中のバリエーションのビットのみを持つ項目として末尾に追加します。
    /// </summary>
    private void AppendFungleMapPositions(StringBuilder builder, EditPoint[] mapPoints)
    {
        //簡易ラボの有無(inMapIdの最下位ビット)はMapAreaに影響しないため、対になるバリエーションをまとめて編集する
        int editMask = 3 << (inMapId & ~1);
        int baseBit = 1 << (inMapId & ~1);

        var kept = new HashSet<int>(mapPoints.Where(p => !p.IsAdded && !p.Moved).Select(p => p.OriginIndex));
        var raw = FungleData.RawMapPositions;

        builder.Append($"\n// FungleData.MapPositions (編集対象ビット: 0x{editMask:X2})\n");
        builder.Append("static private readonly (VVector2 pos, int inMapFlag)[] MapPositions = [\n");
        for (int i = 0; i < raw.Count; i++)
        {
            var (pos, flag) = raw[i];
            int originalFlag = flag;
            string note = "";
            if ((flag & baseBit) != 0 && !kept.Contains(i))
            {
                flag &= ~editMask;
                note = $" //編集により除外 (元: 0x{originalFlag:X2})";
            }
            if (flag == 0)
                builder.Append($"    //({FormatVector(pos)}, 0x{originalFlag:X2}), //削除 (どのバリエーションでも使われなくなった点)\n");
            else
                builder.Append($"    ({FormatVector(pos)}, 0x{flag:X2}),{note}\n");
        }
        var added = mapPoints.Where(p => p.IsAdded || p.Moved).ToArray();
        if (added.Length > 0)
        {
            builder.Append($"    //MapArea Editorで追加 (inMapId: {inMapId & ~1}, {(inMapId & ~1) + 1})\n");
            foreach (var p in added)
                builder.Append($"    ({FormatVector(p.Position)}, 0x{editMask:X2}),{(p.Moved ? $" //移動 (元: {FormatVector(raw[p.OriginIndex].pos)})" : " //追加")}\n");
        }
        builder.Append("    ];\n");
    }
}
