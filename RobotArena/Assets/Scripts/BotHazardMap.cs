using System.Collections.Generic;
using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari
{
    // Rebind after arena regeneration. Decision samples read only the visible phase.
    internal sealed class BotHazardMap
    {
        private const int Capacity = ArenaLayout.Columns * ArenaLayout.Columns;
        private readonly List<ArenaHazard> sources = new List<ArenaHazard>(Capacity);
        private readonly Zone[] zones = new Zone[Capacity];
        private readonly int[] observed = new int[Capacity];
        private int count, active;
        private float clearance;

        private struct Zone
        {
            internal ArenaHazard source;
            internal Vector2 center, axis, halfSize;
        }

        internal void Bind(ArenaEnvironment environment, ArenaBounds bounds)
        {
            count = active = 0;
            sources.Clear();
            if (bounds == null || environment == null || environment.Layout == null) return;
            var contents = environment.Contents;
            if (contents == null) return;
            contents.GetComponentsInChildren(false, sources);
            foreach (var cell in environment.Layout.cells)
            {
                if (cell == null || !cell.IsDamaging || count >= Capacity) continue;
                var center = BotNavigationSpace.Planar(bounds.CellCenter(cell.coordinate.x, cell.coordinate.y));
                foreach (var source in sources)
                {
                    if ((BotNavigationSpace.Planar(source.transform.position) - center).sqrMagnitude > .001f) continue;
                    // A sweep can traverse its marked strip. Its position within the next cycle is unknown.
                    var half = BotNavigationSpace.Planar(cell.HitSize) * .5f;
                    half.x += Mathf.Abs(cell.HitOffset(1).x - cell.HitOffset(0).x) * .5f;
                    zones[count++] = new Zone { source = source, center = center, axis = cell.direction.normalized, halfSize = half };
                    break;
                }
            }
        }

        internal void ClearObservation() => active = 0;

        internal bool Observe(float margin)
        {
            int previous = active;
            active = 0;
            bool changed = false;
            clearance = margin;
            for (int i = 0; i < count; i++)
            {
                var source = zones[i].source;
                if (source == null || !source.isActiveAndEnabled ||
                    (source.Phase != HazardPhase.Warning && source.Phase != HazardPhase.Active)) continue;
                if (active >= previous || observed[active] != i) changed = true;
                observed[active++] = i;
            }
            return changed || previous != active;
        }

        internal bool Crosses(Vector2 start, Vector2 end)
        {
            for (int i = 0; i < active; i++)
            {
                var zone = zones[observed[i]];
                Vector2 side = new Vector2(-zone.axis.y, zone.axis.x);
                Vector2 fromCenter = start - zone.center;
                Vector2 from = new Vector2(Vector2.Dot(fromCenter, zone.axis), Vector2.Dot(fromCenter, side));
                Vector2 delta = end - start;
                Vector2 step = new Vector2(Vector2.Dot(delta, zone.axis), Vector2.Dot(delta, side));
                Vector2 half = zone.halfSize + Vector2.one * clearance;
                float dx = half.x - Mathf.Abs(from.x), dy = half.y - Mathf.Abs(from.y);
                if (dx >= 0 && dy >= 0)
                {
                    // A signal may arrive while the robot is already inside. Permit outward progress.
                    if (step.sqrMagnitude > .000001f &&
                        ((from.x * step.x >= 0 && half.x - Mathf.Abs(from.x + step.x) < Mathf.Min(dx, dy)) ||
                         (from.y * step.y >= 0 && half.y - Mathf.Abs(from.y + step.y) < Mathf.Min(dx, dy)))) continue;
                    return true;
                }
                float first = 0, last = 1;
                if (Clip(from.x, step.x, half.x, ref first, ref last) &&
                    Clip(from.y, step.y, half.y, ref first, ref last)) return true;
            }
            return false;
        }

        private static bool Clip(float from, float step, float half, ref float first, ref float last)
        {
            if (Mathf.Abs(step) < .00001f) return Mathf.Abs(from) <= half;
            float a = (-half - from) / step, b = (half - from) / step;
            first = Mathf.Max(first, Mathf.Min(a, b));
            last = Mathf.Min(last, Mathf.Max(a, b));
            return first <= last;
        }
    }
}
