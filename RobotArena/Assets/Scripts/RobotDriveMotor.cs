using UnityEngine;

namespace RobotSavaslari
{
    // The only component which applies traction. Input sources submit one fixed-step command.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RobotDriveMotor : MonoBehaviour
    {
        public float acceleration = 26f, maxSpeed = 5.2f, turnSpeed = 300f;
        public float braking = 34f, lateralGrip = 28f, reverseSpeed = 4f;
        public Vector2 LastCommand { get; private set; }
        public Rigidbody Body { get; private set; }
        private Vector2 _pending;
        private RobotDamageVisuals _damage;
        private void Awake() { Body = GetComponent<Rigidbody>(); _damage = GetComponent<RobotDamageVisuals>(); }
        public void Submit(float drive, float steer) { _pending = new Vector2(Mathf.Clamp(drive, -1, 1), Mathf.Clamp(steer, -1, 1)); }
        public void Clear(bool stopImmediately = false)
        {
            _pending = LastCommand = Vector2.zero;
            if (stopImmediately && Body != null) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
        }
        private void OnDisable() { Clear(true); }
        private void FixedUpdate() { Step(Time.fixedDeltaTime); }
        public void Step(float dt)
        {
            LastCommand = _pending;
            _pending = Vector2.zero; // No producer this step means braking, never stale input.
            if (_damage == null) _damage = GetComponent<RobotDamageVisuals>();
            float mobility = _damage != null ? _damage.Mobility : 1f;
            float turning = _damage != null ? _damage.Turning : 1f;
            float pull = _damage != null ? _damage.SteeringPull : 0f;
            float drive = LastCommand.x, steer = LastCommand.y;
            Quaternion heading = Body.rotation * Quaternion.Euler(0, Mathf.Clamp(steer + drive * pull, -1, 1) * turnSpeed * turning * dt, 0);
            Body.angularVelocity = Vector3.zero;
            Body.MoveRotation(heading);
            Vector3 forward = heading * Vector3.forward, right = heading * Vector3.right;
            float speed = Vector3.Dot(Body.linearVelocity, forward), side = Vector3.Dot(Body.linearVelocity, right);
            float desired = drive * (drive < 0 ? reverseSpeed : maxSpeed) * mobility;
            float next = Mathf.MoveTowards(speed, desired, (drive == 0 || speed * drive < 0 ? braking : acceleration * mobility) * dt);
            float nextSide = Mathf.MoveTowards(side, 0, lateralGrip * dt);
            Body.AddForce((forward * (next - speed) + right * (nextSide - side)) / dt, ForceMode.Acceleration);
        }
    }
}
