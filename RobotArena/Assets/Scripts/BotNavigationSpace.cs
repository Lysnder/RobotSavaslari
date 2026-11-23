using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari
{
    internal sealed class BotNavigationSpace
    {
        internal enum ProbeResult { Clear, Blocked, Budget }
        internal const int MaxQueries = 64;
        private const float Margin = .04f;
        private readonly Collider[] overlaps = new Collider[64];
        private readonly RaycastHit[] hits = new RaycastHit[64];
        internal readonly BotHazardMap Hazards = new BotHazardMap();
        private readonly Rigidbody body;
        private Transform opponent;
        private readonly Vector3 halfBox;
        private float floor, probeHeight;

        internal float Radius { get; }
        internal Vector2 Center { get; private set; }
        internal Vector2 Limits { get; private set; }
        internal int Queries { get; private set; }
        internal bool Saturated { get; private set; }

        internal BotNavigationSpace(Rigidbody body, Collider[] colliders, float arenaRadius)
        {
            this.body = body;
            float radius = Mathf.Max(.3f, arenaRadius);
            float bottom = body.position.y;
            float top = bottom + .45f;
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
                Bounds bounds = collider.bounds;
                Vector2 corner = Planar(bounds.center - body.position);
                corner = new Vector2(Mathf.Abs(corner.x) + bounds.extents.x, Mathf.Abs(corner.y) + bounds.extents.z);
                radius = Mathf.Max(radius, corner.magnitude);
                bottom = Mathf.Min(bottom, bounds.min.y);
                top = Mathf.Max(top, bounds.max.y);
            }
            Radius = radius + Margin;
            halfBox = new Vector3(Radius, Mathf.Max(.14f, (top - bottom - Margin) * .5f), Radius);
        }

        internal void BeginTick(ArenaBounds arena, Vector2 fallbackLimits, Transform target)
        {
            Queries = 0;
            Saturated = false;
            Center = arena != null ? arena.Center : Vector2.zero;
            Limits = arena != null ? arena.CenterLimits(Radius) : fallbackLimits;
            floor = arena != null ? arena.SurfaceHeight : body.position.y;
            probeHeight = floor + halfBox.y + Margin;
            var targetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            opponent = targetBody != null ? targetBody.transform : target;
        }

        internal Vector2 Clamp(Vector2 point) => Center + new Vector2(
            Mathf.Clamp(point.x - Center.x, -Limits.x, Limits.x),
            Mathf.Clamp(point.y - Center.y, -Limits.y, Limits.y));

        internal ProbeResult Probe(Vector2 from, Vector2 to, bool allowOpponent = true, int reserve = 2)
        {
            if (Saturated || (Clamp(to) - to).sqrMagnitude > .000001f) return ProbeResult.Blocked;
            Vector2 travel = to - from;
            bool segment = travel.sqrMagnitude > .000001f;
            if (Queries + (segment ? 2 : 1) > MaxQueries - reserve) return ProbeResult.Budget;
            Queries++;
            int count = Physics.OverlapBoxNonAlloc(AtHeight(to), halfBox, overlaps, Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return Saturate();
            for (int i = 0; i < count; i++) if (IsObstacle(overlaps[i], allowOpponent)) return ProbeResult.Blocked;
            if (Hazards.Crosses(from, to)) return ProbeResult.Blocked;
            if (!segment) return ProbeResult.Clear;

            Queries++;
            count = Physics.BoxCastNonAlloc(AtHeight(from), halfBox, new Vector3(travel.x, 0, travel.y).normalized,
                hits, Quaternion.identity, travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return Saturate();
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = hits[i].collider;
                if (!IsObstacle(obstacle, allowOpponent)) continue;
                // A robot already touching a wall may leave the overlap, never travel deeper into it.
                if (hits[i].distance == 0 && Vector2.Dot(travel, from - Planar(obstacle.bounds.center)) > 0) continue;
                return ProbeResult.Blocked;
            }
            return ProbeResult.Clear;
        }

        internal bool CanSeeOpponent(Vector2 from, Vector2 to)
        {
            Vector2 ray = to - from;
            if (ray.sqrMagnitude < .000001f) return true;
            if (Saturated || Queries >= MaxQueries - 2) return false;
            Queries++;
            int count = Physics.RaycastNonAlloc(AtHeight(from), new Vector3(ray.x, 0, ray.y).normalized,
                hits, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) { Saturate(); return false; }
            for (int i = 0; i < count; i++) if (IsObstacle(hits[i].collider, true)) return false;
            return true;
        }

        internal static Vector2 Planar(Vector3 point) => new Vector2(point.x, point.z);
        private Vector3 AtHeight(Vector2 point) => new Vector3(point.x, probeHeight, point.y);
        private ProbeResult Saturate() { Saturated = true; return ProbeResult.Blocked; }

        private bool IsObstacle(Collider collider, bool allowOpponent) => collider != null &&
            collider.attachedRigidbody != body && !collider.transform.IsChildOf(body.transform) &&
            (!allowOpponent || opponent == null || !collider.transform.IsChildOf(opponent)) &&
            collider.bounds.max.y > floor + Margin;
    }
}
