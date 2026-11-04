using RobotSavaslari.Combat;
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
        [Tooltip("Bu hizin (m/s) altindaki temaslar carpma sayilmaz; surtunme ve itisme hasar vermez.")]
        [SerializeField, Min(0f)] private float minimumRamSpeed = 1f;
        [SerializeField] private float hitCooldown = 0.6f;

        [Header("Geri tepme")]
        [Tooltip("Carpilan robotun geri tepme hizi = carpma hizi x bu oran (agirlik oranina gore olceklenir).")]
        [SerializeField, Min(0f)] private float knockbackRatio = 0.5f;
        [Tooltip("Carpan robotun geri sekme hizi = carpma hizi x bu oran (agirlik oranina gore olceklenir).")]
        [SerializeField, Min(0f)] private float recoilRatio = 0.3f;
        [Tooltip("Geri tepme hizinin ust siniri (m/s).")]
        [SerializeField, Min(0f)] private float maxKnockbackSpeed = 2.5f;
        [Tooltip("Geri tepme sirasinda robotun kontrol edilemedigi sure (s).")]
        [SerializeField, Min(0f)] private float knockbackDuration = 0.3f;
        [Tooltip("Geri tepme sirasinda kalan yol tutusu (0 = buz, 1 = normal).")]
        [SerializeField, Range(0f, 1f)] private float knockbackTraction = 0.15f;

        private Rigidbody _body;
        private RobotHealth _health;
        private RobotDamageVisuals _damage;
        private Transform _weapon;
        private Collider[] _colliders;
        private float _boundaryRadius;
        public static RobotPrototypeController Selected { get; private set; }
        public bool IsSelected => Selected == this;
        private LineRenderer _selectionRing;
        private Material _ringMaterial;
        private float _nextHitTime;
        // Collision callbacks run after the solver has already changed velocities,
        // so the ram speed is taken from the velocity at the start of the physics step.
        private Vector3 _preStepVelocity;
        private float _knockbackUntil;
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
            _nextHitTime = _knockbackUntil = 0f;
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
            _damage = GetComponent<RobotDamageVisuals>();
            if (_damage == null) _damage = gameObject.AddComponent<RobotDamageVisuals>();
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
            _preStepVelocity = _body.linearVelocity;
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

            WeaponActive &= _damage.WeaponOperational;
            float traction = 1f;
            if (Time.time < _knockbackUntil)
            {
                // Let the recoil play out instead of traction cancelling it the next step.
                drive = 0f;
                steer = 0f;
                traction = knockbackTraction;
            }
            ApplyDrive(drive, steer, traction);

            if (_weapon != null && WeaponActive)
                _weapon.Rotate(Vector3.up, weaponSpinSpeed * Time.fixedDeltaTime, Space.Self);
        }

        private void ApplyDrive(float drive, float steer, float traction)
        {
            float dt = Time.fixedDeltaTime;
            float steering = Mathf.Clamp(steer + drive * _damage.SteeringPull, -1f, 1f);
            Quaternion heading = _body.rotation * Quaternion.Euler(0f, steering * turnSpeed * _damage.Turning * dt, 0f);
            // Use the physics heading, not the interpolated render transform.
            // A/D can pivot in place; steering remains responsive while reversing.
            _body.angularVelocity = Vector3.zero;
            _body.MoveRotation(heading);
            Vector3 forward = heading * Vector3.forward;
            Vector3 right = heading * Vector3.right;
            Vector3 velocity = _body.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float sidewaysSpeed = Vector3.Dot(velocity, right);
            float targetSpeed = drive * (drive < 0f ? reverseSpeed : maxSpeed) * _damage.Mobility;
            bool slowing = drive == 0f || forwardSpeed * drive < 0f;
            float nextForward = Mathf.MoveTowards(forwardSpeed, targetSpeed, (slowing ? braking : acceleration * _damage.Mobility) * traction * dt);
            float nextSideways = Mathf.MoveTowards(sidewaysSpeed, 0f, lateralGrip * traction * dt);
            // Bounded traction forces preserve collisions and pushing between robots.
            Vector3 correction = forward * (nextForward - forwardSpeed) + right * (nextSideways - sidewaysSpeed);
            _body.AddForce(correction / dt, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryRam(collision);
        }

        // Each robot deals only its own outgoing ram damage; the opponent's
        // OnCollisionEnter handles the reverse direction. A head-on crash
        // therefore damages both, while a robot standing still deals none.
        private void TryRam(Collision collision)
        {
            if (CombatStopped || _health.IsDefeated || collision.contactCount == 0) return;
            // One cooldown also stops compound colliders from multiplying a single hit.
            if (Time.time < _nextHitTime) return;
            var opponent = collision.collider.GetComponentInParent<RobotPrototypeController>();
            if (opponent == null || opponent == this || opponent._health.IsDefeated || opponent.CombatStopped) return;

            Vector3 direction = opponent._body.position - _body.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            // Only the part of our velocity that drives into the opponent counts.
            float impactSpeed = Vector3.Dot(_preStepVelocity, direction);
            if (impactSpeed < minimumRamSpeed) return;

            float damage = DamageCalculator.Calculate(AttackData.Ram(impactSpeed, _body.mass, opponent._body.mass));
            if (damage <= 0f) return;

            _nextHitTime = Time.time + hitCooldown;
            ContactPoint contact = collision.GetContact(0);
            Vector3 debrisImpulse = direction * Mathf.Clamp(impactSpeed * 0.5f + 1f, 1f, 7f);
            opponent._health.TakeDamage(damage, contact.point, contact.normal, debrisImpulse, false);

            // The hit robot is pushed away and the rammer bounces back, both scaled by impact speed.
            float pushed = KnockbackCalculator.Calculate(impactSpeed, opponent._body.mass, _body.mass, knockbackRatio, maxKnockbackSpeed);
            float recoil = KnockbackCalculator.Calculate(impactSpeed, _body.mass, opponent._body.mass, recoilRatio, maxKnockbackSpeed);
            opponent.ApplyKnockback(direction, pushed, knockbackDuration);
            ApplyKnockback(-direction, recoil, knockbackDuration);
        }

        // Sets (rather than adds) the velocity along the knockback direction, so the
        // result does not depend on how much the physics solver already exchanged.
        private void ApplyKnockback(Vector3 direction, float speed, float duration)
        {
            if (speed <= 0f || _health.IsDefeated || CombatStopped) return;
            Vector3 velocity = _body.linearVelocity;
            _body.linearVelocity = velocity + direction * (speed - Vector3.Dot(velocity, direction));
            _knockbackUntil = Mathf.Max(_knockbackUntil, Time.time + duration);
        }

        public void ResetMotion()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _preStepVelocity = Vector3.zero;
            _knockbackUntil = 0f;
        }
    }
}
