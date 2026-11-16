using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Duel framing on MainArena: both real robots stay in view near and far, the view never
    /// turns faster than its limit (no single-frame 180 degree flip) and the mode survives a new match.
    /// </summary>
    public sealed class DuelCameraPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator UnloadArena() => MainArenaTestScene.Unload();

        private Keyboard _keyboard;
        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Mouse>().MakeCurrent();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        /// <summary>
        /// Presses C and lets the camera read it within the same input update, then releases it.
        /// Coroutines resume after Update, so waiting a frame would miss wasPressedThisFrame.
        /// </summary>
        /// <summary>Taps C through the input queue so the camera's Update sees a fresh press.</summary>
        private IEnumerator PressCameraKey()
        {
            Press(_keyboard.cKey, queueEventOnly: true);
            yield return null;
            Release(_keyboard.cKey, queueEventOnly: true);
            yield return null;
        }

        private static void PlacePair(Vector2 player, Vector2 opponent)
        {
            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(player.x, MainArenaTestScene.Floor, player.y), true);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(opponent.x, MainArenaTestScene.Floor, opponent.y), true);
        }

        [UnityTest]
        public IEnumerator KeepsBothRobotsInFrameNearAndFar()
        {
            yield return MainArenaTestScene.Load();
            CameraRig.SetMode(CombatCameraController.CameraMode.Duel);
            var scenarios = new (string name, Vector2 player, Vector2 opponent)[]
            {
                ("close", new Vector2(-0.8f, 0f), new Vector2(0.8f, 0f)),
                ("opposite east-west edges", new Vector2(-4.8f, 0f), new Vector2(4.8f, 0f)),
                ("opposite north-south edges", new Vector2(0f, -3.8f), new Vector2(0f, 3.8f)),
                ("diagonal corners", new Vector2(-4.8f, -3.8f), new Vector2(4.8f, 3.8f)),
                ("other diagonal", new Vector2(4.8f, -3.8f), new Vector2(-4.8f, 3.8f)),
                ("opponent behind player at wall", new Vector2(-2f, 0f), new Vector2(-4.8f, 0.5f)),
            };
            foreach (var scenario in scenarios)
            {
                PlacePair(scenario.player, scenario.opponent);
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.That(CameraRig.IsFramingDuel, Is.True);
                MainArenaTestScene.AssertInView(Match.Player, scenario.name);
                MainArenaTestScene.AssertInView(Match.Opponent, scenario.name);
                MainArenaTestScene.AssertCameraClear(scenario.name);
                Assert.That(CameraRig.ViewDistance, Is.LessThanOrEqualTo(CameraRig.MaxDuelDistance));
            }
        }

        [UnityTest]
        public IEnumerator OpponentPassingBehindTurnsAtLimitedRateWithoutFlip()
        {
            yield return MainArenaTestScene.Load();
            CameraRig.SetMode(CombatCameraController.CameraMode.Duel);
            PlacePair(new Vector2(0f, 0f), new Vector2(3f, 0.3f));
            yield return new WaitForSecondsRealtime(1.5f);
            float startYaw = CameraRig.ViewYaw;

            // The opponent drives straight past and ends up behind the player: the axis flips 180 degrees.
            float previous = CameraRig.ViewYaw;
            float largestStep = 0f;
            float x = 3f;
            while (x > -3f)
            {
                float frameDelta = Time.unscaledDeltaTime; // the camera's LateUpdate later in this frame uses it
                x -= 3.5f * frameDelta;
                MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(x, MainArenaTestScene.Floor, 0.3f));
                yield return null;
                float step = Mathf.Abs(Mathf.DeltaAngle(previous, CameraRig.ViewYaw));
                float allowed = CameraRig.MaxDuelYawSpeed * Mathf.Min(frameDelta, CameraRig.MaxFrameDelta) + 0.01f;
                Assert.That(step, Is.LessThanOrEqualTo(allowed), "turn rate at x = " + x);
                Assert.That(float.IsNaN(CameraRig.ViewYaw), Is.False);
                largestStep = Mathf.Max(largestStep, step);
                previous = CameraRig.ViewYaw;
            }
            Assert.That(largestStep, Is.LessThan(90f), "no single-frame flip");

            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, CameraRig.ViewYaw)), Is.GreaterThan(150f),
                "the view does eventually turn to look past the player again");
            MainArenaTestScene.AssertInView(Match.Player, "after crossing");
            MainArenaTestScene.AssertInView(Match.Opponent, "after crossing");
        }

        [UnityTest]
        public IEnumerator OverlappingRobotsKeepPreviousDirection()
        {
            yield return MainArenaTestScene.Load();
            CameraRig.SetMode(CombatCameraController.CameraMode.Duel);
            PlacePair(new Vector2(0f, 0f), new Vector2(2f, 0f));
            yield return new WaitForSecondsRealtime(1.5f);
            float settled = CameraRig.ViewYaw;

            PlacePair(new Vector2(0f, 0f), new Vector2(0.01f, 0.01f));
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Assert.That(float.IsNaN(CameraRig.ViewYaw) || float.IsNaN(CameraRig.Pivot.x), Is.False);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(settled, CameraRig.ViewYaw)), Is.LessThan(1f), "direction held while overlapping");
            }
        }

        [UnityTest]
        public IEnumerator FramesOnlyTheMatchRobotsNotDebris()
        {
            yield return MainArenaTestScene.Load();
            CameraRig.SetMode(CombatCameraController.CameraMode.Duel);
            PlacePair(new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform));
            Assert.That(CameraRig.DuelOpponent, Is.EqualTo(Match.Opponent.transform));
            Vector3 pivot = CameraRig.Pivot;
            float distance = CameraRig.ViewDistance;

            // Debris is spawned at the scene root with the robot's name; it must not widen the frame.
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = Match.Opponent.name + " Debris Wheel_FL";
            debris.transform.position = new Vector3(-5f, 1f, 4f);
            debris.AddComponent<Rigidbody>().isKinematic = true;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(Vector3.Distance(pivot, CameraRig.Pivot), Is.LessThan(0.05f));
            Assert.That(CameraRig.ViewDistance, Is.EqualTo(distance).Within(0.05f));
            Object.Destroy(debris);
        }

        [UnityTest]
        public IEnumerator ModeToggleIsBlockedBySettingsAndPersistsIntoNextMatch()
        {
            yield return MainArenaTestScene.Load();
            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Follow));

            var settings = Match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            yield return PressCameraKey();
            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Follow), "C ignored while settings are open");
            settings.ToggleSettings();

            yield return PressCameraKey();
            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Duel), "C switches to duel");

            CameraRig.ApplyOrbitInput(new Vector2(200f, 0f));
            CameraRig.ApplyZoomInput(-1f);
            Assert.That(CameraRig.DuelYawOffset, Is.Not.Zero);
            Assert.That(CameraRig.DuelZoom, Is.GreaterThan(1f));
            CameraRig.ResetView();
            Assert.That(CameraRig.DuelYawOffset, Is.Zero);
            Assert.That(CameraRig.DuelZoom, Is.EqualTo(1f));

            Assert.That(Match.RequestCountdown(), Is.True);
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            Match.Defeat(false);
            Assert.That(Match.RequestCountdown(), Is.True, "second match");
            yield return null;
            yield return null;

            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Duel), "mode preference kept");
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform));
            Assert.That(CameraRig.DuelOpponent, Is.EqualTo(Match.Opponent.transform));
            Vector3 player = MainArenaTestScene.RenderBounds(Match.Player).center;
            Vector3 opponent = MainArenaTestScene.RenderBounds(Match.Opponent).center;
            float spawnAxis = CombatCameraFraming.PairAxisYaw(player, opponent, 0f, 0.1f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(spawnAxis, CameraRig.ViewYaw)), Is.LessThan(1f), "fresh spawn axis, no carried heading");
            MainArenaTestScene.AssertInView(Match.Player, "second match");
            MainArenaTestScene.AssertInView(Match.Opponent, "second match");

            yield return PressCameraKey();
            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Follow));
            MainArenaTestScene.AssertSingleCameraAndListener();
        }
    }
}
