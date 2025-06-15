using DiscordConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public static class Pathfinding
{
    private enum NodeState
    {
        None,
        Open,
        Closed,
    }

    private struct NodeInfo
    {
        public NodeState State;
        public float HeuristicDistance;
        public float Cost;
        public int Parent;
        /// <summary>
        /// この位置に到達する最短経路の長さ。
        /// 開始地点と終了地点が同じノードの場合、この長さは1になります。
        /// </summary>
        public int Length;
        public float Score => HeuristicDistance + Cost; 
        
        public void Initialize(float heuristicDistance) {
            this.HeuristicDistance = heuristicDistance;
            this.Cost = 10000f;
            this.Parent = -1;
            this.Length = -1;
            this.State = NodeState.None;
        }

        public bool Update(float cost, int parent, int length)
        {
            if (State == NodeState.Closed) return false;
            State = NodeState.Open;
            if (Cost > cost)
            {
                Cost = cost;
                this.Parent = parent;
                this.Length = length;
                return true;
            }
            return false;
        }

        public void Close() => State = NodeState.Closed;
    }

    /// <summary>
    /// 与えられたグラフと始点及び終点から、最短経路を求めます。
    /// </summary>
    /// <param name="positions"></param>
    /// <param name="nextNodes"></param>
    /// <param name="start"></param>
    /// <param name="goal"></param>
    /// <returns></returns>
    static public int[] FindPath(Virial.Compat.Vector2[] positions, int[][] nextNodes, int start, int goal)
    {
        NodeInfo[] info = new NodeInfo[positions.Length];
        for (int i = 0; i < positions.Length; i++) info[i].Initialize(positions[goal].Distance(positions[i]));

        PriorityQueue<int, float> nodeQueue = new PriorityQueue<int, float>();
        nodeQueue.Enqueue(start, -1f);
        info[start].Update(0, -1, 1);

        int[] CalcPath()
        {
            int[] path = new int[info[goal].Length];
            int current = goal;
            for(int i = path.Length - 1; i >= 0; i--)
            {
                path[i] = current;
                current = info[current].Parent;
            }
            return path;
        }

        if (start == goal) CalcPath();

        while (nodeQueue.Count > 0)
        {
            int target = nodeQueue.Dequeue();

            if (info[target].State == NodeState.Closed) continue;

            //周囲を更新し、必要に応じてキューに追加する。
            float targetCost = info[target].Cost;
            int targetLength = info[target].Length;
            for (int i = 0; i < nextNodes[target].Length; i++)
            {
                int next = nextNodes[target][i];
                if (info[next].Update(positions[target].Distance(positions[next]) + targetCost, target, targetLength + 1)){
                    if (next == goal) return CalcPath();
                    nodeQueue.Enqueue(next, info[next].Score);
                }
            }

            info[target].Close();
        }

        //経路が存在しない場合
        return [];
    }
}

