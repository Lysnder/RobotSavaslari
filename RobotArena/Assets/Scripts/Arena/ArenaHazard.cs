using RobotSavaslari.Match;
using Unity.Profiling;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    public enum HazardPhase { Idle, Warning, Active, Recovery }

    public sealed class ArenaHazard : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedUpdateMarker = new ProfilerMarker("ArenaHazard.FixedUpdate");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static System.Action<long> AllocationSample { get; set; }
#endif
        private readonly Collider[] overlaps = new Collider[64];
        private readonly RobotHealth[] targets = new RobotHealth[2];
        private readonly float[] nextHit = new float[2];
        private ArenaCellData cell;
        private MatchController match;

        private Transform marker;
        private float elapsed;
        public HazardPhase Phase { get; private set; }

        public void Configure(MatchController controller, ArenaCellData data, Material material)
        {
            match = controller;
            cell = data;
            targets[0] = match.Player.GetComponent<RobotHealth>();
            targets[1] = match.Opponent.GetComponent<RobotHealth>();
            Vector2 axis = data.direction.normalized;
            transform.rotation = Quaternion.Euler(0f, -Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg, 0f);
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = data.content + " floor marker";
            indicator.transform.SetParent(transform, false);
            indicator.GetComponent<Collider>().enabled = false;
            indicator.GetComponent<Renderer>().sharedMaterial = material;
            marker = indicator.transform;
            ResetCycle();
        }

        public void ResetCycle()
        {
            Phase = HazardPhase.Idle;
            elapsed = 0f;
            nextHit[0] = nextHit[1] = 0f;
            if (marker != null) DrawMarker();
        }

        private void OnDisable() => ResetCycle();

        private void FixedUpdate()
        {
            if (!CanDamage || cell == null) return;
            using (FixedUpdateMarker.Auto())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var sample = AllocationSample;
                long before = sample == null ? 0 : System.GC.GetAllocatedBytesForCurrentThread();
#endif
                AdvanceCycle();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (sample != null) sample(System.GC.GetAllocatedBytesForCurrentThread() - before);
#endif
            }
        }

        private void AdvanceCycle()
        {
            elapsed += Time.fixedDeltaTime;
            switch (Phase)
            {
                case HazardPhase.Idle:
                    if (elapsed >= 1f) Enter(HazardPhase.Warning);
                    break;
                case HazardPhase.Warning:
                    if (elapsed >= cell.warningSeconds)
                    {
                        Enter(HazardPhase.Active);
                        ApplyHits(0f);
                    }
                    break;
                case HazardPhase.Active:
                    ApplyHits(elapsed);
                    if (elapsed >= cell.activeSeconds) Enter(HazardPhase.Recovery);
                    break;
                case HazardPhase.Recovery:
                    if (elapsed >= cell.recoverySeconds) ResetCycle();
                    break;
            }
            DrawMarker();
        }

        private void Enter(HazardPhase phase)
        {
            Phase = phase;
            elapsed = 0f;
        }

        private void DrawMarker()
        {
            bool active = Phase == HazardPhase.Active;
            bool warning = Phase == HazardPhase.Warning;
            float progress = warning ? elapsed / cell.warningSeconds : elapsed / cell.activeSeconds;
            Vector3 size = cell.HitSize;
            float height = active ? .24f : warning ? .04f + .08f * Mathf.PingPong(progress * 4, 1) : .015f;
            Vector3 offset = cell.HitOffset(active || warning ? progress : 0);
            if (warning && cell.content == ArenaContent.Pulse)
                size *= Mathf.Lerp(.4f, 1f, progress);
            marker.localScale = new Vector3(size.x, height, size.z);
            marker.localPosition = new Vector3(offset.x, .01f + height * .5f, offset.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (cell == null) return;
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Phase == HazardPhase.Active ? Color.red : Phase == HazardPhase.Warning ? Color.yellow : Color.gray;
            Gizmos.DrawWireCube(cell.HitOffset(elapsed / cell.activeSeconds), cell.HitSize);
            Gizmos.matrix = previous;
        }

        private bool CanDamage => match != null && match.isActiveAndEnabled && !match.IsPaused &&
            match.State == MatchController.MatchState.Playing;

        private void ApplyHits(float elapsed)
        {
            Vector3 center = transform.TransformPoint(cell.HitOffset(elapsed / cell.activeSeconds));
            int count = Physics.OverlapBoxNonAlloc(center, cell.HitSize * .5f, overlaps, transform.rotation,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return;

            for (int i = 0; i < count && CanDamage; i++)
            {
                var health = overlaps[i].GetComponentInParent<RobotHealth>();
                if (health == null || health.IsDefeated) continue;
                int slot = health == targets[0] ? 0 : health == targets[1] ? 1 : -1;
                if (slot < 0 || elapsed < nextHit[slot]) continue;
                nextHit[slot] = cell.content == ArenaContent.Sweep ? float.PositiveInfinity : elapsed + cell.repeatSeconds;
                health.TakeDamage(cell.Damage, overlaps[i].ClosestPoint(center), Vector3.up, Vector3.zero, false, null);
            }
        }
    }
}
