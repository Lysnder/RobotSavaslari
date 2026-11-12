using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Post-match robot inspection on MainArena: a frozen look-only snapshot of the chosen robot,
    /// missing parts outlined, orbit and hidden interface, then everything restored. Tests cause
    /// damage through RobotHealth themselves; the inspection code never does.
    /// </summary>
    public sealed class PostMatchInspectionPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator UnloadArena() => MainArenaTestScene.Unload();

        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;
        private PostMatchInspection _inspection;
        private MatchReplayPlayer _replay;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        private IEnumerator Load()
        {
            yield return MainArenaTestScene.Load();
            _inspection = Match.GetComponent<PostMatchInspection>();
            _replay = Match.GetComponent<MatchReplayPlayer>();
            Assert.That(_inspection, Is.Not.Null, "inspection installed");
            Assert.That(Match.GetComponents<PostMatchInspection>().Length, Is.EqualTo(1));
        }

        private static void SeparateRobots()
        {
            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(-4.5f, MainArenaTestScene.Floor, -3.5f), true);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(4.5f, MainArenaTestScene.Floor, 3.5f), true);
        }

        /// <summary>Hits the opponent's first part of a kind hard enough to tear it off.</summary>
        private static RobotDamageVisuals.Part DetachOpponentPart(RobotDamageVisuals.PartKind kind)
        {
            var visuals = Match.Opponent.GetComponent<RobotDamageVisuals>();
            Assume.That(visuals, Is.Not.Null);
            RobotDamageVisuals.Part part = visuals.Parts.FirstOrDefault(p => p.Kind == kind);
            Assume.That(part, Is.Not.Null, "opponent mesh is partitioned into parts");
            Match.Opponent.GetComponent<RobotHealth>().TakeDamage(part.Threshold + 1f, part.Transform.position, Vector3.up, Vector3.zero, true);
            Assert.That(part.Detached, Is.True, kind + " came off");
            return part;
        }

        private static Renderer[] LiveRenderers() => Match.Player.GetComponentsInChildren<Renderer>(true)
            .Concat(Match.Opponent.GetComponentsInChildren<Renderer>(true)).ToArray();

        private static void AssertLookOnly(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            var allowed = new HashSet<System.Type> { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer) };
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                Assert.That(allowed.Contains(component.GetType()), Is.True, component.GetType().Name + " on snapshot " + component.name);
        }

        [UnityTest]
        public IEnumerator InspectionShowsTheFinalDamageWithoutSideEffects()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            RobotDamageVisuals.Part wheel = DetachOpponentPart(RobotDamageVisuals.PartKind.Wheel);
            Match.Defeat(false);
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Result));

            var menu = Match.GetComponent<MainMenuScreen>();
            Renderer[] live = LiveRenderers();
            bool[] liveForceOff = live.Select(r => r.forceRenderingOff).ToArray();
            float yaw = CameraRig.Yaw, pitch = CameraRig.Pitch, distance = CameraRig.Distance;
            float playerHealth = Match.Player.GetComponent<RobotHealth>().CurrentHealth;
            float opponentHealth = Match.Opponent.GetComponent<RobotHealth>().CurrentHealth;
            int matchCount = Match.MatchCount, resultCount = Match.ResultCount;
            MatchController.MatchOutcome outcome = Match.Outcome;
            Vector3 playerPosition = Match.Player.transform.position, opponentPosition = Match.Opponent.transform.position;

            int events = 0;
            System.Action<RobotHealth> onHealth = _ => events++;
            System.Action<RobotHit> onDamage = _ => events++;
            System.Action<MatchController.MatchOutcome> onResult = _ => events++;
            RobotHealth[] healths = { Match.Player.GetComponent<RobotHealth>(), Match.Opponent.GetComponent<RobotHealth>() };
            foreach (RobotHealth health in healths)
            {
                health.HealthChanged += onHealth;
                health.Damaged += onDamage;
            }
            Match.ResultDetermined += onResult;
            try
            {
                Assert.That(_inspection.CanBegin, Is.True);
                Assert.That(_inspection.Begin(PostMatchInspection.OpponentRobot), Is.True);
                yield return null;
                Transform opponentSnapshot = _inspection.SnapshotRoot;
                Assert.That(menu.enabled, Is.False, "menu hidden while inspecting");
                Assert.That(live.All(r => r.forceRenderingOff), Is.True, "live robots hidden");
                Assert.That(CameraRig.Target, Is.EqualTo(opponentSnapshot), "camera orbits the snapshot");
                Assert.That(CameraRig.Distance, Is.EqualTo(4.5f).Within(1e-3f), "inspection starts at a close orbit");
                MainArenaTestScene.AssertSingleCameraAndListener();
                AssertLookOnly(opponentSnapshot);
                Assert.That(opponentSnapshot.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0), "snapshot has meshes");
                Assert.That(opponentSnapshot.Find(wheel.Name), Is.Null, "detached wheel is not part of the snapshot");
                Assert.That(opponentSnapshot.Find("Missing " + wheel.Name), Is.Not.Null, "missing wheel outlined");
                Assert.That(_inspection.MissingPartOutlines, Is.EqualTo(1));

                RobotInspectionReport report = _inspection.Report;
                Assert.That(report.RobotName, Is.EqualTo(Match.Opponent.name));
                Assert.That(report.DetachedCount, Is.EqualTo(1));
                Assert.That(report.IsUndamaged, Is.False);
                Assert.That(report.Parts.Single(p => p.Name == wheel.Name).Condition, Is.EqualTo("Koptu"));

                float viewYaw = CameraRig.Yaw;
                CameraRig.ApplyOrbitInput(new Vector2(120f, 0f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(viewYaw, CameraRig.Yaw)), Is.GreaterThan(1f), "orbit works on the snapshot");

                Assert.That(_inspection.ProcessToggleKey(), Is.True);
                Assert.That(_inspection.IsInterfaceHidden, Is.True);
                Assert.That(CameraRig.ControlsVisible, Is.False, "camera buttons hidden with the interface");
                Assert.That(_inspection.ProcessToggleKey(), Is.True);
                Assert.That(CameraRig.ControlsVisible, Is.True);

                Assert.That(_inspection.Select(PostMatchInspection.PlayerRobot), Is.True);
                yield return null;
                Assert.That(opponentSnapshot == null, Is.True, "previous snapshot destroyed");
                Assert.That(_inspection.SelectedRobot, Is.EqualTo(PostMatchInspection.PlayerRobot));
                Assert.That(_inspection.Report.RobotName, Is.EqualTo(Match.Player.name));
                Assert.That(_inspection.Report.DetachedCount, Is.Zero);
                Assert.That(CameraRig.Target, Is.EqualTo(_inspection.SnapshotRoot));
                Assert.That(menu.enabled, Is.False, "menu stays hidden while switching");
                Assert.That(live.All(r => r.forceRenderingOff), Is.True, "live robots stay hidden while switching");
                MainArenaTestScene.AssertSingleCameraAndListener();

                _inspection.SetInterfaceHidden(true);
                _inspection.Stop();
                yield return null;
            }
            finally
            {
                foreach (RobotHealth health in healths)
                {
                    health.HealthChanged -= onHealth;
                    health.Damaged -= onDamage;
                }
                Match.ResultDetermined -= onResult;
            }

            Assert.That(Object.FindObjectsByType<Transform>().Any(t => t.name.StartsWith("Robot Inspection")), Is.False, "snapshots cleaned up");
            Assert.That(menu.enabled, Is.True, "menu restored");
            Assert.That(live.Select(r => r.forceRenderingOff).ToArray(), Is.EqualTo(liveForceOff), "live renderers restored");
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform), "camera back on the live robot");
            Assert.That(CameraRig.ControlsVisible, Is.True, "camera buttons restored");
            Assert.That(CameraRig.Yaw, Is.EqualTo(yaw).Within(1e-3f), "orbit restored");
            Assert.That(CameraRig.Pitch, Is.EqualTo(pitch).Within(1e-3f));
            Assert.That(CameraRig.Distance, Is.EqualTo(distance).Within(1e-3f));

            Assert.That(events, Is.Zero, "no health, damage or result events during inspection");
            Assert.That(healths[0].CurrentHealth, Is.EqualTo(playerHealth));
            Assert.That(healths[1].CurrentHealth, Is.EqualTo(opponentHealth));
            Assert.That(Match.MatchCount, Is.EqualTo(matchCount));
            Assert.That(Match.ResultCount, Is.EqualTo(resultCount));
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(Match.Outcome, Is.EqualTo(outcome));
            Assert.That(Vector3.Distance(Match.Player.transform.position, playerPosition), Is.LessThan(1e-4f), "player not moved");
            Assert.That(Vector3.Distance(Match.Opponent.transform.position, opponentPosition), Is.LessThan(1e-4f), "opponent not moved");
            Assert.That(wheel.Detached, Is.True, "the live robot is not repaired");
        }

        [UnityTest]
        public IEnumerator SnapshotOutlivesDebrisAndIgnoresTheLiveRobot()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            RobotDamageVisuals.Part weapon = DetachOpponentPart(RobotDamageVisuals.PartKind.Weapon);
            Match.Defeat(false);
            Assert.That(_inspection.Begin(PostMatchInspection.OpponentRobot), Is.True);
            Assert.That(_inspection.Report.WeaponOperational, Is.False, "weapon loss reported");
            Assert.That(_inspection.SnapshotRoot.Find("Missing " + weapon.Name), Is.Not.Null, "missing weapon outlined");

            Transform[] parts = _inspection.SnapshotRoot.Cast<Transform>().ToArray();
            Vector3[] positions = parts.Select(t => t.position).ToArray();

            // The damage system destroys debris on its own timer; do it now, and move the live robot.
            string debrisPrefix = Match.Opponent.name + " Debris ";
            GameObject[] debris = Match.gameObject.scene.GetRootGameObjects().Where(g => g.name.StartsWith(debrisPrefix)).ToArray();
            Assert.That(debris, Is.Not.Empty, "debris spawned");
            foreach (GameObject item in debris) Object.Destroy(item);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(0f, MainArenaTestScene.Floor, 0f), true);
            yield return null;
            yield return null;

            Assert.That(parts.All(t => t != null), Is.True, "snapshot intact after debris is gone");
            Assert.That(parts.Select(t => t.position).ToArray(), Is.EqualTo(positions), "snapshot frozen");
            Assert.That(_inspection.IsActive, Is.True);
            _inspection.Stop();
            yield return null;
            Assert.That(Match.GetComponent<MainMenuScreen>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator UndamagedRobotAndInvalidSelectionsAreHandled()
        {
            yield return Load();
            Assert.That(_inspection.Begin(PostMatchInspection.PlayerRobot), Is.False, "not before a match");
            yield return MainArenaTestScene.StartPlaying();
            Assert.That(_inspection.CanBegin, Is.False, "not during play");
            Assert.That(_inspection.Begin(PostMatchInspection.PlayerRobot), Is.False);
            Match.Defeat(false); // Decided without any damage.

            Assert.That(_inspection.Begin(-1), Is.False, "no selection");
            Assert.That(_inspection.Begin(5), Is.False, "unknown robot");
            Assert.That(_inspection.IsActive, Is.False);
            Assert.That(Match.GetComponent<MainMenuScreen>().enabled, Is.True, "nothing taken over by a rejected selection");

            Assert.That(_inspection.Begin(PostMatchInspection.OpponentRobot), Is.True);
            Assert.That(_inspection.Report.IsUndamaged, Is.True);
            Assert.That(_inspection.Report.WeaponOperational, Is.True);
            Assert.That(_inspection.MissingPartOutlines, Is.Zero);
            Assert.That(_inspection.Select(7), Is.False);
            Assert.That(_inspection.SelectedRobot, Is.EqualTo(PostMatchInspection.OpponentRobot), "invalid switch keeps the selection");
            _inspection.Stop();
            Assert.That(_inspection.SelectedRobot, Is.EqualTo(-1));
            Assert.That(_inspection.Report, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplayAndInspectionNeverRunTogether()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(false);
            var menu = Match.GetComponent<MainMenuScreen>();
            Renderer[] live = LiveRenderers();
            bool[] liveForceOff = live.Select(r => r.forceRenderingOff).ToArray();

            Assert.That(_replay.Begin(), Is.True);
            Assert.That(_inspection.Begin(PostMatchInspection.PlayerRobot), Is.True);
            Assert.That(_replay.IsActive, Is.False, "inspection closes the replay");
            Assert.That(_replay.CanBegin, Is.False, "replay waits while inspecting");
            Assert.That(_replay.Begin(), Is.False);
            yield return null;
            Assert.That(CameraRig.Target, Is.EqualTo(_inspection.SnapshotRoot));
            Assert.That(menu.enabled, Is.False);

            _inspection.Stop();
            Assert.That(menu.enabled, Is.True);
            Assert.That(live.Select(r => r.forceRenderingOff).ToArray(), Is.EqualTo(liveForceOff));
            Assert.That(_replay.CanBegin, Is.True);
            Assert.That(_replay.Begin(), Is.True);
            _replay.Stop();
            Assert.That(menu.enabled, Is.True);
            Assert.That(live.Select(r => r.forceRenderingOff).ToArray(), Is.EqualTo(liveForceOff));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewMatchClosesInspection()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            Match.Defeat(true);
            Assert.That(_inspection.Begin(PostMatchInspection.OpponentRobot), Is.True);
            _inspection.SetInterfaceHidden(true);

            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            yield return null;
            Assert.That(_inspection.IsActive, Is.False, "inspection ends with the result screen");
            Assert.That(_inspection.IsInterfaceHidden, Is.False);
            Assert.That(CameraRig.ControlsVisible, Is.True);
            Assert.That(Match.GetComponent<MainMenuScreen>().enabled, Is.True);
            Assert.That(Object.FindObjectsByType<Transform>().Any(t => t.name.StartsWith("Robot Inspection")), Is.False);
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform));
        }

        [UnityTest]
        public IEnumerator OpenSettingsBlockTheHideShortcut()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            Match.Defeat(false);
            Assert.That(_inspection.Begin(PostMatchInspection.PlayerRobot), Is.True);
            var settings = Match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            Assert.That(_inspection.ProcessToggleKey(), Is.False);
            Assert.That(_inspection.IsInterfaceHidden, Is.False);
            Assert.That(CameraRig.AcceptsPointerInput, Is.False, "no orbit while settings are open");
            settings.ToggleSettings();
            Assert.That(_inspection.ProcessToggleKey(), Is.True);
            _inspection.Stop();
            yield return null;
        }
    }
}
