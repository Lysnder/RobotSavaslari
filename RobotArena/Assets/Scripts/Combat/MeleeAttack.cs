using System;
using UnityEngine;

namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Kisa menzilli tek silah saldirisi. Oyuncu girdisi, bot veya deneme bileseni
    /// ayni <see cref="RequestAttack"/> cagrisini kullanir; bilesen hicbir tusa bagli degildir.
    ///
    /// Kurallar:
    /// - Istek; saldiran yenikse, mac kapaliysa veya bekleme suruyorsa reddedilir.
    /// - Kabul edilen istek bekleme suresini hemen baslatir ve kisa bir saldiri penceresi acar.
    /// - Pencere boyunca menzil ve yay icindeki rakip collider'lar aranir. Collider'in ait oldugu
    ///   <see cref="RobotHealth"/> hedef sayilir; kendi robotu hedef olamaz.
    /// - Tek darbeler hedef başına bir kere, testere ise kesintisiz temasta sınırlı aralıklarla hasar verir.
    /// - Pencere sirasinda mac kapanirsa saldiri kesilir ve artik hasar yazilmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeAttack : MonoBehaviour
    {
        [Tooltip("Atanirsa hasar, menzil, bekleme, pencere ve yay degerleri bu profilden alinir.")]
        [SerializeField] private CombatProfile profile;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.05f)] private float range = 1.5f;
        [SerializeField, Min(0f)] private float cooldown = 1f;
        [SerializeField, Min(0f)] private float activeDuration = 0.15f;
        [SerializeField, Range(1f, 360f)] private float arcDegrees = 120f;
        [Tooltip("Saldirinin olculdugu nokta, robotun yerel uzayinda.")]
        [SerializeField] private Vector3 originOffset = new Vector3(0f, 0.35f, 0f);
        [SerializeField] private LayerMask targetLayers = ~0;
        [Tooltip("Sahnedeki mac anahtari. Kod ile Gate atanirsa o kullanilir.")]
        [SerializeField] private LocalMatchGate matchGate;

        private readonly AttackCooldown _cooldown = new AttackCooldown();
        private readonly AttackHitLog _hitLog = new AttackHitLog();
        private RobotHealth _health;
        private bool _initialized;
        private bool _active;
        private float _activeUntil, _strikeAt, _nextCut, _releaseAt;
        private int _cuts;
        private RobotHealth _sawContact;
        private FixedJoint _grip;
        private AudioSource _toolSpeaker;
        private AudioClip _toolCue;
        private WeaponStyle _cueStyle;
        private WeaponStyle Style => profile ? profile.style : WeaponStyle.Standard;
        public bool HoldingTarget => _grip != null;
        public bool WindingUp => _active && Now < _strikeAt;
        public float WindupFraction => !WindingUp ? 1f : 1f - (_strikeAt - Now) / profile.windup;
        public string PhaseLabel => HoldingTarget ? "Kavradı" : WindingUp ? (Style == WeaponStyle.Drum ? "Devir alıyor" : "Hazırlanıyor") :
            _active ? "Etkin" : CooldownRemaining > 0f ? $"Bekliyor  {CooldownRemaining:0.0} sn" : "Hazır";

        /// <summary>Kod ile atanan mac izni; null ise sahnedeki <see cref="LocalMatchGate"/> kullanilir, o da yoksa mac acik sayilir.</summary>
        public IMatchGate Gate { get; set; }

        /// <summary>Zaman kaynagi. Varsayilan Time.time; testler sabit zaman verebilir.</summary>
        public Func<float> Clock { get; set; }

        public float Damage => damage;
        public float Range => range;
        public float Cooldown => cooldown;
        public float ActiveDuration => activeDuration;
        public float ArcDegrees => arcDegrees;
        public CombatProfile Profile => profile;
        public bool IsAttacking => _active;
        public int TargetsHitThisAttack => _hitLog.Count;
        public AttackReport LastReport { get; private set; }
        public float CooldownRemaining => _cooldown.Remaining(Now);
        public Vector3 Origin => transform.TransformPoint(originOffset);

        /// <summary>Istek reddedildiginde veya saldiri bittiginde cagrilir.</summary>
        public event Action<MeleeAttack, AttackReport> Resolved;

        /// <summary>An accepted attack, before its first hit scan. Cooldown rejections do not fire it.</summary>
        public event Action<MeleeAttack> Started;

        /// <summary>Saldiri bir hedefe hasar yazdiginda cagrilir.</summary>
        public event Action<MeleeAttack, RobotHealth, float> TargetHit;

        private float Now => Clock != null ? Clock() : Time.time;

        private bool IsGateOpen
        {
            get
            {
                if (Gate != null) return Gate.IsOpen;
                return matchGate == null || matchGate.IsOpen;
            }
        }

        private bool IsSelfDefeated => _health != null && _health.IsDefeated;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            if (profile != null) CopyProfile(profile);
        }

        private void FixedUpdate()
        {
            Tick();
        }

        public void ApplyProfile(CombatProfile newProfile)
        {
            profile = newProfile;
            if (profile != null) CopyProfile(profile);
        }

        /// <summary>Tek saldiri girisi. Oyuncu, bot veya test ayni cagriyi kullanir.</summary>
        public AttackOutcome RequestAttack()
        {
            EnsureInitialized();
            float now = Now;
            if (IsSelfDefeated) return Reject(AttackOutcome.AttackerDefeated, now);
            if (!IsGateOpen) return Reject(AttackOutcome.MatchClosed, now);
            if (_active || !_cooldown.IsReady(now)) return Reject(AttackOutcome.Cooldown, now);

            _cooldown.Begin(now, cooldown);
            _hitLog.Clear();
            _active = true;
            _cuts = 0;
            _sawContact = null;
            _nextCut = now;
            _strikeAt = now + (profile ? Mathf.Max(0f, profile.windup) : 0f);
            _activeUntil = _strikeAt + activeDuration;
            PlayToolCue();
            LastReport = new AttackReport(AttackOutcome.Started, 0, now);
            Started?.Invoke(this);
            Tick();
            return AttackOutcome.Started;
        }

        /// <summary>Suren saldiri penceresini ilerletir. FixedUpdate cagirir; testler elle de cagirabilir.</summary>
        public void Tick()
        {
            float now = Now;
            if (_grip && (!_grip.connectedBody || now >= _releaseAt || !IsGateOpen || IsSelfDefeated)) ReleaseGrip();
            if (!_active) return;
            if (!IsGateOpen)
            {
                Finish(AttackOutcome.MatchClosed, now);
                return;
            }

            if (IsSelfDefeated)
            {
                Finish(AttackOutcome.AttackerDefeated, now);
                return;
            }

            if (now < _strikeAt) return;
            // A zero-duration profile samples once, synchronously in RequestAttack.
            // A late physics tick must not collect a hit after the window closed.
            if (now < _activeUntil || (activeDuration == 0f && now == _activeUntil))
                ScanTargets();
            if (_active && now >= _activeUntil)
                Finish(_hitLog.Count > 0 ? AttackOutcome.Hit : AttackOutcome.Miss, now);
        }

        /// <summary>Yeni mac: bekleme, suren saldiri ve hedef kayitlari baslangic durumuna doner.</summary>
        public void ResetForNewMatch()
        {
            EnsureInitialized();
            ReleaseGrip();
            _cooldown.Reset();
            _hitLog.Clear();
            _active = false;
            LastReport = default;
        }

        /// <summary>Collider bu saldirinin menzilinde ve yayinda mi? Temas noktasini da verir.</summary>
        public bool IsInReach(Collider target, out Vector3 point)
        {
            Vector3 origin = Origin;
            point = target.ClosestPoint(origin);
            if ((point - origin).sqrMagnitude > range * range + 1e-6f) return false;
            if (arcDegrees >= 360f) return true;

            Vector3 toTarget = point - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-6f) return true;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget) <= arcDegrees * 0.5f + 1e-3f;
        }

        private void ScanTargets()
        {
            Physics.SyncTransforms();
            Collider[] colliders = Physics.OverlapSphere(Origin, range, targetLayers, QueryTriggerInteraction.Ignore);
            bool stillCutting = false;
            foreach (Collider collider in colliders)
            {
                // Damage callbacks can end the match synchronously, even before this loop returns.
                if (!_active || !IsGateOpen || IsSelfDefeated) break;
                RobotHealth target = collider.GetComponentInParent<RobotHealth>();
                if (target == null || target == _health || target.IsDefeated) continue;
                if (!IsInReach(collider, out Vector3 point)) continue;
                if (Style == WeaponStyle.Saw)
                {
                    if (_sawContact != null && _sawContact != target) continue;
                    stillCutting = true;
                    if (Now < _nextCut || _cuts >= 4) continue;
                    _sawContact = target;
                    _cuts++;
                    _nextCut = Now + .18f;
                }
                else if (_hitLog.Contains(target)) continue;

                _hitLog.TryRegister(target);
                Vector3 direction = point - Origin;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : transform.forward;
                if (Style == WeaponStyle.Hammer)
                {
                    var hull = target.GetComponent<BoxCollider>();
                    if (hull) point = hull.ClosestPoint(new Vector3(point.x, hull.bounds.max.y + 1f, point.z));
                }
                target.TakeDamage(damage, point, Style == WeaponStyle.Hammer ? Vector3.up : -direction, direction * 2f, true, _health);
                if (IsGateOpen && !target.IsDefeated) ApplyToolEffect(target, direction);
                TargetHit?.Invoke(this, target, damage);
            }
            // An interrupted cut cannot resume until the next activation.
            if (_active && _sawContact != null && !stillCutting) Finish(AttackOutcome.Hit, Now);
        }

        private void ApplyToolEffect(RobotHealth target, Vector3 direction)
        {
            if (!profile) return;
            if (!target.TryGetComponent(out RobotPrototypeController robot)) return;
            if (profile.pushSpeed > 0f) robot.ApplyKnockback(direction, profile.pushSpeed * robot.WeaponPushMultiplier);
            if (!target.TryGetComponent(out Rigidbody body) || body.isKinematic) return;
            if (Style == WeaponStyle.Drum || Style == WeaponStyle.Flipper)
            {
                float lift = Style == WeaponStyle.Drum ? 1.35f : 3.05f;
                body.AddForce(Vector3.up * (lift * robot.WeaponPushMultiplier), ForceMode.VelocityChange);
            }
            else if (Style == WeaponStyle.Clamp && !_grip && TryGetComponent(out Rigidbody owner) && !owner.isKinematic)
            {
                _grip = gameObject.AddComponent<FixedJoint>();
                _grip.enableCollision = true;
                _grip.connectedBody = body;
                _releaseAt = Now + .64f;
            }
        }

        internal void ReleaseGrip()
        {
            if (!_grip) return;
            // Destroy takes effect at frame end. Zero break limits also release it on the next physics step.
            _grip.breakForce = 0f;
            _grip.breakTorque = 0f;
            Destroy(_grip);
            _grip = null;
        }

        private void PlayToolCue()
        {
            if (Style == WeaponStyle.Standard || Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!_toolSpeaker)
            {
                _toolSpeaker = gameObject.AddComponent<AudioSource>();
                _toolSpeaker.playOnAwake = false;
                _toolSpeaker.spatialBlend = .6f;
            }
            if (!_toolCue || _cueStyle != Style)
            {
                if (_toolCue) Destroy(_toolCue);
                _cueStyle = Style;
                _toolCue = ToolSound.Create(Style);
            }
            _toolSpeaker.PlayOneShot(_toolCue, .32f);
        }

        private void OnDisable() => ReleaseGrip();
        private void OnDestroy() { if (_toolCue) Destroy(_toolCue); }

        private AttackOutcome Reject(AttackOutcome outcome, float now)
        {
            var report = new AttackReport(outcome, 0, now);
            LastReport = report;
            Resolved?.Invoke(this, report);
            return outcome;
        }

        private void Finish(AttackOutcome outcome, float now)
        {
            _active = false;
            var report = new AttackReport(outcome, _hitLog.Count, now);
            LastReport = report;
            Resolved?.Invoke(this, report);
        }

        private void CopyProfile(CombatProfile source)
        {
            damage = source.damage;
            range = Mathf.Max(0.05f, source.range);
            cooldown = source.cooldown;
            activeDuration = source.activeDuration;
            arcDegrees = source.arcDegrees;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _health = GetComponent<RobotHealth>();
            if (profile != null) CopyProfile(profile);
        }
    }
}
