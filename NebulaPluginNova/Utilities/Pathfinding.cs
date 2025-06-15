using Nebula.Map;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Virial.Game;
using Virial.Utilities;

namespace Nebula.Utilities;

public class NavVerticesStructure
{
    [JsonSerializableField]
    public List<NavVertexStructure> MainNodes = [];
    [JsonSerializableField]
    public List<NavVertexStructure> SubNodes = [];
    [JsonSerializableField]
    public List<NavSpecialEdge> SpecialEdges = [];
}

public class NavVertexStructure
{
    [JsonSerializableField]
    public float X;
    [JsonSerializableField]
    public float Y;
    [JsonSerializableField]
    public List<int> Nodes = [];
    [JsonSerializableField(true)]
    public float? CustomNearbyRange = null;
}

public class NavSpecialEdge
{
    [JsonSerializableField]
    public string Tag;
    [JsonSerializableField]
    public int From;
    [JsonSerializableField]
    public int To;
}


public static class NavVerticesHelpers
{
    private const int AdditionalSubNodesCount = 50; //追加されるであろうサブノード数上限の見積もり
    private const int AdditionalSubNodesPerNodeCount = 20; //ノードごとに追加されるであろうサブノード数上限の見積もり

    /// <summary>
    /// 
    /// </summary>
    /// <param name="structure"></param>
    /// <param name="from">この点は末尾から2番目に格納されます。</param>
    /// <param name="to">この点は末尾に格納されます。</param>
    /// <param name="detailRange"></param>
    /// <param name="positions"></param>
    /// <param name="nextNodes"></param>
    public static void GetPathfindingNode(this NavVerticesStructure structure, Vector2 from, Vector2 to, float detailRange, float defaultNearbyRange, out Virial.Compat.Vector2[] positions, out int[][] nextNodes)
    {
        List<Virial.Compat.Vector2> positionsList = new(structure.MainNodes.Count + 50);
        List<List<int>> mainNextNodes = new(structure.MainNodes.Count);
        List<int[]> subNextNodes = new(50);

        List<int> fromNearby = [], toNearby = [];

        //現在のゲーム空間で移動の可能性を調べます。
        bool CanMove(Vector2 from, float toX, float toY) => !Helpers.AnyNonTriggersBetween(from, new(toX, toY), out _);

        foreach (var node in structure.MainNodes)
        {
            positionsList.Add(new(node.X, node.Y));
            var myNextNodes = new List<int>(node.Nodes);
            mainNextNodes.Add(myNextNodes);

            //相対的な位置は後で正しいものに直す
            if (from.Distance(new(node.X, node.Y)) < (node.CustomNearbyRange ?? defaultNearbyRange) && CanMove(from, node.X, node.Y))
            {
                myNextNodes.Add(-2);
                fromNearby.Add(positionsList.Count - 1);
            }
            if (to.Distance(new(node.X, node.Y)) < (node.CustomNearbyRange ?? defaultNearbyRange) && CanMove(to, node.X, node.Y))
            {
                myNextNodes.Add(-1);
                toNearby.Add(positionsList.Count - 1);
            }
        }
        foreach (var node in structure.SubNodes)
        {
            if (
                (MathF.Abs(node.X - from.x) < detailRange && MathF.Abs(node.Y - from.y) < detailRange) ||
                (MathF.Abs(node.X - to.x) < detailRange && MathF.Abs(node.Y - to.y) < detailRange)
                )
            {
                positionsList.Add(new(node.X, node.Y));

                IEnumerable<int> myNextNodes = node.Nodes;
                //相対的な位置は後で正しいものに直す
                if (from.Distance(new(node.X, node.Y)) < (node.CustomNearbyRange ?? defaultNearbyRange) && CanMove(from, node.X, node.Y))
                {
                    myNextNodes = myNextNodes.Append(-2);
                    fromNearby.Add(positionsList.Count - 1);
                }
                if (to.Distance(new(node.X, node.Y)) < (node.CustomNearbyRange ?? defaultNearbyRange) && CanMove(to, node.X, node.Y))
                {
                    myNextNodes = myNextNodes.Append(-1);
                    toNearby.Add(positionsList.Count - 1);
                }

                subNextNodes.Add(myNextNodes.ToArray());
                foreach (var id in node.Nodes) mainNextNodes[id].Add(positionsList.Count - 1);
            }
        }

        //特別な辺をひく
        {
            ElectricalDoors electricalDoors = null!;
            foreach (var edge in structure.SpecialEdges)
            {
                void AddBidirectionalEdge()
                {
                    mainNextNodes[edge.From].Add(edge.To);
                    mainNextNodes[edge.To].Add(edge.From);
                }
                void AddSingleEdge()
                {
                    mainNextNodes[edge.From].Add(edge.To);
                }
                ElectricalDoors GetElecDoors()
                {
                    if (!electricalDoors) electricalDoors = ShipStatus.Instance.GetComponentInChildren<ElectricalDoors>();
                    return electricalDoors;
                }
                switch (edge.Tag)
                {
                    case string s when s.StartsWith("Electrical-"):
                        int i = int.Parse(s.Substring(11));
                        if (GetElecDoors().Doors[i].IsOpen) AddBidirectionalEdge();
                        break;
                    case "FungleLaboratory":
                        if (GeneralConfigurations.FungleSimpleLaboratoryOption.Value) AddBidirectionalEdge();
                        break;
                    case "AirshipMeetingLeft":
                        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.Value) AddSingleEdge();
                        else AddBidirectionalEdge();
                        break;
                    case "AirshipMeetingRight":
                        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.Value) AddSingleEdge();
                        break;
                }
            }
        }

        positionsList.Add(from);
        positionsList.Add(to);

        positions = positionsList.ToArray();
        nextNodes = new int[positions.Length][];
        for (int i = 0; i < nextNodes.Length; i++)
        {
            if (i < mainNextNodes.Count) nextNodes[i] = mainNextNodes[i].ToArray();
            else if (i == nextNodes.Length - 1) nextNodes[i] = toNearby.ToArray();
            else if (i == nextNodes.Length - 2) nextNodes[i] = fromNearby.ToArray();
            else nextNodes[i] = subNextNodes[i - mainNextNodes.Count];

            //末尾の添え字を正しい値に直す。
            for (int n = 0; n < nextNodes[i].Length; n++) if (nextNodes[i][n] < 0) nextNodes[i][n] += positions.Length;
        }
    }

    private const float VHMovementInitCoeff = 0.8f;


    internal static IEnumerator CoInteractManualDoor(IPlayerLogics player, ManualDoor door)
    {
        var decon = ShipStatus.Instance.Systems[SystemTypes.Decontamination].CastFast<DeconSystem>();
        while (decon.CurState != DeconSystem.States.Idle && !door.Opening) yield return null;
        if (door.Opening) yield break;

        door.SetDoorway(true);
        switch (player.Position.y)
        {
            case > 9.3f:
                decon.OpenDoor(true);
                break;
            case > 6f:
                decon.OpenFromInside(true);
                break;
            case > 2.7f:
                decon.OpenFromInside(false);
                break;
            default:
                decon.OpenDoor(false);
                break;
        }
        yield return Effects.Wait(0.4f);
        yield break;
    }
    internal static IEnumerator CoInteractDoor(IPlayerLogics player, OpenableDoor door)
    {
        if (door.TryCast<AutoOpenDoor>())
        {
            while (!door.IsOpen) yield return null;
            yield return Effects.Wait(0.4f);
            yield break;
        }
        if (door.TryCast<AutoCloseDoor>())
        {
            if (!door.IsOpen) door.SetDoorway(true);
            yield return Effects.Wait(0.4f);
            yield break;
        }
        if (door.TryCast<PlainDoor>())
        {
            var inner = door.transform.FindChild("InnerConsole");
            var outer = door.transform.FindChild("OuterConsole");
            if (inner && outer)
            {
                var innerDistance = inner.transform.position.Distance(player.Position);
                var outerDistance = outer.transform.position.Distance(player.Position);
                var decon = (innerDistance < outerDistance ? inner : outer).GetComponent<DeconControl>();
                if (decon)
                {
                    while (decon!.System.CurState != DeconSystem.States.Idle && !door.IsOpen) yield return null;
                    if (door.IsOpen) yield break;

                    decon.OnUse.Invoke();
                    yield return Effects.Wait(0.4f);
                    yield break;
                }
            }
            yield return Effects.Wait(3.8f);
            if (!door.IsOpen) door.SetDoorway(true);
            yield break;
        }
        if (door.TryCast<MushroomWallDoor>())
        {
            yield return Effects.Wait(5.2f);
            if (!door.IsOpen) door.SetDoorway(true);
            yield return Effects.Wait(0.4f);
            yield break;
        }
    }

    internal static IEnumerator WalkPath(Vector2[] path, IPlayerLogics player)
    {
        player.SnapTo(path[0] - player.GroundCollider.offset);
        player.ClearPositionQueues();
        player.UpdateNetworkTransformState(false);

        ZiplineConsole[] ziplineConsoles = [];
        ManualDoor[] manualDoors = [];

        {
            //MIRA HQ
            var miraShipStatus = ShipStatus.Instance.TryCast<MiraShipStatus>();
            if (miraShipStatus)
            {
                manualDoors = miraShipStatus!.FastRooms[SystemTypes.Decontamination].gameObject.GetComponentsInChildren<ManualDoor>();
            }

            //the Fungle
            var fungleShipStatus = ShipStatus.Instance.TryCast<FungleShipStatus>();
            if (fungleShipStatus)
            {
                ziplineConsoles = fungleShipStatus!.Zipline.GetComponentsInChildren<ZiplineConsole>();
            }
        }

        int currentTarget = 1;
        float VHMovementCoeff = VHMovementInitCoeff;
        Vector2 lastPos = player.Position;
        int noMoveCount = 0;

        while (currentTarget < path.Length)
        {
            if (player.Player.IsDead) break;

            var d = player.TrueSpeed * Time.fixedDeltaTime + 0.01f;

            Vector2 currentPos = player.Position;
            Vector2 currentDisp = currentPos - lastPos;

            if (currentPos.Distance(lastPos) < d * 0.5f) VHMovementCoeff -= Time.deltaTime * 3f;
            lastPos = currentPos;

            Vector2 currentGoal = path[currentTarget];
            Vector2 diff = currentGoal - player.TruePosition;


            Vector2 velocity = Vector2.zero;

            if (diff.magnitude < d * 0.7f)
            {
                currentTarget++;
                VHMovementCoeff = VHMovementInitCoeff;
                continue;
            }

            var absX = Math.Abs(diff.x);
            var absY = Math.Abs(diff.y);
            if (diff.x < -d)
            {
                if (absX > absY * VHMovementCoeff) velocity.x = -1f;
            }
            else if (diff.x > d)
            {
                if (absX > absY * VHMovementCoeff) velocity.x = 1f;
            }
            else velocity.x = diff.x;

            if (diff.y < -d)
            {
                if (absY > absX * 0.8f) velocity.y = -1f;
            }
            else if (diff.y > d)
            {
                if (absY > absX * 0.8f) velocity.y = 1f;
            }
            else velocity.y = diff.y;
            player.SetNormalizedVelocity(velocity.normalized);

            if (diff.magnitude < d)
            {
                currentTarget++;
                VHMovementCoeff = VHMovementInitCoeff;
            }

            float dispMagnitude = currentDisp.magnitude;

            if (dispMagnitude < 0.005f)
                noMoveCount++;
            else
                noMoveCount = 0;
            if (noMoveCount > 60) break;

            foreach (var door in manualDoors)
            {
                if (door.Opening) continue;
                var doorPos = door.transform.position;
                var distance = doorPos.Distance(currentPos);
                if (distance > (dispMagnitude < 0.01f ? 1.1f : 0.6f)) continue;
                var dir = (Vector2)door.transform.position - player.TruePosition;

                if (Vector2.Dot(dir, velocity.normalized) > 0.25f || distance < (dispMagnitude < 0.01f ? 0.9f : 0.27f))
                {
                    player.Body.velocity = Vector2.zero;
                    yield return CoInteractManualDoor(player, door);
                }
            }

            foreach (var door in ShipStatus.Instance.AllDoors)
            {
                if (door.IsOpen) continue;
                var doorPos = door.transform.position;
                var distance = doorPos.Distance(currentPos);
                if (distance > (dispMagnitude < 0.01f ? 1.1f : 0.6f)) continue;
                var dir = (Vector2)door.transform.position - player.TruePosition;

                if (Vector2.Dot(dir, velocity.normalized) > 0.25f || distance < (dispMagnitude < 0.01f ? 0.9f : 0.27f))
                {
                    player.Body.velocity = Vector2.zero;
                    yield return CoInteractDoor(player, door);
                }
            }

            foreach (var ladder in ShipStatus.Instance.Ladders)
            {
                var ladderPos = ladder.transform.position;
                var distance = ladderPos.Distance(currentPos);

                if (distance > 0.8f) continue;
                var dir = (Vector2)ladder.Destination.transform.position - player.TruePosition;

                //次のノードと梯子の行先はある程度近づける必要がある。
                if (ladder.Destination.transform.position.Distance(currentGoal) > 1.2f) continue;

                if (Vector2.Dot(dir, velocity.normalized) > 0.6f || (dispMagnitude < 0.01f && distance < 0.4f))
                {
                    player.Body.velocity = Vector2.zero;
                    yield return player.UseLadder(ladder);
                    break;
                }
            }

            foreach (var zipline in ziplineConsoles)
            {
                var ziplinePos = zipline.transform.position;
                var distance = ziplinePos.Distance(currentPos);

                if (distance > 3f) continue;

                var topDistance = zipline.zipline.dropPositionTop.position.Distance(currentPos);
                var bottomDistance = zipline.zipline.dropPositionBottom.position.Distance(currentPos);
                var targetTransform = topDistance > bottomDistance ? zipline.zipline.landingPositionTop : zipline.zipline.landingPositionBottom;
                //次のノードとジップラインの行先はある程度近づける必要がある。
                if (currentGoal.Distance(targetTransform.position) > 3f) continue;

                player.Body.velocity = Vector2.zero;
                yield return player.UseZipline(zipline);
                break;
            }


            yield return null;

        }

        player.Body.velocity = Vector2.zero;
        player.UpdateNetworkTransformState(true);
        if (!player.Player.IsDead) player.SnapTo(path[^1] - player.GroundCollider.offset);
    }

    static public Vector2[]? CalcPath(Vector2 from, Vector2 to)
    {
        if (Helpers.AnyNonTriggersBetween(from, to, out _))
        {
            MapData.GetCurrentMapData().MapNavData.GetPathfindingNode(from, to, 8f, 3.2f, out var positions, out var nextNodes);
            var indexPath = Pathfinding.FindPath(positions, nextNodes, positions.Length - 2, positions.Length - 1);
            if (indexPath.Length == 0) return null;
            
            return indexPath.Select(i => positions[i].ToUnityVector()).ToArray();
        }
        else
        {
            return [from, to];
        }
    }
}

