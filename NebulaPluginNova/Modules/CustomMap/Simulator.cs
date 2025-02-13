using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules.CustomMap;

public static class RoomSimulator
{
    /// <summary>
    /// 二つの接続点に注目し、部屋を配置できる範囲を求めます。
    /// </summary>
    /// <param name="roomDirection1Range"></param>
    /// <param name="roomDirection2Range"></param>
    /// <param name="target1Range"></param>
    /// <param name="target2Range"></param>
    /// <returns></returns>
    public static FloatVectorRange? CalcRoomRange(FloatVectorRange roomDirection1Range,  FloatVectorRange roomDirection2Range, FloatVectorRange target1Range, FloatVectorRange target2Range)
    {
        FloatVectorRange cand1 = new FloatVectorRange(
            new(target1Range.Max.x - roomDirection1Range.Min.x, target1Range.Max.y - roomDirection1Range.Min.y),
            new(target1Range.Min.x - roomDirection1Range.Max.x, target1Range.Max.y - roomDirection1Range.Min.y)
            );
        FloatVectorRange cand2 = new FloatVectorRange(
            new(target2Range.Max.x - roomDirection2Range.Min.x, target2Range.Max.y - roomDirection2Range.Min.y),
            new(target2Range.Min.x - roomDirection2Range.Max.x, target2Range.Max.y - roomDirection2Range.Min.y)
            );
        return cand1.GetOverlappedRange(cand2);
    }
    
}


public class SimulatedRoom
{
    public BlueprintRoom Room { get; private init; }
    public FloatVectorRange AvailableArea { get; private set; } = null!;

    private class SimulatedRoomEdge
    {
        public TerminalPoint Left { get; private init; }
        public TerminalPoint Right { get; private init; }
        public SimulatedRoomEdge(SimulatedRoom left, int leftIndex, SimulatedRoom right, int rightIndex)
        {
            Left = new(this, left, leftIndex, true);
            Right = new(this, right, rightIndex, false);
        }
        public class TerminalPoint
        {
            public SimulatedRoomEdge MyEdge { get; private init; }
            public TerminalPoint OtherPoint => IsLeft ? MyEdge.Right : MyEdge.Left;
            public SimulatedRoom Room { get; private init; }
            public int Index { get; private init; }
            public bool IsLeft { get; private init; }
            private FloatVectorRange MyVector => Room.Room.Connection[Index].Position;

            public TerminalPoint(SimulatedRoomEdge edge, SimulatedRoom room, int index, bool isLeft)
            {
                this.MyEdge = edge;
                this.Room = room;
                this.Index = index;
                this.IsLeft = isLeft;
                room.Edges[index] = this;
            }

            /// <summary>
            /// この端点の側の範囲を他方の端点に反映させます。
            /// </summary>
            public void ReflectTo()
            {
                OtherPoint.Room.AvailableAreaCache[OtherPoint.Index] = Room.AvailableArea.Add(MyVector).Sub(OtherPoint.MyVector);
                OtherPoint.Room.UpdateArea();
            }
        }

        /// <summary>
        /// この辺を消去します。
        /// 周囲の部屋の範囲は再計算されます。
        /// </summary>
        public void Erase()
        {
            Left.Room.Edges[Left.Index] = null;
            Left.Room.AvailableAreaCache[Left.Index] = null;

            Right.Room.Edges[Right.Index] = null;
            Right.Room.AvailableAreaCache[Right.Index] = null;

            Left.Room.UpdateArea();
            Right.Room.UpdateArea();
        }
    }

    private SimulatedRoomEdge.TerminalPoint?[] Edges { get; init; }
    public FloatVectorRange?[] AvailableAreaCache { get; private init; }
    public FloatVectorRange? Constraint { get; private init; }
    public FloatVectorRange GetFreeEdgeArea(int index) => AvailableArea.Add(Room.Connection[index].Position);
    public SimulatedRoom(BlueprintRoom room, FloatVectorRange constraiant)
    {
        Room = room;
        Constraint = constraiant;
        Edges = new SimulatedRoomEdge.TerminalPoint?[room.Connection.Length];
        AvailableAreaCache = new FloatVectorRange?[room.Connection.Length];

        UpdateArea();
    }

    /// <summary>
    /// 付け足す部屋のためのコンストラクタ
    /// 範囲の計算が未実行な点に注意してください。
    /// </summary>
    /// <param name="room"></param>
    /// <param name=""></param>
    private SimulatedRoom(BlueprintRoom room)
    {
        Room = room;
        Constraint = null;
        Edges = new SimulatedRoomEdge.TerminalPoint?[room.Connection.Length];
        AvailableAreaCache = new FloatVectorRange?[room.Connection.Length];
    }

    public SimulatedRoom TryConnect(int index, BlueprintRoom room, int targetIndex)
    {
        if (Edges[index] != null) Edges[index]!.MyEdge.Erase();

        SimulatedRoom targetSimulatedRoom = new(room);
        SimulatedRoomEdge edge = new(this, index, targetSimulatedRoom, targetIndex); //Edgesに辺が代入される。
        Edges[index]!.ReflectTo();

        return targetSimulatedRoom;
    }

    public void TryConnect(int index, SimulatedRoom room, int targetIndex)
    {
        if (Edges[index] != null) Edges[index]!.MyEdge.Erase();
        if (room.Edges[targetIndex] != null) room.Edges[targetIndex]!.MyEdge.Erase();

        SimulatedRoomEdge edge = new(this, index, room, targetIndex); //Edgesに辺が代入される。
        Edges[index]!.ReflectTo();
        room.Edges[index]!.ReflectTo();
    }

    private void UpdateArea()
    {
        var lastArea = AvailableArea;

        FloatVectorRange? area = Constraint;
        foreach(var edgeConst in AvailableAreaCache) {
            if (edgeConst == null) continue;

            if (area == null) area = edgeConst;
            else
            {
                area = area.GetOverlappedRange(edgeConst);
                if (area == null) Debug.Log("実行不可能なエリアが計算されました。");
            }
        }

        AvailableArea = area!;
        if (area == null) return; //例外

        //範囲が更新されたら変更を周囲へ波及させる
        if(lastArea == null || !area.Approximately(lastArea)) Edges.Do(edge => edge?.ReflectTo());
    }
}