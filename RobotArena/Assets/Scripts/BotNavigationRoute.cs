using UnityEngine;

namespace RobotSavaslari
{
    public enum BotRouteReason { None, Obstructed, SegmentBlocked, TargetMoved, NoProgress, Unreachable, QuerySaturated, HazardDetour, HazardWait, HazardEscape }

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
            bool risk = space.Hazards.Crosses(position, target);
            if (risk && space.Hazards.Crosses(target, target))
            {
                Invalidate(target, BotRouteReason.HazardWait);
                return false;
            }
            if (wait > 0) return false;
            var direct = space.Probe(position, target);
            if (direct == BotNavigationSpace.ProbeResult.Clear)
            {
                // Keep difficulty's sampled aim when it also fits. An aim offset near a wall
                // must not start a grid search if the real opponent is directly reachable.
                bool useAim = (aim - target).sqrMagnitude <= .0001f ||
                    space.Probe(position, aim) == BotNavigationSpace.ProbeResult.Clear;
                if (space.Saturated) { Invalidate(aim, BotRouteReason.QuerySaturated); return false; }
                planner.Clear();
                failures = 0;
                Reason = BotRouteReason.None;
                steering = useAim ? aim : target;
                return true;
            }
            if (space.Saturated) { Invalidate(target, BotRouteReason.QuerySaturated); return false; }
            if (direct == BotNavigationSpace.ProbeResult.Budget) return false;

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
                    if (progressTime >= 1.6f)
                    { Fail(point, BotRouteReason.NoProgress); return false; }
                    if (waypoint < planner.Count - 1 || distance > tolerance)
                    {
                        steering = Waypoint = point;
                        HasWaypoint = true;
                        return true;
                    }
                    // Target motion can change the end of a detour without making its
                    // current segment unsafe. Keep crossing the opening before replanning.
                    if ((target - planner.Goal).sqrMagnitude > .25f)
                        Invalidate(target, BotRouteReason.TargetMoved);
                    else { Fail(point, BotRouteReason.NoProgress); return false; }
                }
                else Invalidate(point, space.Saturated ? BotRouteReason.QuerySaturated : BotRouteReason.SegmentBlocked);
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
                if (risk || Reason == BotRouteReason.None)
                {
                    Reason = risk ? BotRouteReason.HazardDetour : BotRouteReason.Obstructed;
                    BlockedPoint = target;
                }
            }
            if (planner.State == BotRoutePlanner.Status.Searching)
            {
                planner.Step(space);
                Expanded = planner.Expanded;
            }
            if (space.Saturated) Invalidate(target, BotRouteReason.QuerySaturated);
            else if (planner.State == BotRoutePlanner.Status.Unreachable) Fail(target, risk ? BotRouteReason.HazardWait : BotRouteReason.Unreachable);
            return false;
        }

        internal void SetEscape(Vector2 exit)
        {
            if (Reason != BotRouteReason.HazardEscape) Invalidate(exit, BotRouteReason.HazardEscape);
            Waypoint = exit;
            HasWaypoint = true;
        }

        internal void RiskChanged()
        {
            // Resume a blocked crossing as soon as the observed warning clears.
            if (Reason == BotRouteReason.HazardWait || Reason == BotRouteReason.HazardDetour || Reason == BotRouteReason.HazardEscape)
            {
                retry = wait = 0;
                failures = 0;
                Invalidate(BlockedPoint, BotRouteReason.None);
            }
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
