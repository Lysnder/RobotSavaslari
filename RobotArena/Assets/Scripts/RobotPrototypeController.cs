using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RobotHealth))]
    public sealed class RobotPrototypeController : MonoBehaviour
    {
        [SerializeField] private float acceleration = 26f;
        [SerializeField] private float maxSpeed = 5.2f;
        [SerializeField] private float turnSpeed = 300f;
        [SerializeField] private float braking = 34f;
        [SerializeField] private float lateralGrip = 28f;
        [SerializeField] private float reverseSpeed = 4f;
        [SerializeField] private float weaponSpinSpeed = 900f;
        [SerializeField] private float hitDamage = 10f;
        [SerializeField] private float hitCooldown = 0.6f;

        private Rigidbody _body;
        private RobotHealth _health;
        private Transform _weapon;
        private Collider[] _colliders;
        private float _boundaryRadius;
        public static RobotPrototypeController Selected { get; private set; }
        public bool IsSelected => Selected == this;
        private LineRenderer _selectionRing;
        private Material _ringMaterial;
        private float _nextHitTime;
        private float _nextImpactTime;
        private RobotPrototypeController _opponent;
        public bool IsComputer { get; private set; }
        public bool CombatStopped { get; private set; }
        public bool WeaponActive { get; private set; }

        public void ConfigureCombat(bool computer, RobotPrototypeController opponent)
        {
            IsComputer = computer;
            _opponent = opponent;
            CombatStopped = false;
            WeaponActive = false;
            _nextHitTime = _nextImpactTime = 0f;
            var animator = GetComponent<RobotModelAnimator>();
            if (animator != null) animator.ResetPose();
        }

        public void StopCombat()
        {
            CombatStopped = true;
            WeaponActive = false;
            ResetMotion();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _health = GetComponent<RobotHealth>();
            _weapon = transform.Find("Top Spinner");
            _colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in _colliders)
            {
                Bounds b = collider.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3(x, 0, z)) - transform.position;
                        _boundaryRadius = Mathf.Max(_boundaryRadius, new Vector2(p.x, p.z).magnitude);
                    }
            }
            var ring = new GameObject("Selection Ring");
            ring.transform.SetParent(transform, false);
            _selectionRing = ring.AddComponent<LineRenderer>();
            _ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _ringMaterial.color = new Color(1f, 0.85f, 0.12f);
            _selectionRing.sharedMaterial = _ringMaterial;
            _selectionRing.useWorldSpace = false;
            _selectionRing.loop = true;
            _selectionRing.widthMultiplier = 0.045f;
            _selectionRing.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                _selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * _boundaryRadius, 0.06f, Mathf.Sin(angle) * _boundaryRadius));
            }
            _selectionRing.enabled = false;
        }

        public static void Select(RobotPrototypeController robot)
        {
            if (robot != null && robot.IsComputer) return;
            if (Selected == robot) return;
            if (Selected != null) Selected.ResetMotion();
            Selected = robot;
        }

        private void LateUpdate()
        {
            ConstrainToArena();
            _selectionRing.enabled = IsSelected;
        }

        private void OnDisable()
        {
            if (Selected == this) Selected = null;
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
        }

        // Inner wall faces are x=+/-5.91 and z=+/-4.91.
        // A rotation-independent footprint also protects corners while turning.
        public void ConstrainToArena()
        {
            if (_body == null) return;
            Vector3 p = _body.position;
            Vector3 v = _body.linearVelocity;
            float xLimit = Mathf.Max(0f, 5.89f - _boundaryRadius);
            float zLimit = Mathf.Max(0f, 4.89f - _boundaryRadius);
            float x = Mathf.Clamp(p.x, -xLimit, xLimit);
            float z = Mathf.Clamp(p.z, -zLimit, zLimit);
            if ((p.x >= xLimit && v.x > 0f) || (p.x <= -xLimit && v.x < 0f)) v.x = 0f;
            if ((p.z >= zLimit && v.z > 0f) || (p.z <= -zLimit && v.z < 0f)) v.z = 0f;
            if (x != p.x || z != p.z) _body.position = new Vector3(x, p.y, z);
            _body.linearVelocity = v;
        }

        private void FixedUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            ConstrainToArena();
            WeaponActive = false;
            if (CombatStopped || _health.IsDefeated)
            {
                ResetMotion();
                return;
            }

            float drive = 0f;
            float steer = 0f;
            if (IsComputer)
            {
                if (_opponent == null || _opponent._health.IsDefeated) return;
                Vector3 direction = _opponent._body.position - _body.position;
                direction.y = 0f;
                float angle = Vector3.SignedAngle(_body.rotation * Vector3.forward, direction, Vector3.up);
                steer = Mathf.Clamp(angle / 35f, -1f, 1f);
                drive = Mathf.Abs(angle) < 65f ? 0.85f : 0f;
                WeaponActive = direction.sqrMagnitude < 9f;
            }
            else if (keyboard != null)
            {
                drive = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                          (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                steer = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                          (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                WeaponActive = keyboard.spaceKey.isPressed;
            }

            ApplyDrive(drive, steer);

            if (_weapon != null && WeaponActive)
                _weapon.Rotate(Vector3.up, weaponSpinSpeed * Time.fixedDeltaTime, Space.Self);
        }

        private void ApplyDrive(float drive, float steer)
        {
            float dt = Time.fixedDeltaTime;
            Quaternion heading = _body.rotation * Quaternion.Euler(0f, steer * turnSpeed * dt, 0f);
            // Use the physics heading, not the interpolated render transform.
            // A/D can pivot in place; steering remains responsive while reversing.
            _body.angularVelocity = Vector3.zero;
            _body.MoveRotation(heading);
            Vector3 forward = heading * Vector3.forward;
            Vector3 right = heading * Vector3.right;
            Vector3 velocity = _body.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float sidewaysSpeed = Vector3.Dot(velocity, right);
            float targetSpeed = drive * (drive < 0f ? reverseSpeed : maxSpeed);
            bool slowing = drive == 0f || forwardSpeed * drive < 0f;
            float nextForward = Mathf.MoveTowards(forwardSpeed, targetSpeed, (slowing ? braking : acceleration) * dt);
            float nextSideways = Mathf.MoveTowards(sidewaysSpeed, 0f, lateralGrip * dt);
            // Bounded traction forces preserve collisions and pushing between robots.
            Vector3 correction = forward * (nextForward - forwardSpeed) + right * (nextSideways - sidewaysSpeed);
            _body.AddForce(correction / dt, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            DealContactDamage(collision, true);
        }

        private void OnCollisionStay(Collision collision)
        {
            DealContactDamage(collision, false);
        }

        private void DealContactDamage(Collision collision, bool impact)
        {
            if (CombatStopped || _health.IsDefeated) return;
            var opponent = collision.collider.GetComponentInParent<RobotPrototypeController>();
            if (opponent == null || opponent == this || opponent._health.IsDefeated || opponent.CombatStopped) return;
            // A moving body impact hurts even when its weapon is not running.
            // One shared cooldown prevents compound colliders from multiplying damage.
            bool ram = impact && collision.relativeVelocity.sqrMagnitude >= 1f && Time.time >= _nextImpactTime;
            // Combat is independent of the visual model: imported robots may
            // contain their weapon in one combined mesh rather than a separate spinner.
            bool weapon = WeaponActive;
            if ((!ram && !weapon) || Time.time < _nextHitTime) return;
            _nextHitTime = Time.time + hitCooldown;
            _nextImpactTime = Time.time + hitCooldown;
            opponent._health.TakeDamage(weapon ? hitDamage : hitDamage * 0.5f);
        }

        public void ResetMotion()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }
}
