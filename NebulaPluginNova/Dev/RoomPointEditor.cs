using Il2CppSystem.IO;
using Microsoft.CodeAnalysis;
using Nebula.Behavior;
using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;
using Virial.Utilities;
using static Rewired.Demos.CustomPlatform.MyPlatformControllerExtension;

namespace Nebula.Dev;

[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class RoomPointEditor : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) => DIManager.Instance.RegisterModule(() => DebugTools.UseRoomPointEditor ? new RoomPointEditor() : null);
    public RoomPointEditor()
    {
        ModSingleton<RoomPointEditor>.Instance = this;
        this.Register(NebulaAPI.CurrentGame!);
    }

    Transform nodeParent = null!, pathParent = null!;
    SpriteRenderer customNearbyRangeRenderer = null!;
    Transform GetMapTransform()
    {
        if (!nodeParent) nodeParent = UnityHelper.CreateObject("MapParent", null, new(0f, 0f, -10f)).transform;
        return nodeParent;
    }

    static private Image image = SpriteLoader.FromResource("Nebula.Resources.WhiteCircle.png", 100f);
    static private Image squareImage = SpriteLoader.FromResource("Nebula.Resources.White.png", 100f);

    private class Node
    {
        public SpriteRenderer Renderer { get; set; }
        public bool IsDetail { get; set; }
        public List<Edge> RelatedNodes = [];
        public int Id = -1;
        public float? CustomNearbyRange = null;
        public Node(bool isDetail, SpriteRenderer renderer)
        {
            IsDetail = isDetail;
            Renderer = renderer;
        }
    }
    private record Edge(GameObject Obj, Node Node1, Node Node2);

    List<Node> MainNodes = [];
    List<Node> DetailNodes = [];
    List<NavSpecialEdge> CopiedSpecialEdges = [];
    Node? currentSelected = null;

    void DestroyAll()
    {
        MainNodes.Clear();
        DetailNodes.Clear();
        CopiedSpecialEdges.Clear();
        if (nodeParent) GameObject.Destroy(nodeParent.gameObject);
        currentSelected = null;
    }

    void AddEdge(Node node1, Node node2)
    {
        var pos1 = node1.Renderer.transform.position;
        var pos2 = node2.Renderer.transform.position;
        Vector2 diff = pos2 - pos1;
        var edgeRenderer = UnityHelper.CreateObject<SpriteRenderer>("Edge", GetMapTransform(), ((Vector2)((pos1 + pos2) * 0.5f)).AsVector3(-4f));
        edgeRenderer.transform.localScale = new(diff.magnitude, 0.05f);
        edgeRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x).RadToDeg());
        edgeRenderer.sprite = squareImage.GetSprite();

        var edge = new Edge(edgeRenderer.gameObject, node1, node2);
        node1.RelatedNodes.Add(edge);
        node2.RelatedNodes.Add(edge);

        var button = edgeRenderer.gameObject.SetUpButton(true).gameObject.AddComponent<ExtraPassiveBehaviour>();
        button.OnRightClicked = () => {

            edge.Node1.RelatedNodes.Remove(edge);
            edge.Node2.RelatedNodes.Remove(edge);
            GameObject.Destroy(edgeRenderer.gameObject);
        };
        var collider = edgeRenderer.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new(1f, 1.25f);
    }

    void AddNode(Vector2 position, bool isDetail)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Node", GetMapTransform(), position.AsVector3(-5f));
        renderer.sprite = image.GetSprite();
        renderer.transform.localScale = new Vector3(0.8f, 0.8f, 1f) * (isDetail ? 1f : 1.5f);
        var button = renderer.gameObject.SetUpButton(true);
        var collider = renderer.gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.13f;

        var node = new Node(isDetail, renderer);
        if (isDetail)
            DetailNodes.Add(node);
        else
            MainNodes.Add(node);

            button.OnClick.AddListener(() =>
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    //シフトクリックなら辺を伸ばす
                    if (currentSelected?.Renderer ?? false)
                    {

                        if (node.RelatedNodes.Any(n => n.Node1 == currentSelected || n.Node2 == currentSelected))
                        {
                            DebugScreen.Push("既に同じ辺があります。", 2f);
                        }
                        else if (currentSelected == node)
                        {
                            DebugScreen.Push("始点と終点は異なる必要があります。", 2f);
                        }
                        else if (currentSelected.IsDetail && node.IsDetail)
                        {
                            DebugScreen.Push("始点と終点のいずれか一方は<br>主要ノードである必要があります。", 2f);
                        }
                        else
                        {
                            AddEdge(node, currentSelected);
                        }

                    }
                }
                else
                {
                    //シフトを押さえないクリックならノードを伸ばす元の変更
                    if (currentSelected?.Renderer ?? false) currentSelected.Renderer.color = Color.white;
                    renderer.color = Color.yellow;
                    currentSelected = node;
                }
            });

        //右クリックでノードの削除
        button.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked = () => {
            foreach (var relatedNodes in node.RelatedNodes)
            {
                var targetNode = relatedNodes.Node1 == node ? relatedNodes.Node2 : relatedNodes.Node1;
                targetNode.RelatedNodes.RemoveAll(e => e.Node1 == node || e.Node2 == node);
                GameObject.Destroy(relatedNodes.Obj);
            }
            GameObject.Destroy(renderer.gameObject);
            DetailNodes.Remove(node);
            MainNodes.Remove(node);
        };
    }

    Vector2 fromCache;
    void OnUpdate(GameHudUpdateEvent ev)
    {
        if (Input.GetKeyDown(KeyCode.Y))
        {
            bool isDetail = Input.GetKey(KeyCode.LeftShift);
            AddNode(PlayerControl.LocalPlayer.GetTruePosition(), isDetail);
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.RightShift))
            {
                var json = Helpers.GetClipboardString();
                NavVerticesStructure? structure = null;
                try
                {
                    structure = JsonStructure.Deserialize<NavVerticesStructure>(json);
                }
                catch
                {
                    DebugScreen.Push("JSONが不適格です。", 2f);
                    return;
                }
                if(structure == null)
                {
                    DebugScreen.Push("読み込みに失敗しました。", 2f);
                }
                else
                {
                    DestroyAll();
                    LogUtils.WriteToConsole(JsonStructure.Serialize(structure));

                    foreach (var node in structure.MainNodes)
                    {
                        AddNode(new(node.X, node.Y), false);
                        var myNode = MainNodes[^1];
                        foreach (var target in node.Nodes) if(target < MainNodes.Count) AddEdge(MainNodes[target], myNode);
                    }
                    foreach (var node in structure.SubNodes)
                    {
                        AddNode(new(node.X, node.Y), true);
                        var myNode = DetailNodes[^1];
                        foreach(var target in node.Nodes) AddEdge(MainNodes[target], myNode);
                    }
                    CopiedSpecialEdges = structure.SpecialEdges;
                    
                    DebugScreen.Push("クリップボードから読み込みました。", 2f);
                }
                
            }
            else
            {
                ClipboardHelper.PutClipboardString(JsonStructure.Serialize(OutputStructure()));
                DebugScreen.Push("クリップボードにコピーしました。", 2f);
            }
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                //var from = fromCache;
                //var to = PlayerControl.LocalPlayer.GetTruePosition();
                var to = fromCache;
                var from = PlayerControl.LocalPlayer.GetTruePosition();
                var structure = OutputStructure();

                int[] path = null!;
                NavVerticesHelpers.GetPathfindingNode(structure, from, to, 8f, 3.2f, out var positions, out var nextNodes);

                if (pathParent) GameObject.Destroy(pathParent.gameObject);

                path = Pathfinding.FindPath(positions, nextNodes, positions.Length - 2, positions.Length - 1);

                if (path == null)
                {
                    DebugScreen.Push("想定外のケース (path == null)", 2f);
                }
                else if (path.Length == 0)
                {
                    DebugScreen.Push("到達不可能です。", 2f);
                }
                else
                {
                    
                    pathParent = UnityHelper.CreateObject("Parent", null, new(0f,0f,-10f)).transform;
                    int lastIndex = -1;
                    for (int i = 0; i < path.Length; i++)
                    {
                        if (i > 0)
                        {
                            UnityEngine.Vector2 pos1 = positions[lastIndex];
                            UnityEngine.Vector2 pos2 = positions[path[i]];
                            UnityEngine.Vector2 diff = pos2 - pos1;
                            var edgeRenderer = UnityHelper.CreateObject<SpriteRenderer>("Edge", pathParent, ((Vector2)((pos1 + pos2) * 0.5f)).AsVector3(-6f));
                            edgeRenderer.transform.localScale = new(diff.magnitude, 0.05f);
                            edgeRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x).RadToDeg());
                            edgeRenderer.sprite = squareImage.GetSprite();
                            edgeRenderer.color = Color.red;
                        }
                        lastIndex = path[i];
                    }
                    

                    //NebulaManager.Instance.StartCoroutine(NavVerticesHelpers.WalkPath(path.Select(i => positions[i].ToUnityVector()).ToArray(), Helpers.GetPlayer(1)).WrapToIl2Cpp());
                }

            }
            else
            {
                fromCache = PlayerControl.LocalPlayer.GetTruePosition();
                DebugScreen.Push("現在位置をコピーしました。", 2f);
            }
        }

        if(customNearbyRangeRenderer && customNearbyRangeRenderer.color.a > 0f)
        {
            var lastColor = customNearbyRangeRenderer.color;
            lastColor.a -= Time.deltaTime * 0.2f;
            customNearbyRangeRenderer.color = lastColor;
        }

        if (Input.GetKey(KeyCode.LeftShift) && currentSelected != null)
        {
            if (currentSelected.Renderer.transform.position.Distance(PlayerControl.LocalPlayer.transform.position) < 2f)
            {

                float axis = Input.GetKeyDown(KeyCode.K) ? -0.4f : Input.GetKeyDown(KeyCode.L) ? 0.4f : 0f;

                if (Math.Abs(axis) > 0f)
                {
                    currentSelected!.CustomNearbyRange ??= 3f;
                    currentSelected.CustomNearbyRange += axis;

                    if (!customNearbyRangeRenderer)
                    {
                        customNearbyRangeRenderer = UnityHelper.CreateObject<SpriteRenderer>("NearbyRange", GetMapTransform(), currentSelected.Renderer.transform.localPosition);
                        customNearbyRangeRenderer.sprite = squareImage.GetSprite();
                    }
                    customNearbyRangeRenderer.transform.localScale = new Vector3(1f, 1f, 1f) * (currentSelected.CustomNearbyRange.Value * 2f);
                    customNearbyRangeRenderer.transform.position = currentSelected.Renderer.transform.localPosition;
                    customNearbyRangeRenderer.color = new(1f, 1f, 1f, 0.4f);
                }
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                if (MainNodes.Remove(currentSelected))
                {
                    MainNodes.Insert(0, currentSelected);
                    DebugScreen.Push("先頭に移動しました。", 2f);
                }
                else
                {
                    DebugScreen.Push("骨組みノードのみ順序を入れ替えられます。", 2f);
                }
            }
        }
        

    }

    NavVerticesStructure OutputStructure()
    {
        for (int i = 0; i < MainNodes.Count; i++) MainNodes[i].Id = i;
        NavVerticesStructure structure = new();

        structure.SpecialEdges = CopiedSpecialEdges;

        for (int i = 0; i < MainNodes.Count; i++)
        {
            var node = MainNodes[i];
            NavVertexStructure vertex = new();
            vertex.X = node.Renderer.transform.position.x;
            vertex.Y = node.Renderer.transform.position.y;
            vertex.CustomNearbyRange = node.CustomNearbyRange;
            foreach(var edge in node.RelatedNodes)
            {
                var targetNode = edge.Node1.Id == node.Id ? edge.Node2 : edge.Node1;
                if (targetNode.IsDetail) continue;
                vertex.Nodes.Add(targetNode.Id);
            }
            structure.MainNodes.Add(vertex);
        }

        for (int i = 0; i < DetailNodes.Count; i++)
        {
            var node = DetailNodes[i];
            NavVertexStructure vertex = new();
            vertex.X = node.Renderer.transform.position.x;
            vertex.Y = node.Renderer.transform.position.y;
            vertex.CustomNearbyRange = node.CustomNearbyRange;
            foreach (var edge in node.RelatedNodes)
            {
                var targetNode = edge.Node1.IsDetail ? edge.Node2 : edge.Node1;
                vertex.Nodes.Add(targetNode.Id);
            }
            structure.SubNodes.Add(vertex);
        }

        return structure;
    }
}

