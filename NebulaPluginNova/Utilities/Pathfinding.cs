using MS.Internal.Xml.XPath;
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

[Flags]
public enum NavPathStopCondition
{
    None                    = 0x0000,
    ChangeMovingPlatState   = 0x0001,
}

public record NavPath(Vector2[] Path, NavPathStopCondition StopCond);

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
    public static void GetPathfindingNode(this NavVerticesStructure structure, Vector2 from, Vector2 to, float radius, float detailRange, float defaultNearbyRange, out Virial.Compat.Vector2[] positions, out int[][] nextNodes, out (int from, int to, NavPathStopCondition stopCond)[] conds)
    {
        List<(int from, int to, NavPathStopCondition stopCond)> condsList = [];

        List<Virial.Compat.Vector2> positionsList = new(structure.MainNodes.Count + 50);
        List<List<int>> mainNextNodes = new(structure.MainNodes.Count);
        List<int[]> subNextNodes = new(50);

        List<int> fromNearby = [], toNearby = [];

        //現在のゲーム空間で移動の可能性を調べます。
        bool CanMove(Vector2 from, float toX, float toY) => !Helpers.AnyCustomNonTriggersBetweenThick(from, new(toX, toY), radius, null, null, true);

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
                    if (!electricalDoors) electricalDoors = AmongUsLLImpl.ShipStatusInstance.GetComponentInChildren<ElectricalDoors>();
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
                    case "FungleLowerLadderLeft":
                        AddSingleEdge();
                        break;
                    case "FungleLowerLadderRight":
                        AddSingleEdge();
                        break;
                    case "AirshipMeetingLeft":
                        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.Value) AddSingleEdge();
                        else AddBidirectionalEdge();
                        break;
                    case "AirshipMeetingRight":
                        if (GeneralConfigurations.AirshipOneWayMeetingRoomOption.Value) AddSingleEdge();
                        break;
                    case "AirshipGapFromRight":
                        var fromNodeR = structure.MainNodes[edge.From];
                        if (from.Distance(new Vector2(fromNodeR.X, fromNodeR.Y)) < 5f)
                        {
                            var airshipR = AmongUsLLImpl.ShipStatusInstance.TryCast<AirshipStatus>();
                            if (airshipR && !airshipR!.GapPlatform.Target && !airshipR.GapPlatform.IsLeft)
                            {
                                AddSingleEdge();
                                condsList.Add((edge.From, edge.To, NavPathStopCondition.ChangeMovingPlatState));
                            }
                        }
                        break;
                    case "AirshipGapFromLeft":
                        var fromNodeL = structure.MainNodes[edge.From];
                        if (from.Distance(new Vector2(fromNodeL.X, fromNodeL.Y)) < 5f)
                        {
                            var airshipL = AmongUsLLImpl.ShipStatusInstance.TryCast<AirshipStatus>();
                            if (airshipL && !airshipL!.GapPlatform.Target && airshipL.GapPlatform.IsLeft)
                            {
                                AddSingleEdge();
                                condsList.Add((edge.From, edge.To, NavPathStopCondition.ChangeMovingPlatState));
                            }
                        }
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

        conds = condsList.ToArray();
    }

    private const float VHMovementInitCoeff = 0.8f;


    internal static IEnumerator CoInteractManualDoor(IPlayerLogics player, ManualDoor door)
    {
        var decon = AmongUsLLImpl.ShipStatusInstance.Systems[SystemTypes.Decontamination].CastFast<DeconSystem>();
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
        if (door.IsFast<AutoOpenDoor>())
        {
            while (!door.IsOpen) yield return null;
            yield return Effects.Wait(0.4f);
            yield break;
        }
        if (door.IsFast<AutoCloseDoor>())
        {
            if (!door.IsOpen)
            {
                AmongUsLLImpl.ShipStatusInstance.RpcUpdateSystem(SystemTypes.Doors, (byte)(door.Id | 64));
                door.SetDoorway(true);
            }
            yield return Effects.Wait(0.4f);
            yield break;
        }
        if (door.IsFast<PlainDoor>())
        {
            var inner = door.transform.FindChild("InnerConsole");
            var outer = door.transform.FindChild("OuterConsole");
            if (inner && outer)
            {
                var innerDistance = inner.ModGameObject(false).Position.Distance(player.Position);
                var outerDistance = outer.ModGameObject(false).Position.Distance(player.Position);
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
            if (!door.IsOpen)
            {
                AmongUsLLImpl.ShipStatusInstance.RpcUpdateSystem(SystemTypes.Doors, (byte)(door.Id | 64));
                door.SetDoorway(true);
            }
            yield break;
        }
        if (door.IsFast<MushroomWallDoor>())
        {
            yield return Effects.Wait(5.2f);
            if (!door.IsOpen)
            {
                AmongUsLLImpl.ShipStatusInstance.RpcUpdateSystem(SystemTypes.Doors, (byte)(door.Id | 64));
                door.SetDoorway(true);
            }
            yield return Effects.Wait(0.4f);
            yield break;
        }
    }

    internal static IEnumerator WalkPath(Vector2[] path, NavPathStopCondition stopCond, IPlayerLogics player, Func<bool>? interrupter = null)
    {
        bool RecalcPath()
        {
            var newPath = CalcPath(player.TruePosition, path.Last());
            if (newPath == null) return false;
            path = newPath.Path;
            stopCond = newPath.StopCond;
            return true;
        }

        player.SnapTo(path[0] - player.GroundCollider.offset);
        player.ClearPositionQueues();

        ZiplineConsole[] ziplineConsoles = [];
        ManualDoor[] manualDoors = [];
        MovingPlatformBehaviour movingPlatform = null!;

        {
            //MIRA HQ
            var miraShipStatus = AmongUsLLImpl.ShipStatusInstance.TryCast<MiraShipStatus>();
            if (miraShipStatus)
            {
                manualDoors = miraShipStatus!.FastRooms[SystemTypes.Decontamination].gameObject.GetComponentsInChildren<ManualDoor>();
            }

            //the Fungle
            var fungleShipStatus = AmongUsLLImpl.ShipStatusInstance.TryCast<FungleShipStatus>();
            if (fungleShipStatus)
            {
                ziplineConsoles = fungleShipStatus!.Zipline.GetComponentsInChildren<ZiplineConsole>();
            }

            //the Airship
            var airshipStatus = AmongUsLLImpl.ShipStatusInstance.TryCast<AirshipStatus>();
            if (airshipStatus)
            {
                movingPlatform = airshipStatus!.GapPlatform;
            }
        }

        int currentTarget = 1;
        float VHMovementCoeff = VHMovementInitCoeff;
        var lastPos = player.Position;
        int noMoveCount = 0;
        bool shouldNotSnapToTargetPos = false;

        while (currentTarget < path.Length && player.IsActive)
        {
            if (player.Player.IsDead) break;
            if (interrupter?.Invoke() ?? false)
            {
                shouldNotSnapToTargetPos = true;
                break;
            }

            var d = player.TrueSpeed * FastMethods.GetFixedDeltaTimeFast() + 0.01f;

            var currentPos = player.Position;
            var currentDisp = currentPos - lastPos;

            if (currentPos.Distance(lastPos) < d * 0.5f) VHMovementCoeff -= FastMethods.GetDeltaTimeFast() * 3f;
            lastPos = currentPos;

            VVector2 currentGoal = path[currentTarget];
            VVector2 diff = currentGoal - player.TruePosition;


            VVector2 velocity = VVector2.Zero;

            if (diff.Magnitude < d * 0.7f)
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
            player.SetNormalizedVelocity(velocity.Normalized);

            if (diff.Magnitude < d)
            {
                currentTarget++;
                VHMovementCoeff = VHMovementInitCoeff;
            }

            float dispMagnitude = currentDisp.Magnitude;

            if (dispMagnitude < 0.005f)
                noMoveCount++;
            else
                noMoveCount = 0;
            if (noMoveCount > 80)
            {
                VVector2 snapTo = (currentGoal - (VVector2)player.GroundCollider.offset);
                player.Body.transform.position = snapTo.AsGameWorldUnityVector3();
                currentTarget++;
                continue;
            }

            foreach (var door in manualDoors)
            {
                if (door.Opening) continue;
                var doorPos = door.ModGameObject(false).Position;
                var distance = doorPos.Distance(currentPos);
                if (distance > (dispMagnitude < 0.01f ? 1.1f : 0.6f)) continue;
                var dir = (VVector2)doorPos - player.TruePosition;

                if (VVector2.Dot(dir, velocity.Normalized) > 0.25f || distance < (dispMagnitude < 0.01f ? 0.9f : 0.27f))
                {
                    player.Body.velocity = VVector2.Zero;
                    yield return CoInteractManualDoor(player, door);
                }
            }

            var ship = AmongUsLLImpl.ShipStatusInstance;

            foreach (var door in ship.AllDoors)
            {
                if (door.IsOpen) continue;
                var doorPos = door.ModGameObject(false).Position;
                var distance = doorPos.Distance(currentPos);
                if (distance > (dispMagnitude < 0.01f ? 1.1f : 0.6f)) continue;
                var dir = (VVector2)doorPos - player.TruePosition;

                if (VVector2.Dot(dir, velocity.Normalized) > 0.25f || distance < (dispMagnitude < 0.01f ? 0.9f : 0.27f))
                {
                    player.Body.velocity = new(0f, 0f);
                    yield return CoInteractDoor(player, door);
                }
            }

            foreach (var ladder in ship.Ladders)
            {
                var ladderPos = ladder.ModGameObject(false).Position;
                var distance = ladderPos.Distance(currentPos);

                if (distance > 0.8f) continue;
                VVector2 dir = (VVector2)ladder.Destination.ModGameObject(false).Position - player.TruePosition;

                //次のノードと梯子の行先はある程度近づける必要がある。
                if (ladder.Destination.ModGameObject(false).Position.Distance(currentGoal) > 1.2f) continue;

                if (VVector2.Dot(dir, velocity.Normalized) > 0.6f || (dispMagnitude < 0.01f && distance < 0.4f))
                {
                    player.Body.velocity = VVector2.Zero;
                    yield return player.UseLadder(ladder);
                    break;
                }
            }

            foreach (var zipline in ziplineConsoles)
            {
                var ziplinePos = zipline.ModGameObject(false).Position;
                var distance = ziplinePos.Distance(currentPos);

                if (distance > 3f) continue;

                var topDistance = zipline.zipline.dropPositionTop.ModGameObject(false).Position.Distance(currentPos);
                var bottomDistance = zipline.zipline.dropPositionBottom.ModGameObject(false).Position.Distance(currentPos);
                var targetTransform = topDistance > bottomDistance ? zipline.zipline.landingPositionTop : zipline.zipline.landingPositionBottom;
                //次のノードとジップラインの行先はある程度近づける必要がある。
                if (currentGoal.Distance(targetTransform.ModGameObject(false).Position) > 3f) continue;

                player.Body.velocity = VVector2.Zero;
                yield return player.UseZipline(zipline);
                break;
            }

            if (movingPlatform && stopCond.HasFlag(NavPathStopCondition.ChangeMovingPlatState))
            {
                if (movingPlatform.Target) RecalcPath(); //状態が変わったため経路を再計算する。
                else
                {
                    var mpDir = movingPlatform.IsLeft ? VVector2.Right : VVector2.Left;
                    if (VVector2.Dot(mpDir, velocity.Normalized) > 0.9f && movingPlatform.ModGameObject(false).Position.Distance(currentPos) < 1f)
                    {
                        Variable<bool> done = new();
                        yield return player.UseMovingPlatform(movingPlatform, done);
                        stopCond &= ~NavPathStopCondition.ChangeMovingPlatState;
                    }
                }
            }

            yield return null;
        }

        if (player.IsActive)
        {
            player.Body.velocity = VVector2.Zero;
            if (!player.Player.IsDead && !shouldNotSnapToTargetPos) player.SnapTo(path[^1] - player.GroundCollider.offset);
        }
    }

    static public NavPath? CalcPath(Vector2 from, Vector2 to, float radius = 0.15f)
    {
        if (Helpers.AnyCustomNonTriggersBetweenThick(from, to, radius, null, null, true))
        {
            MapData.GetCurrentMapData().MapNavData.GetPathfindingNode(from, to, radius, 8f, 3.2f, out var positions, out var nextNodes, out var conds);
            var indexPath = Pathfinding.FindPath(positions, nextNodes, positions.Length - 2, positions.Length - 1);
            if (indexPath.Length == 0) return null;

            NavPathStopCondition condFlag = NavPathStopCondition.None;
            int lastPos = -1;
            var pathArray = indexPath.Select(i =>
            {
                int nextPos = i;
                foreach (var c in conds) if (c.from == lastPos && c.to == nextPos) condFlag |= c.stopCond;
                lastPos = nextPos;
                return positions[i].ToUnityVector();
            }).ToArray();
            return new(pathArray, condFlag);
        }
        else
        {
            return new([from, to], NavPathStopCondition.None);
        }
    }
}

