using System;
using UnityEngine;

namespace RobotSavaslari
{
    // Resumable A* search. Every edge, including the joins to the exact start and goal,
    // must accommodate the swept footprint. The caller rechecks edges before driving.
    internal sealed class BotRoutePlanner
    {
        internal const float GridStep = .5f;
        internal const int MaxCells = 1089;
        internal const int MaxWaypoints = 128;
        internal const int MaxExpansions = 128;

        internal enum Status { Idle, Searching, Found, Unreachable }

        private readonly float[] costs = new float[MaxCells];
        private readonly int[] parents = new int[MaxCells];
        private readonly byte[] visited = new byte[MaxCells];
        private readonly Vector2[] route = new Vector2[MaxWaypoints];
        private Vector2 origin, start;
        private int width, height, current, neighbor;

        internal Status State { get; private set; }
        internal Vector2 Goal { get; private set; }
        internal int Count { get; private set; }
        internal int Expanded { get; private set; }
        internal Vector2 this[int index] => route[index];

        internal void Clear()
        {
            State = Status.Idle;
            Count = Expanded = 0;
        }

        internal void Start(Vector2 position, Vector2 goal, Vector2 center, Vector2 limits)
        {
            Clear();
            Goal = goal;
            start = position;
            width = Mathf.FloorToInt(limits.x * 2 / GridStep) + 1;
            height = Mathf.FloorToInt(limits.y * 2 / GridStep) + 1;
            if (width < 2 || height < 2 || width > 33 || height > 33)
            {
                State = Status.Unreachable;
                return;
            }
            // Centre the grid instead of leaving the fractional remainder on just one wall.
            origin = center - new Vector2(width - 1, height - 1) * (GridStep * .5f);
            Array.Clear(visited, 0, width * height);
            for (int i = 0; i < width * height; i++) costs[i] = float.PositiveInfinity;
            current = -2; // virtual start node
            neighbor = 0;
            State = Status.Searching;
        }

        internal void Step(BotNavigationSpace space)
        {
            Expanded = 0;
            while (State == Status.Searching && Expanded < MaxExpansions)
            {
                if (current == -1)
                {
                    current = BestOpenCell();
                    if (current < 0) { State = Status.Unreachable; return; }
                    visited[current] = 2;
                    Expanded++;
                    neighbor = -1;
                }
                bool seed = current == -2;
                Vector2 from = seed ? start : Position(current);
                if (neighbor == -1)
                {
                    if ((Goal - from).sqrMagnitude <= 2.01f * GridStep * GridStep)
                    {
                        var join = space.Probe(from, Goal);
                        if (join == BotNavigationSpace.ProbeResult.Budget) return;
                        if (join == BotNavigationSpace.ProbeResult.Clear) { BuildRoute(); return; }
                    }
                    neighbor = 0;
                }
                int column = seed ? Mathf.Clamp(Mathf.RoundToInt((start.x - origin.x) / GridStep), 0, width - 1) : current % width;
                int row = seed ? Mathf.Clamp(Mathf.RoundToInt((start.y - origin.y) / GridStep), 0, height - 1) : current / width;
                while (neighbor < 9)
                {
                    int x = column + neighbor % 3 - 1, y = row + neighbor / 3 - 1;
                    if (x < 0 || x >= width || y < 0 || y >= height || (!seed && neighbor == 4))
                    { neighbor++; continue; }
                    int next = y * width + x;
                    Vector2 to = Position(next);
                    float cost = (seed ? 0 : costs[current]) + Vector2.Distance(from, to);
                    if (visited[next] == 2 || cost >= costs[next]) { neighbor++; continue; }
                    var link = space.Probe(from, to);
                    if (link == BotNavigationSpace.ProbeResult.Budget) return;
                    neighbor++;
                    if (link != BotNavigationSpace.ProbeResult.Clear) continue;
                    parents[next] = current;
                    costs[next] = cost;
                    visited[next] = 1;
                }
                current = -1;
            }
        }

        // The grid is small and the scan is bounded. No heap or per-search allocations.
        private int BestOpenCell()
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < width * height; i++)
            {
                if (visited[i] != 1) continue;
                float score = costs[i] + Vector2.Distance(Position(i), Goal);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        private Vector2 Position(int index) => origin + new Vector2(index % width, index / width) * GridStep;

        private void BuildRoute()
        {
            Count = 1;
            route[0] = Goal;
            for (int cell = current; cell >= 0; cell = parents[cell])
            {
                if (Count == MaxWaypoints) { Count = 0; State = Status.Unreachable; return; }
                route[Count++] = Position(cell);
            }
            Array.Reverse(route, 0, Count);
            State = Status.Found;
        }
    }
}
