using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ArenaVenue : MonoBehaviour
    {
        private MatchController match;
        private ArenaEnvironment arena;
        private ArenaVenueModel model;
        private MatchReplayPlayer replay;
        private MatchReplayArchivePlayer archive;
        private PostMatchInspection inspection;
        private ChallengeSessionController challenge;
        private RobotHealth blue, orange;
        private ArenaLayout layout;
        private bool low;
        private AudioSource room, signals;
        private AudioClip bed, bell;
        private MatchController.MatchState previous;
        private int lastCount;
        private float lastBlue = -10f, lastOrange = -10f;
        public bool IsPresenting { get; private set; }
        public float BlueSignal { get; private set; }
        public float OrangeSignal { get; private set; }

        private void Start()
        {
            match = GetComponent<MatchController>();
            arena = FindAnyObjectByType<ArenaEnvironment>();
            if (!arena || !match || !match.Player || !match.Opponent) { enabled = false; return; }
            replay = GetComponent<MatchReplayPlayer>();
            archive = GetComponent<MatchReplayArchivePlayer>();
            inspection = GetComponent<PostMatchInspection>();
            challenge = GetComponent<ChallengeSessionController>();
            blue = match.Player.GetComponent<RobotHealth>();
            orange = match.Opponent.GetComponent<RobotHealth>();
            blue.Damaged += BlueHit;
            orange.Damaged += OrangeHit;
            var root = new GameObject("Arena venue");
            root.transform.SetParent(arena.transform, false);
            model = root.AddComponent<ArenaVenueModel>();
            room = NewSource(root, true);
            signals = NewSource(root, false);
            bed = MakeSound(true);
            bell = MakeSound(false);
            room.clip = bed;
            root.SetActive(false);
        }

        private static AudioSource NewSource(GameObject root, bool loop)
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.priority = loop ? 220 : 170;
            return source;
        }

        // Deterministic band-limited crowd murmur and a short two-note referee chime.
        private static AudioClip MakeSound(bool ambient)
        {
            const int rate = 16000;
            int count = ambient ? 4 * rate : rate / 4;
            var samples = new float[count];
            var random = new System.Random(391);
            float smooth = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                if (ambient)
                {
                    smooth += .12f * ((float)random.NextDouble() * 2 - 1 - smooth);
                    float edge = Mathf.Clamp01(Mathf.Min(i, count - i - 1) / 600f);
                    samples[i] = smooth * (.66f + .24f * Mathf.Cos(t * Mathf.PI)) * edge;
                }
                else samples[i] = Mathf.Sin(t * 2 * Mathf.PI * 440) * (.8f + .2f * Mathf.Cos(t * 2 * Mathf.PI * 220))
                    * Mathf.Exp(-t * 18) * Mathf.Clamp01(t * 140);
            }
            var clip = AudioClip.Create(ambient ? "Indoor arena crowd" : "Referee bell", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void BlueHit(RobotHit hit) { if (hit.Weapon && hit.Source == orange) lastOrange = Time.time; }
        private void OrangeHit(RobotHit hit) { if (hit.Weapon && hit.Source == blue) lastBlue = Time.time; }

        private void LateUpdate()
        {
            if (!model) return;
            var state = match.State;
            bool active = match.isActiveAndEnabled && !match.IsPaused &&
                (state == MatchController.MatchState.Playing || state == MatchController.MatchState.Countdown || state == MatchController.MatchState.Result) &&
                !(replay && replay.IsActive) && !(inspection && inspection.IsActive) &&
                !(archive && archive.IsArchiveModeOpen) && !(challenge && (challenge.DialogOpen || challenge.InChallenge));
            if (!active || !arena.Layout) { Silence(); return; }
            bool economy = QualitySettings.GetQualityLevel() == 0;
            if (layout != arena.Layout || low != economy)
            {
                layout = arena.Layout;
                low = economy;
                model.Build(layout, low);
            }
            if (!IsPresenting)
            {
                IsPresenting = true;
                model.gameObject.SetActive(true);
                room.Play();
            }
            bool counting = state == MatchController.MatchState.Countdown;
            bool result = state == MatchController.MatchState.Result;
            float now = Time.time;
            float amber = counting ? Mathf.Clamp01(1 - Mathf.Repeat(match.CountdownRemaining, 1)) : 0;
            float blueHit = Mathf.Clamp01(1 - (now - lastBlue) * 2);
            float orangeHit = Mathf.Clamp01(1 - (now - lastOrange) * 2);
            BlueSignal = result ? (match.Outcome == MatchController.MatchOutcome.OpponentVictory ? .12f : 1) :
                state == MatchController.MatchState.Playing && match.ShowStartMessage ? .85f : .18f + .82f * blueHit;
            OrangeSignal = result ? (match.Outcome == MatchController.MatchOutcome.PlayerVictory ? .12f : 1) :
                state == MatchController.MatchState.Playing && match.ShowStartMessage ? .85f : .18f + .82f * orangeHit;
            float cheer = result ? .8f : Mathf.Max(blueHit, orangeHit);
            model.Present(now, cheer, BlueSignal, OrangeSignal, amber);
            room.volume = (low ? .075f : .14f) + cheer * (low ? .02f : .055f);
            int tick = counting ? Mathf.CeilToInt(match.CountdownRemaining) : 0;
            if ((counting && tick > 0 && lastCount != tick) ||
                (previous == MatchController.MatchState.Countdown && state == MatchController.MatchState.Playing) ||
                (result && previous != state))
            {
                signals.pitch = result ? .8f : counting ? 1f : 1.35f;
                signals.PlayOneShot(bell, low ? .09f : .14f);
            }
            lastCount = tick;
            previous = state;
        }

        private void Silence()
        {
            if (IsPresenting)
            {
                room.Stop();
                signals.Stop();
                model.gameObject.SetActive(false);
            }
            IsPresenting = false;
            lastCount = 0;
            previous = match ? match.State : MatchController.MatchState.Ready;
            lastBlue = lastOrange = -10;
            BlueSignal = OrangeSignal = 0;
        }

        private void OnDisable() => Silence();

        private void OnDestroy()
        {
            if (blue) blue.Damaged -= BlueHit;
            if (orange) orange.Damaged -= OrangeHit;
            if (model) Destroy(model.gameObject);
            if (bed) Destroy(bed);
            if (bell) Destroy(bell);
        }
    }
}
