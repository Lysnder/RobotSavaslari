using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari
{
    [DefaultExecutionOrder(20)]
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
        private RobotDriveMotor _motor;
        private RobotBotController _bot;
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
        private float _nextImpactTime;
        public bool IsComputer { get; private set; }
        public bool CombatStopped { get; private set; }
        public bool WeaponActive { get; private set; }

        public void ConfigureCombat(bool computer, RobotPrototypeController opponent)
        {
            IsComputer = computer;
            CombatStopped = false;
            if (_motor == null) _motor = GetComponent<RobotDriveMotor>() ?? gameObject.AddComponent<RobotDriveMotor>();
            if (_bot == null) _bot = GetComponent<RobotBotController>();
            if (computer)
            {
                _bot = GetComponent<RobotBotController>();
                if (_bot == null)
                {
                    _bot = gameObject.AddComponent<RobotBotController>();
                    // Arena bodies touch near 1.65m; request before contact, stop inside weapon range.
                    _bot.attackEnterDistance = 1.85f;
                    _bot.followResumeDistance = 2.3f;
                    _bot.attackStopInset = 0.3f;
                    _bot.enableCombatTactics = true;
                }
                _bot.enabled = isActiveAndEnabled;
                _bot.Target = opponent != null ? opponent.transform : null;
                _bot.SetMatchRunning(isActiveAndEnabled);
            }
            else if (_bot != null) { _bot.SetMatchRunning(false); _bot.enabled = false; }
            WeaponActive = false;
            _nextHitTime = _nextImpactTime = 0f;
            var animator = GetComponent<RobotModelAnimator>();
            if (animator != null) animator.ResetPose();
        }

        public void StopCombat()
        {
            CombatStopped = true;
            if (_bot != null) _bot.SetMatchRunning(false);
            WeaponActive = false;
            ResetMotion();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _motor = GetComponent<RobotDriveMotor>() ?? gameObject.AddComponent<RobotDriveMotor>();
            _motor.acceleration = acceleration; _motor.maxSpeed = maxSpeed;
            _motor.turnSpeed = turnSpeed; _motor.braking = braking;
            _motor.lateralGrip = lateralGrip; _motor.reverseSpeed = reverseSpeed;
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
            if (_bot != null) { _bot.SetMatchRunning(false); _bot.enabled = false; }
            if (_motor != null) _motor.Clear(true);
        }

        private void OnEnable()
        {
            if (_bot != null && IsComputer && !CombatStopped)
            {
                _bot.enabled = true;
                _bot.SetMatchRunning(true);
            }
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
                // Bot owns steering; combat consumes intent and retains its own damage cooldown.
                WeaponActive = _bot != null && _bot.enabled && _bot.AttackRequested;
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
            if (!IsComputer) _motor.Submit(drive, steer);

            if (_weapon != null && WeaponActive)
                _weapon.Rotate(Vector3.up, weaponSpinSpeed * Time.fixedDeltaTime, Space.Self);
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
            if (CombatStopped || _health.IsDefeated || collision.contactCount == 0) return;
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
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = opponent.transform.position - transform.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            float speed = Mathf.Clamp(collision.relativeVelocity.magnitude / 3f, 0.5f, 1.6f);
            float damage = weapon ? hitDamage : hitDamage * 0.5f * speed;
            opponent._health.TakeDamage(damage, contact.point, contact.normal,
                direction * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.5f + (weapon ? 3f : 1f), 1f, 7f), weapon);
        }

        public void ResetMotion()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            if (_motor != null) _motor.Clear(true);
        }
    }
}
