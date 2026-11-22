using UnityEngine;

namespace RobotSavaslari
{
    public enum BotRouteReason { None, Obstructed, SegmentBlocked, TargetMoved, NoProgress, Unreachable, QuerySaturated }

    internal sealed class BotNavigationRoute
    {
        private const float RetryInterval = .5f;
        private readonly BotRoutePlanner planner = new BotRoutePlanner();
        private float retry, wait, progressTime, bestDistance;
        private int waypoint, failures;
        private Vector2 center, limits;
        private bool hasBounds;

        internal BotRouteReason Reason { get; private set; }
        internal int Plans { get; private set; }
        internal int Expanded { get; private set; }
        internal bool Detouring => planner.State == BotRoutePlanner.Status.Found;
        internal bool HasWaypoint { get; private set; }
        internal Vector2 Waypoint { get; private set; }
        internal Vector2 BlockedPoint { get; private set; }
        internal int Count => planner.Count;
        internal Vector2 Point(int index) => planner[index];

        internal void Reset()
        {
            planner.Clear();
            retry = wait = progressTime = 0;
            bestDistance = float.PositiveInfinity;
            waypoint = Plans = Expanded = failures = 0;
            Reason = BotRouteReason.None;
            HasWaypoint = hasBounds = false;
        }

        internal void BeginTick(float dt, BotNavigationSpace space)
        {
            if (hasBounds && (space.Center != center || space.Limits != limits)) Reset();
            center = space.Center;
            limits = space.Limits;
            hasBounds = true;
            retry = Mathf.Max(0, retry - dt);
            wait = Mathf.Max(0, wait - dt);
            HasWaypoint = false;
            Expanded = 0;
        }

        internal bool TrySteer(BotNavigationSpace space, Vector2 position, Vector2 aim, Vector2 target,
            Vector2 motorVelocity, Vector2 conveyorVelocity, float dt, out Vector2 steering)
        {
            steering = position;
            if (wait > 0) return false;
            var direct = space.Probe(position, aim);
            if (direct == BotNavigationSpace.ProbeResult.Clear)
            {
                planner.Clear();
                failures = 0;
                Reason = BotRouteReason.None;
                steering = aim;
                return true;
            }
            if (space.Saturated) { Invalidate(aim, BotRouteReason.QuerySaturated); return false; }
            if (direct == BotNavigationSpace.ProbeResult.Budget) return false;

            if (planner.State != BotRoutePlanner.Status.Idle && (planner.Goal - target).sqrMagnitude > 1f && retry <= 0)
                Invalidate(target, BotRouteReason.TargetMoved);

            if (planner.State == BotRoutePlanner.Status.Found)
            {
                Vector2 point = planner[waypoint];
                float distance = Vector2.Distance(position, point);
                // Physical velocity includes the surface contribution. Only the along-route
                // component changes the tolerance, so lateral drift cannot skip a corner.
                float along = Vector2.Dot(motorVelocity + conveyorVelocity, (point - position).normalized);
                float tolerance = Mathf.Clamp(.19f + along * dt, .14f, .32f);
                if (distance <= tolerance && waypoint < planner.Count - 1)
                { waypoint++; bestDistance = float.PositiveInfinity; progressTime = 0; }

                point = planner[waypoint];
                var segment = space.Probe(position, point);
                if (segment == BotNavigationSpace.ProbeResult.Budget) return false;
                if (segment == BotNavigationSpace.ProbeResult.Clear)
                {
                    // Only skip waypoints when the complete shortcut is clear this tick.
                    for (int skip = 0; skip < 3 && waypoint + 1 < planner.Count; skip++)
                    {
                        Vector2 next = planner[waypoint + 1];
                        if ((next - position).sqrMagnitude > 1.35f * 1.35f ||
                            space.Probe(position, next) != BotNavigationSpace.ProbeResult.Clear) break;
                        point = next;
                        waypoint++;
                        bestDistance = float.PositiveInfinity;
                        progressTime = 0;
                    }
                    if (space.Saturated) { Invalidate(point, BotRouteReason.QuerySaturated); return false; }
                    distance = Vector2.Distance(position, point);
                    if (distance < bestDistance - .07f) { bestDistance = distance; progressTime = 0; }
                    else progressTime += dt;
                    if (progressTime >= 1.6f || (waypoint == planner.Count - 1 && distance <= tolerance))
                    { Fail(point, BotRouteReason.NoProgress); return false; }
                    steering = Waypoint = point;
                    HasWaypoint = true;
                    return true;
                }
                Invalidate(point, space.Saturated ? BotRouteReason.QuerySaturated : BotRouteReason.SegmentBlocked);
                if (space.Saturated) return false;
            }

            if (planner.State == BotRoutePlanner.Status.Idle && retry <= 0)
            {
                planner.Start(position, target, center, limits);
                retry = RetryInterval;
                waypoint = 0;
                bestDistance = float.PositiveInfinity;
                progressTime = 0;
                Plans++;
                if (Reason == BotRouteReason.None) { Reason = BotRouteReason.Obstructed; BlockedPoint = aim; }
            }
            if (planner.State == BotRoutePlanner.Status.Searching)
            {
                planner.Step(space);
                Expanded = planner.Expanded;
            }
            if (space.Saturated) Invalidate(aim, BotRouteReason.QuerySaturated);
            else if (planner.State == BotRoutePlanner.Status.Unreachable) Fail(target, BotRouteReason.Unreachable);
            return false;
        }

        internal void Invalidate(Vector2 point, BotRouteReason reason)
        {
            planner.Clear();
            HasWaypoint = false;
            Reason = reason;
            BlockedPoint = point;
        }

        private void Fail(Vector2 point, BotRouteReason reason)
        {
            Invalidate(point, reason);
            if (++failures < 3) return;
            failures = 0;
            wait = 2f;
        }
    }
}
