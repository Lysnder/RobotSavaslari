using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Loads the production MainArena and drives the follow camera through a virtual mouse,
    /// arena corners and a second match. Real mouse feel is still checked by hand on Windows.
    /// </summary>
    public sealed class CombatCameraPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator UnloadArena() => MainArenaTestScene.Unload();

        private Mouse _mouse;
        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator InstallsOnceOnExistingCameraAcrossReloads()
        {
            yield return MainArenaTestScene.Load();
            MainArenaTestScene.AssertSingleCameraAndListener();
            Assert.That(CameraRig.GetComponent<Camera>().CompareTag("MainCamera"), Is.True);
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform));
            Assert.That(CameraRig.Mode, Is.EqualTo(CombatCameraController.CameraMode.Follow), "default mode");

            Assert.That(MainArenaPresentationInstaller.Install(SceneManager.GetActiveScene()), Is.SameAs(CameraRig));
            MainArenaTestScene.AssertSingleCameraAndListener();

            yield return MainArenaTestScene.Load();
            MainArenaTestScene.AssertSingleCameraAndListener();
            Assert.That(CameraRig.GetComponents<Rigidbody>(), Is.Empty);
            Assert.That(CameraRig.GetComponents<Collider>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RightMouseOrbitsWheelZoomsAndResetRestoresDefaults()
        {
            yield return MainArenaTestScene.Load();
            float yaw = CameraRig.Yaw, pitch = CameraRig.Pitch, distance = CameraRig.Distance;

            Set(_mouse.delta, new Vector2(40f, 0f), queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(CameraRig.Yaw, Is.EqualTo(yaw), "moving without the right button does not orbit");

            Press(_mouse.rightButton);
            Set(_mouse.delta, new Vector2(40f, 20f), queueEventOnly: true);
            yield return null; // the queued movement is applied before this frame's LateUpdate
            yield return null;
            Assert.That(Mathf.DeltaAngle(yaw, CameraRig.Yaw), Is.GreaterThan(1f), "horizontal drag orbits");
            Assert.That(CameraRig.Pitch, Is.Not.EqualTo(pitch), "vertical drag tilts");
            Release(_mouse.rightButton);

            Set(_mouse.scroll, new Vector2(0f, 120f), queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(CameraRig.Distance, Is.LessThan(distance), "wheel forward zooms in");

            for (int i = 0; i < 60; i++) CameraRig.ApplyZoomInput(1f);
            Assert.That(CameraRig.Distance, Is.EqualTo(CameraRig.MinDistance));
            for (int i = 0; i < 60; i++) CameraRig.ApplyOrbitInput(new Vector2(0f, 1000f));
            Assert.That(CameraRig.Pitch, Is.InRange(CameraRig.MinPitch, CameraRig.MaxPitch));

            CameraRig.ResetView();
            Assert.That(CameraRig.Yaw, Is.EqualTo(CameraRig.DefaultYaw));
            Assert.That(CameraRig.Pitch, Is.EqualTo(CameraRig.DefaultPitch));
            Assert.That(CameraRig.Distance, Is.EqualTo(CameraRig.DefaultDistance));
        }

        [UnityTest]
        public IEnumerator OpenSettingsBlockOrbitInput()
        {
            yield return MainArenaTestScene.Load();
            var settings = Match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            Assert.That(settings.IsOpen, Is.True);
            float yaw = CameraRig.Yaw, distance = CameraRig.Distance;

            Press(_mouse.rightButton);
            Set(_mouse.delta, new Vector2(60f, 0f));
            yield return null;
            Set(_mouse.scroll, new Vector2(0f, 120f), queueEventOnly: true);
            yield return null;
            yield return null;
            Release(_mouse.rightButton);

            Assert.That(CameraRig.Yaw, Is.EqualTo(yaw), "orbit while settings are open");
            Assert.That(CameraRig.Distance, Is.EqualTo(distance), "zoom while settings are open");
            settings.ToggleSettings();
            Assert.That(settings.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator StaysAboveFloorAndInsideWallsAtEveryCorner()
        {
            yield return MainArenaTestScene.Load();
            var corners = new[] { new Vector2(4.8f, 3.8f), new Vector2(-4.8f, 3.8f), new Vector2(4.8f, -3.8f), new Vector2(-4.8f, -3.8f) };
            foreach (Vector2 corner in corners)
            {
                MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(corner.x, MainArenaTestScene.Floor, corner.y));
                // Look from outside the corner: the orbit would pass through both walls.
                float outward = Mathf.Atan2(-corner.x, -corner.y) * Mathf.Rad2Deg;
                foreach (float distance in new[] { CameraRig.MaxDistance, CameraRig.MinDistance })
                {
                    CameraRig.SetOrbit(outward, CameraRig.MinPitch, distance);
                    yield return new WaitForSecondsRealtime(0.8f);
                    MainArenaTestScene.AssertCameraClear("corner " + corner + " distance " + distance);
                }
                // Facing a single wall head-on.
                CameraRig.SetOrbit(corner.x > 0f ? -90f : 90f, CameraRig.MinPitch, CameraRig.MaxDistance);
                yield return new WaitForSecondsRealtime(0.8f);
                MainArenaTestScene.AssertCameraClear("wall " + corner);
            }
        }

        [UnityTest]
        public IEnumerator SecondMatchRetargetsSpawnWithoutSweepOrDuplicate()
        {
            yield return MainArenaTestScene.Load();
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);

            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(4.5f, MainArenaTestScene.Floor, 3.5f));
            yield return new WaitForSecondsRealtime(0.6f);
            Vector3 farPivot = CameraRig.Pivot;

            Match.Defeat(false);
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(Match.RequestCountdown(), Is.True, "new match from the result screen");
            yield return null;
            yield return null;

            Vector3 spawnPivot = MainArenaTestScene.RenderBounds(Match.Player).center;
            Assert.That(Vector3.Distance(CameraRig.Pivot, spawnPivot), Is.LessThan(0.3f), "camera cut to the respawned robot");
            Assert.That(Vector3.Distance(CameraRig.Pivot, farPivot), Is.GreaterThan(2.5f), "camera is not left at the old position");
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform));

            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            Assert.That(Match.MatchCount, Is.EqualTo(2));
            MainArenaTestScene.AssertSingleCameraAndListener();
        }
    }
}
