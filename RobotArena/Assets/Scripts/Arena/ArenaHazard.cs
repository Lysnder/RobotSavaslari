using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    public sealed class ArenaHazard : MonoBehaviour
    {
        private readonly Collider[] overlaps = new Collider[64];
        private readonly RobotHealth[] targets = new RobotHealth[2];
        private readonly float[] nextHit = new float[2];
        private ArenaCellData cell;
        private MatchController match;

        public void Configure(MatchController controller, ArenaCellData data)
        {
            match = controller;
            cell = data;
            targets[0] = match.Player.GetComponent<RobotHealth>();
            targets[1] = match.Opponent.GetComponent<RobotHealth>();
            Vector2 axis = data.direction.normalized;
            transform.rotation = Quaternion.Euler(0f, -Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg, 0f);
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
