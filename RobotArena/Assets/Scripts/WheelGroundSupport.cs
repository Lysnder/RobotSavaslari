using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari
{
    // The stock contact box supports the intact, level chassis. After a break the lower
    // portion becomes suspension travel and the same box becomes the belly skid.
    internal sealed class WheelGroundSupport
    {
        private const float Sag = .065f;
        private const float ProbeHeight = .34f;
        private const RigidbodyConstraints Tilt = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        private readonly RobotDamageVisuals.Part[] wheels = new RobotDamageVisuals.Part[4];
        private readonly Vector3[] soles = new Vector3[4];
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private readonly Rigidbody body;
        private readonly BoxCollider hull;
        private readonly Vector3 fullSize, fullCenter;
        private RigidbodyConstraints originalTilt;
        private bool damaged;
        private int count;

        public float Drive { get; private set; } = 1f;
        public float Turn { get; private set; } = 1f;
        public float Brake { get; private set; } = 1f;
        public float Pull { get; private set; }

        public WheelGroundSupport(Rigidbody body, BoxCollider hull)
        {
            this.body = body;
            this.hull = hull;
            fullSize = hull.size;
            fullCenter = hull.center;
        }

        public void Bind(IReadOnlyList<RobotDamageVisuals.Part> parts)
        {
            Repair();
            count = 0;
            foreach (var part in parts)
            {
                if (part.Kind != RobotDamageVisuals.PartKind.Wheel || count >= wheels.Length) continue;
                wheels[count] = part;
                Vector3 sole = body.transform.InverseTransformPoint(part.Transform.position);
                sole.y = fullCenter.y - fullSize.y * .5f;
                soles[count++] = sole;
            }
        }

        public void ReleaseCorner()
        {
            if (count != 4) return;
            if (!damaged)
            {
                damaged = true;
                originalTilt = body.constraints & Tilt;
                body.constraints &= ~Tilt;
                float clearance = Mathf.Min(.135f, fullSize.y * .22f);
                hull.size = fullSize - Vector3.up * clearance;
                hull.center = fullCenter + Vector3.up * (clearance * .5f);
                body.WakeUp();
            }
            Step(false);
        }

        public void Repair()
        {
            if (damaged)
            {
                hull.size = fullSize;
                hull.center = fullCenter;
                body.constraints = (body.constraints & ~Tilt) | originalTilt;
                damaged = false;
            }
            Drive = Turn = Brake = 1f;
            Pull = 0f;
        }

        public void Step(bool applySupport = true)
        {
            if (count != 4) return;
            float drive = 0f, turn = 0f, brake = 0f, pull = 0f;
            for (int i = 0; i < count; i++)
            {
                if (wheels[i].Detached) continue;
                Vector3 point = body.position + body.rotation * soles[i];
                if (!TryGround(point, out var hit)) continue;
                bool front = soles[i].z >= fullCenter.z;
                drive += front ? .21f : .29f;
                turn += front ? .29f : .21f;
                brake += front ? .31f : .19f;
                pull -= Mathf.Sign(soles[i].x - fullCenter.x) * (front ? .10f : .15f);
                if (!damaged || !applySupport || body.isKinematic) continue;

                // At the original ride height, one spring carries one quarter of the weight.
                // A remaining wheel can compress under extra load until the hull touches down.
                float compression = ProbeHeight + Sag - hit.distance;
                Vector3 floorVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
                float verticalSpeed = Vector3.Dot(body.GetPointVelocity(point) - floorVelocity, Vector3.up);
                float gravity = Mathf.Abs(Physics.gravity.y);
                float support = Mathf.Clamp(gravity * compression / (4f * Sag) - 5.5f * verticalSpeed, 0f, gravity * 1.5f);
                body.AddForceAtPosition(Vector3.up * (support * body.mass), point, ForceMode.Force);
            }
            Drive = drive;
            Turn = turn;
            Brake = brake;
            Pull = pull;
        }

        private bool TryGround(Vector3 sole, out RaycastHit ground)
        {
            // A loose wheel still collides with the chassis, but cannot act as an anchored spring.
            int hits = Physics.RaycastNonAlloc(sole + Vector3.up * ProbeHeight, Vector3.down, groundHits,
                ProbeHeight + Sag, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ground = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                var hit = groundHits[i];
                if (hit.distance >= nearest || hit.normal.y < .55f ||
                    (hit.rigidbody != null && (!hit.rigidbody.isKinematic || hit.rigidbody == body))) continue;
                ground = hit;
                nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
