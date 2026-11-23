using System;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using Unity.Profiling;
using UnityEngine;

namespace RobotSavaslari
{
    [RequireComponent(typeof(RobotDriveMotor))]
    public sealed class RobotBotController : MonoBehaviour
    {
        public enum Decision { Idle, Turning, Following, Attacking, BackingOut, EscapeTurn, EscapeDrive, Disengaging, Repositioning, Waiting, Evading, Navigating }
        [SerializeField] private Transform target;
        [Header("Opponent decisions")]
        public BotPersonality personality = BotPersonality.Aggressive;
        public BotDifficulty difficulty = BotDifficulty.Hard;
        public int decisionSeed = 740;
        public BotAttackMemory Memory { get; } = new BotAttackMemory();
        public BotThreatObservation Threat { get; private set; }
        public BotDamageState OwnCondition { get; private set; }
        public BotDamageState TargetCondition { get; private set; }
        public float AimError { get; private set; }
        public int DecisionSamples { get; private set; }
        public int EvasionCount { get; private set; }
        public Vector3 EvasionPoint { get; private set; }
        public float DefenseSide { get; private set; }
        public float EffectiveReactionSeconds => Mathf.Max(0.02f, reactionSeconds) * new BotDecisionProfile(difficulty).ReactionMultiplier;
        [Header("Tracking and attack (centre-to-centre metres)")]
        [Min(0.1f)] public float attackEnterDistance = 1.65f;
        [Min(0.1f)] public float followResumeDistance = 2.1f;
        [Min(0)] public float attackStopInset = 0.15f;
        [Range(1, 89)] public float attackAngle = 22f;
        [Range(1, 89)] public float driveAngle = 55f;
        [Min(1)] public float fullSteerAngle = 40f;
        [Header("Recovery")]
        [Min(0.1f)] public float stuckSeconds = 0.9f;
        [Min(0.01f)] public float minimumProgress = 0.12f;
        [Min(0.1f)] public float reverseSeconds = 0.65f;
        [Min(0.1f)] public float escapeTurnSeconds = 0.4f;
        [Min(0.1f)] public float escapeDriveSeconds = 0.85f;
        [Header("Combat rhythm (Arena; off in the H1-H4 motor lab)")]
        public bool enableCombatTactics;
        [Range(0.1f, 1)] public float pursuitDrive = 0.62f;
        [Range(0.1f, 1)] public float maximumSteer = 0.7f;
        [Min(0.02f)] public float reactionSeconds = 0.16f;
        [Min(0.1f)] public float attackBurstSeconds = 1.05f;
        [Min(0.1f)] public float circleTimeout = 2.4f;
        [Min(0.1f)] public float disengageSeconds = 0.65f;
        [Min(0.1f)] public float repositionSeconds = 1.25f;
        [Min(0.1f)] public float openingSeconds = 0.55f;
        [Tooltip("Safe centre limits for Arena robots; waypoint coordinates are in world XZ.")]
        public Vector2 tacticalHalfExtents = new Vector2(4.25f, 3.25f);
        [SerializeField] private bool drawNavigationGizmos;
        public int NavigationPlans => _route.Plans;
        public int NavigationQueries => _space?.Queries ?? 0;
        public int NavigationExpansions => _route.Expanded;
        public BotRouteReason NavigationReason => _route.Reason;
        public Vector3 NavigationWaypoint => AtBodyHeight(_route.Waypoint);
        public bool MatchRunning { get; private set; } = true;
        public Decision State { get; private set; }
        public float Distance { get; private set; } = float.PositiveInfinity;
        public float TargetAngle { get; private set; }
        public Vector2 Command { get; private set; }
        public bool AttackRequested { get; private set; }
        public int RecoveryCount { get; private set; }
        public int RepositionCount { get; private set; }
        // Intent only; subscriber owns damage, weapon operation and cooldown.
        public event Action<RobotBotController, Transform> AttackRequest;
        private RobotDriveMotor _motor;
        private Transform _previousTarget;
        private Vector3 _anchor;
        private float _stuckTime, _maneuverTime, _escapeSign = 1;
        private bool _attackHold;
        private float _pressureTime, _circleTime, _spinTime, _tacticTime, _graceTime;
        private float _bestDistance, _previousTargetYaw, _lastTargetHealth, _noDamageTime, _reactionTime;
        private bool _engaged, _tacticsObserved;
        private Vector3 _retreatPoint, _flankPoint, _aimPosition;
        private float _flankSide = 1;
        private System.Random _decisionRandom;
        private RobotHealth _ownHealth;
        private RobotDamageVisuals _ownDamage;
        private float _evasionTime, _evasionCooldown, _profileCooldown;
        private bool _sampleDecision;
        private readonly Collider[] _obstacles = new Collider[64];
        private readonly RaycastHit[] _pathHits = new RaycastHit[64];
        private Collider[] _ownColliders;
        private readonly BotNavigationRoute _route = new BotNavigationRoute();
        private static readonly ProfilerMarker NavigationSample = new ProfilerMarker("RobotBot.Navigation");
        private BotNavigationSpace _space;
        private ArenaBounds _arenaBounds;
        private ArenaEnvironment _environment;
        private ArenaLayout _layout;
        private MatchController _match;
        private int _matchId = -1;
        private bool _navigationTick;
        public Transform Target
        {
            get => target;
            set { if (target == value) return; target = value; ResetDecision(); }
        }
        private void Awake()
        {
            _motor = GetComponent<RobotDriveMotor>();
            _ownHealth = GetComponent<RobotHealth>();
            _ownDamage = GetComponent<RobotDamageVisuals>();
            _ownColliders = GetComponentsInChildren<Collider>();
            if (_ownHealth != null) _ownHealth.Restored += ResetDecision;
        }
        private void OnDestroy() { if (_ownHealth != null) _ownHealth.Restored -= ResetDecision; }
        public void ConfigureDecisions(BotPersonality selectedPersonality, BotDifficulty selectedDifficulty, int seed = 740)
        {
            personality = selectedPersonality; difficulty = selectedDifficulty; decisionSeed = seed;
            ResetDecision();
        }
        private void OnValidate()
        {
            attackEnterDistance = Mathf.Max(0.1f, attackEnterDistance);
            followResumeDistance = Mathf.Max(attackEnterDistance + 0.1f, followResumeDistance);
        }
        public void SetMatchRunning(bool running) { MatchRunning = running; ResetDecision(); }
        public void ResetDecision()
        {
            State = Decision.Idle; Command = Vector2.zero; AttackRequested = _attackHold = false;
            _stuckTime = _maneuverTime = 0; Distance = float.PositiveInfinity;
            _pressureTime = _circleTime = _spinTime = _tacticTime = _graceTime = _noDamageTime = _reactionTime = 0;
            _engaged = _tacticsObserved = false; _bestDistance = float.PositiveInfinity;
            _flankSide = 1;
            Memory.Clear(); Threat = default;
            _decisionRandom = new System.Random(decisionSeed);
            _evasionTime = _evasionCooldown = _profileCooldown = AimError = DefenseSide = 0;
            DecisionSamples = EvasionCount = 0; EvasionPoint = transform.position;
            _previousTarget = target;
            _anchor = transform.position;
            _route.Reset();
            _navigationTick = false;
            if (_motor == null) _motor = GetComponent<RobotDriveMotor>();
            _motor.Clear(true);
        }
        private void OnDisable() { ResetDecision(); }
        private void FixedUpdate() { Tick(Time.fixedDeltaTime); }
        // Public deterministic decision step for the regression harness; motion stays in the shared motor.
        public void Tick(float dt)
        {
            // Settings pauses scaled time. Do not observe, forget, sample randomness or reset a round.
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt) || Time.timeScale <= 0)
            { AttackRequested = false; Send(0, 0); return; }
            if (_previousTarget != target) { ResetDecision(); _previousTarget = target; }
            var ownHealth = GetComponent<RobotHealth>();
            var targetHealth = target != null ? target.GetComponentInParent<RobotHealth>() : null;
            if (!MatchRunning || target == null || !target.gameObject.activeInHierarchy ||
                (ownHealth != null && ownHealth.IsDefeated) || (targetHealth != null && targetHealth.IsDefeated))
            { ResetDecision(); return; }
            // A physical footprint is required for runtime routing. Decision-only fixtures
            // and legacy rigs without colliders still use the original pursuit and recovery.
            _navigationTick = enableCombatTactics && _ownColliders.Length > 0;
            if (_navigationTick)
                using (NavigationSample.Auto()) BeginNavigation(dt);
            Vector3 delta = target.position - _motor.Body.position; delta.y = 0;
            Distance = delta.magnitude;
            TargetAngle = delta.sqrMagnitude < 0.0001f ? 0 : Vector3.SignedAngle(_motor.Body.rotation * Vector3.forward, delta, Vector3.up);
            AttackRequested = false;
            OwnCondition = new BotDamageState(ownHealth, _ownDamage);
            TargetCondition = new BotDamageState(targetHealth, target.GetComponentInParent<RobotDamageVisuals>());
            bool closeCounterPursuit = false;
            if (enableCombatTactics)
            {
                ObserveOpponent(dt, delta);
                bool recovering = State == Decision.BackingOut || State == Decision.EscapeTurn || State == Decision.EscapeDrive;
                closeCounterPursuit = !recovering && ResumeCloseCounterPursuitIfReady();
                if (!recovering && !closeCounterPursuit && TickOpponentDecisions(dt)) return;
            }
            if (enableCombatTactics && !closeCounterPursuit && TickCombatTactics(dt, targetHealth)) return;
            if (State == Decision.BackingOut || State == Decision.EscapeTurn || State == Decision.EscapeDrive)
            {
                _maneuverTime += dt;
                if (State == Decision.BackingOut && _maneuverTime < reverseSeconds) { Send(-0.7f, 0); return; }
                if (State == Decision.BackingOut) { State = Decision.EscapeTurn; _maneuverTime = 0; }
                if (State == Decision.EscapeTurn && _maneuverTime < escapeTurnSeconds) { Send(0, _escapeSign); return; }
                if (State == Decision.EscapeTurn) { State = Decision.EscapeDrive; _maneuverTime = 0; }
                if (_maneuverTime < escapeDriveSeconds) { Send(0.7f, 0); return; }
                State = Decision.Following; _stuckTime = 0; _anchor = _motor.Body.position;
            }
            // Keep the stopped stance across the distance band; attack intent is strictly inside the entry range.
            if (Distance > Mathf.Max(attackEnterDistance + 0.1f, followResumeDistance)) _attackHold = false;
            if (Distance <= Mathf.Max(0.1f, attackEnterDistance - attackStopInset)) _attackHold = true;
            float steeringAngle = TargetAngle;
            if (enableCombatTactics)
            {
                Vector3 aim = _aimPosition - _motor.Body.position; aim.y = 0;
                steeringAngle = Vector3.SignedAngle(_motor.Body.rotation * Vector3.forward, aim, Vector3.up);
            }
            float steerLimit = enableCombatTactics ? maximumSteer : 1;
            float steer = Mathf.Clamp(steeringAngle / Mathf.Max(1, fullSteerAngle), -steerLimit, steerLimit);
            bool facing = Mathf.Abs(TargetAngle) <= attackAngle;
            AttackRequested = OwnCondition.WeaponOperational && facing && Distance <= attackEnterDistance;
            if (AttackRequested) AttackRequest?.Invoke(this, target);
            if (!MatchRunning || target == null) { ResetDecision(); return; }
            if (_attackHold && facing && OwnCondition.WeaponOperational)
            {
                State = Decision.Attacking; Send(0, 0); _stuckTime = 0; _anchor = _motor.Body.position;
                return;
            }
            // An unarmed robot can still make a physical ram through the shared motor.
            bool canAdvance = !_attackHold || !OwnCondition.WeaponOperational;
            if (_navigationTick && canAdvance)
            {
                if (!TryPursuitDirection(dt, out Vector3 direction))
                { State = Decision.Navigating; _stuckTime = 0; _anchor = _motor.Body.position; Send(0, 0); return; }
                if (direction.sqrMagnitude <= .0001f) canAdvance = false;
                else
                {
                    steeringAngle = Vector3.SignedAngle(_motor.Body.rotation * Vector3.forward, direction, Vector3.up);
                    steer = Mathf.Clamp(steeringAngle / Mathf.Max(1, fullSteerAngle), -steerLimit, steerLimit);
                }
            }
            // Detours need tighter turns than open pursuit. Keep the original direct-path profile.
            float drive = canAdvance && Mathf.Abs(steeringAngle) <= (_route.Detouring ? 28f : driveAngle)
                ? (enableCombatTactics ? pursuitDrive : 0.85f) : 0;
            if (_route.HasWaypoint)
                drive *= Mathf.Clamp(Vector2.Distance(BotNavigationSpace.Planar(_motor.Body.position), _route.Waypoint) * 1.6f, .3f, .8f);
            State = drive > 0 ? Decision.Following : Decision.Turning;
            if (drive > 0 && !_route.Detouring)
            {
                Vector3 progress = _motor.Body.position - _anchor; progress.y = 0;
                if (progress.magnitude >= minimumProgress) { _anchor = _motor.Body.position; _stuckTime = 0; }
                else _stuckTime += dt;
                if (_stuckTime >= stuckSeconds)
                {
                    State = Decision.BackingOut; _maneuverTime = _stuckTime = 0; RecoveryCount++;
                    _escapeSign = TargetAngle < 0 ? -1 : 1;
                    Send(-0.7f, 0); return;
                }
            }
            else { _stuckTime = 0; _anchor = _motor.Body.position; }
            Send(drive, steer);
        }
        private bool ResumeCloseCounterPursuitIfReady()
        {
            if (personality != BotPersonality.Counter || !OwnCondition.WeaponOperational ||
                Distance > Mathf.Max(attackEnterDistance + .1f, followResumeDistance)) return false;
            var weapon = GetComponent<Combat.MeleeAttack>();
            if (weapon == null || weapon.IsAttacking || weapon.CooldownRemaining > 0) return false;

            bool defending = State == Decision.Disengaging || State == Decision.Repositioning ||
                State == Decision.Waiting || State == Decision.Evading;
            if (defending)
            {
                // Once caught in the existing pursuit band, reacquire aim instead of finishing a flank.
                State = Decision.Following;
                _tacticTime = _evasionTime = _graceTime = _stuckTime = 0;
                _pressureTime = _circleTime = _spinTime = _noDamageTime = 0;
                _engaged = _tacticsObserved = _attackHold = false;
                _anchor = _motor.Body.position;
                _evasionCooldown = Mathf.Max(_evasionCooldown, EffectiveReactionSeconds);
                _profileCooldown = Mathf.Max(_profileCooldown, EffectiveReactionSeconds);
            }
            // Normal pursuit still owns steering, attack range and attack angle; melee owns cooldown.
            return true;
        }

        private bool TickCombatTactics(float dt, RobotHealth targetHealth)
        {
            // Lost wheels favor holding an angle over repeatedly attempting wide flank arcs.
            if (OwnCondition.LimitedMovement)
            {
                if (State == Decision.Disengaging || State == Decision.Repositioning || State == Decision.Waiting)
                    State = Decision.Following;
                return false;
            }
            // Committed world-space maneuvers: the target's spin cannot keep rotating the waypoint.
            if (State == Decision.Disengaging || State == Decision.Repositioning || State == Decision.Waiting)
            {
                _tacticTime += dt;
                if (State == Decision.Disengaging)
                {
                    if (_tacticTime < disengageSeconds) { DriveToPoint(_retreatPoint, true); return true; }
                    State = Decision.Repositioning; _tacticTime = 0;
                }
                if (State == Decision.Repositioning)
                {
                    if (_tacticTime < repositionSeconds) { DriveToPoint(_flankPoint, false); return true; }
                    State = Decision.Waiting; _tacticTime = 0;
                }
                if (_tacticTime < openingSeconds)
                {
                    // Hold the opening position while reacquiring the opponent after the flank.
                    Vector3 aim = _aimPosition - _motor.Body.position; aim.y = 0;
                    float angle = Vector3.SignedAngle(_motor.Body.rotation * Vector3.forward, aim, Vector3.up);
                    Send(0, Mathf.Clamp(angle / Mathf.Max(1, fullSteerAngle), -maximumSteer, maximumSteer));
                    return true;
                }
                State = Decision.Following; _tacticTime = 0; _graceTime = 0.75f;
                _pressureTime = _circleTime = _spinTime = _noDamageTime = 0;
                _engaged = false; _bestDistance = Distance; _reactionTime = 0;
            }
            // Wall recovery has priority and must not be mistaken for a failed attack.
            if (State == Decision.BackingOut || State == Decision.EscapeTurn || State == Decision.EscapeDrive)
            { _circleTime = _spinTime = _noDamageTime = _pressureTime = 0; _engaged = false; return false; }
            float yaw = target.GetComponentInParent<Rigidbody>() is Rigidbody body ? body.rotation.eulerAngles.y : target.eulerAngles.y;
            if (!_tacticsObserved)
            {
                _previousTargetYaw = yaw; _lastTargetHealth = targetHealth != null ? targetHealth.CurrentHealth : 0;
                _bestDistance = Distance; _tacticsObserved = true;
            }
            float yawRate = Mathf.Abs(Mathf.DeltaAngle(_previousTargetYaw, yaw)) / Mathf.Max(dt, 0.001f);
            _previousTargetYaw = yaw;
            bool damaged = targetHealth != null && targetHealth.CurrentHealth < _lastTargetHealth - 0.01f;
            if (targetHealth != null) _lastTargetHealth = targetHealth.CurrentHealth;
            _graceTime = Mathf.Max(0, _graceTime - dt);
            if (Distance > followResumeDistance + 1 || _graceTime > 0)
            {
                _pressureTime = _circleTime = _spinTime = _noDamageTime = 0;
                _engaged = false; _bestDistance = Distance; return false;
            }
            _spinTime = yawRate > 100 ? _spinTime + dt : Mathf.Max(0, _spinTime - dt * 2);
            _noDamageTime = damaged ? 0 : _noDamageTime + dt;
            if (Distance < _bestDistance - 0.25f) { _bestDistance = Distance; _circleTime = 0; }
            else _circleTime += dt;
            if (Distance <= attackEnterDistance && Mathf.Abs(TargetAngle) <= attackAngle) _engaged = true;
            if (_engaged) _pressureTime += dt;
            float pressureBudget = attackBurstSeconds * (personality == BotPersonality.Counter ? 0.65f : 1f);
            if (OwnCondition.HealthFraction < 0.35f || !OwnCondition.WeaponOperational) pressureBudget *= 0.65f;
            if (!TargetCondition.WeaponOperational || TargetCondition.HealthFraction < 0.3f) pressureBudget *= 1.25f;
            if (_pressureTime >= pressureBudget || _spinTime >= 0.85f ||
                _circleTime >= circleTimeout || targetHealth != null && _noDamageTime >= circleTimeout + 0.5f)
            {
                BeginReposition(); DriveToPoint(_retreatPoint, true); return true;
            }
            return false;
        }
        private void BeginReposition()
        {
            Vector3 away = _motor.Body.position - target.position; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = -(_motor.Body.rotation * Vector3.forward);
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);
            _retreatPoint = ClampTacticalPoint(_motor.Body.position + away * 1.6f);
            Vector3 first = ClampTacticalPoint(target.position + away * 2.3f + side * (_flankSide * 1.8f));
            Vector3 other = ClampTacticalPoint(target.position + away * 2.3f - side * (_flankSide * 1.8f));
            // Prefer the other side if a boundary squeezed the chosen point into the opponent.
            _flankPoint = (first - target.position).sqrMagnitude >= (other - target.position).sqrMagnitude * 0.7f ? first : other;
            _flankSide = -_flankSide;
            State = Decision.Disengaging; _tacticTime = 0; _attackHold = false;
            _stuckTime = 0; _anchor = _motor.Body.position; RepositionCount++;
        }
        private Vector3 ClampTacticalPoint(Vector3 point)
        {
            if (_navigationTick) return AtBodyHeight(_space.Clamp(BotNavigationSpace.Planar(point)));
            return new Vector3(Mathf.Clamp(point.x, -tacticalHalfExtents.x, tacticalHalfExtents.x),
                _motor.Body.position.y, Mathf.Clamp(point.z, -tacticalHalfExtents.y, tacticalHalfExtents.y));
        }
        private void DriveToPoint(Vector3 point, bool reverse)
        {
            Vector3 offset = point - _motor.Body.position; offset.y = 0;
            Vector3 forward = _motor.Body.rotation * Vector3.forward;
            float angle = Vector3.SignedAngle(reverse ? -forward : forward, offset, Vector3.up);
            float drive = offset.magnitude > 0.25f && Mathf.Abs(angle) < 65 ? (reverse ? -0.6f : pursuitDrive) : 0;
            if (drive != 0 && !IsTacticalPathSafe(point)) drive = 0;
            Send(drive, Mathf.Clamp(angle / Mathf.Max(1, fullSteerAngle), -maximumSteer, maximumSteer));
        }
        private void ObserveOpponent(float dt, Vector3 separation)
        {
            var body = target.GetComponentInParent<Rigidbody>();
            Threat = new BotThreatObservation(separation, _motor.Body.linearVelocity,
                body != null ? body.linearVelocity : Vector3.zero, target.forward);
            Memory.Observe(Threat.Incoming, TargetAngle, dt);
            _evasionCooldown = Mathf.Max(0, _evasionCooldown - dt);
            _profileCooldown = Mathf.Max(0, _profileCooldown - dt);
            _reactionTime -= dt;
            _sampleDecision = _reactionTime <= 0;
            if (!_sampleDecision) return;
            if (_decisionRandom == null) _decisionRandom = new System.Random(decisionSeed);
            var profile = new BotDecisionProfile(difficulty);
            bool missed = DecisionSamples > 0 && _decisionRandom.NextDouble() < profile.MissedDecisionChance;
            _reactionTime = EffectiveReactionSeconds;
            if (missed) { _sampleDecision = false; return; }
            AimError = ((float)_decisionRandom.NextDouble() * 2f - 1f) * profile.AimErrorDegrees;
            _aimPosition = _motor.Body.position + Quaternion.Euler(0, AimError, 0) * separation;
            DecisionSamples++;
        }

        private bool TickOpponentDecisions(float dt)
        {
            // Finish existing bounded retreat/flank/opening cycles before starting another defense.
            if (State == Decision.Disengaging || State == Decision.Repositioning || State == Decision.Waiting) return false;
            if (State == Decision.Evading)
            {
                _evasionTime += dt;
                if (!OwnCondition.LimitedMovement && _evasionTime < 0.45f)
                { DriveToPoint(EvasionPoint, false); return true; }
                State = Decision.Following; _graceTime = 0.65f;
                _stuckTime = 0; _anchor = _motor.Body.position;
            }
            if (!_sampleDecision || OwnCondition.LimitedMovement) return false;
            if (Threat.Incoming && _evasionCooldown <= 0)
            {
                // Repeated frontal charges switch the preferred defense side after three distinct onsets.
                float side = TargetAngle < 0 ? 1 : -1;
                if (Memory.AdaptDefense) side = -side;
                Vector3 lateral = Vector3.Cross(Vector3.up, (target.position - _motor.Body.position).normalized);
                float error = ((float)_decisionRandom.NextDouble() * 2 - 1) * new BotDecisionProfile(difficulty).PositionError;
                Vector3 first = ClampTacticalPoint(_motor.Body.position + lateral * side * (1.5f + error));
                Vector3 second = ClampTacticalPoint(_motor.Body.position - lateral * side * (1.5f + error));
                bool firstSafe = IsTacticalPathSafe(first), secondSafe = IsTacticalPathSafe(second);
                EvasionPoint = firstSafe ? first : secondSafe ? second : _motor.Body.position;
                DefenseSide = firstSafe ? side : secondSafe ? -side : 0;
                State = Decision.Evading; _evasionTime = 0; _evasionCooldown = 1.1f;
                _attackHold = false; EvasionCount++;
                DriveToPoint(EvasionPoint, false); return true;
            }
            // Counter takes one bounded angle-seeking pass, then commits for at least three seconds.
            // Aggressive closes the same gap immediately with exactly the same motor capability.
            bool poorEntry = Distance > attackEnterDistance && Distance < followResumeDistance + 0.8f &&
                Vector3.Dot(target.forward, (_motor.Body.position - target.position).normalized) > 0.5f;
            bool vulnerable = OwnCondition.HealthFraction < 0.35f || !OwnCondition.WeaponOperational;
            if (_profileCooldown <= 0 && _graceTime <= 0 &&
                (State == Decision.Following || State == Decision.Turning || State == Decision.Idle) &&
                ((personality == BotPersonality.Counter && TargetCondition.WeaponOperational && poorEntry) ||
                 vulnerable && TargetCondition.WeaponOperational && Distance < followResumeDistance))
            {
                BeginReposition(); _profileCooldown = disengageSeconds + repositionSeconds + openingSeconds + 3f;
                DriveToPoint(_retreatPoint, true); return true;
            }
            return false;
        }

        public bool IsTacticalPathSafe(Vector3 point)
        {
            if (_navigationTick)
            {
                using (NavigationSample.Auto())
                {
                    Vector2 startPoint = BotNavigationSpace.Planar(_motor.Body.position);
                    Vector2 endPoint = BotNavigationSpace.Planar(point);
                    return (endPoint - startPoint).sqrMagnitude >= .09f &&
                        _space.Probe(startPoint, endPoint, allowOpponent: false) == BotNavigationSpace.ProbeResult.Clear;
                }
            }
            if (Mathf.Abs(point.x) > Mathf.Abs(tacticalHalfExtents.x) || Mathf.Abs(point.z) > Mathf.Abs(tacticalHalfExtents.y)) return false;
            Vector3 start = _motor.Body.position;
            Vector3 travel = point - start; travel.y = 0;
            if (travel.magnitude < 0.3f) return false;
            // A conservative horizontal footprint; ignore floors, self and triggers, never the opponent.
            float radius = 0.65f;
            foreach (var collider in _ownColliders)
                if (collider != null && collider.enabled && !collider.isTrigger && collider.attachedRigidbody == _motor.Body)
                    radius = Mathf.Max(radius, new Vector2(collider.bounds.extents.x, collider.bounds.extents.z).magnitude);
            Vector3 centre = new Vector3(point.x, start.y + 0.2f, point.z);
            int overlaps = Physics.OverlapBoxNonAlloc(centre, new Vector3(radius, 0.18f, radius), _obstacles,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps == _obstacles.Length) return false;
            for (int i = 0; i < overlaps; i++) if (IsObstacle(_obstacles[i], start.y)) return false;
            int hits = Physics.BoxCastNonAlloc(new Vector3(start.x, start.y + 0.2f, start.z),
                new Vector3(radius, 0.18f, radius), travel.normalized, _pathHits, Quaternion.identity,
                travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (hits == _pathHits.Length) return false;
            for (int i = 0; i < hits; i++) if (IsObstacle(_pathHits[i].collider, start.y)) return false;
            return true;
        }
        private bool IsObstacle(Collider collider, float height)
        {
            return collider != null && collider.attachedRigidbody != _motor.Body &&
                !collider.transform.IsChildOf(transform) && collider.bounds.max.y > height + 0.02f;
        }
        private void BeginNavigation(float dt)
        {
            if (_space == null)
            {
                var robot = GetComponent<RobotPrototypeController>();
                foreach (var arena in FindObjectsByType<ArenaBounds>(FindObjectsSortMode.None))
                {
                    if (robot == null || arena.gameObject.scene != gameObject.scene) continue;
                    _arenaBounds = arena;
                    _environment = arena.GetComponent<ArenaEnvironment>();
                    _match = _environment != null ? _environment.Match : null;
                    break;
                }
                _space = new BotNavigationSpace(_motor.Body, _ownColliders, robot != null ? robot.ArenaFootprintRadius : 0);
            }
            var layout = _environment != null ? _environment.Layout : null;
            int id = _match != null ? _match.MatchCount : -1;
            if (_layout != layout || _matchId != id) { _route.Reset(); _layout = layout; _matchId = id; }
            _space.BeginTick(_arenaBounds, tacticalHalfExtents, target);
            _route.BeginTick(dt, _space);
        }

        private bool TryPursuitDirection(float dt, out Vector3 direction)
        {
            using (NavigationSample.Auto())
            {
                Vector2 position = BotNavigationSpace.Planar(_motor.Body.position);
                Vector2 conveyor = BotNavigationSpace.Planar(ConveyorSurface.AppliedVelocity(_motor.Body));
                Vector2 motorVelocity = BotNavigationSpace.Planar(_motor.Body.linearVelocity) - conveyor;
                bool safe = _route.TrySteer(_space, position, _space.Clamp(BotNavigationSpace.Planar(_aimPosition)),
                    _space.Clamp(BotNavigationSpace.Planar(target.position)), motorVelocity, conveyor, dt, out var point);
                Vector2 offset = point - position;
                direction = new Vector3(offset.x, 0, offset.y);
                return safe;
            }
        }

        private void Send(float drive, float steer)
        {
            if (_navigationTick && drive != 0)
            {
                using (NavigationSample.Auto())
                {
                    Vector2 position = BotNavigationSpace.Planar(_motor.Body.position);
                    Vector2 heading = BotNavigationSpace.Planar(_motor.Body.rotation * Vector3.forward).normalized * Mathf.Sign(drive);
                    float speed = Mathf.Max(0f, Vector2.Dot(BotNavigationSpace.Planar(_motor.Body.linearVelocity), heading));
                    Vector2 point = position + heading * Mathf.Clamp(.12f + speed * .1f, .12f, .6f);
                    if (_space.Probe(position, point, reserve: 0) != BotNavigationSpace.ProbeResult.Clear)
                        drive = 0;
                }
            }
            if (_navigationTick && _space.Saturated)
            {
                if (_route.Reason != BotRouteReason.QuerySaturated)
                    _route.Invalidate(BotNavigationSpace.Planar(_motor.Body.position), BotRouteReason.QuerySaturated);
                drive = steer = 0;
            }
            Command = new Vector2(drive, steer);
            _motor.Submit(drive, steer);
        }

        private Vector3 AtBodyHeight(Vector2 point) => new Vector3(point.x, _motor != null && _motor.Body != null ? _motor.Body.position.y : transform.position.y, point.y);

        private void OnDrawGizmosSelected()
        {
            if (!drawNavigationGizmos) return;
            Gizmos.color = Color.yellow;
            if (target != null) Gizmos.DrawWireSphere(target.position, .16f);
            Gizmos.color = Color.cyan;
            for (int i = 1; i < _route.Count; i++) Gizmos.DrawLine(AtBodyHeight(_route.Point(i - 1)), AtBodyHeight(_route.Point(i)));
            if (_route.HasWaypoint) Gizmos.DrawLine(transform.position, NavigationWaypoint);
            if (_route.Reason != BotRouteReason.None)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, AtBodyHeight(_route.BlockedPoint));
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"{NavigationReason}  queries {NavigationQueries}  nodes {NavigationExpansions}");
#endif
        }
    }
}
