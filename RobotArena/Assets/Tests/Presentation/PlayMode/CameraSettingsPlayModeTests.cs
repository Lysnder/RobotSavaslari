using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// The persistent camera settings change the real MainArena camera. In-memory values are restored
    /// after each test; the panel is only closed (which saves) after the originals are put back.
    /// </summary>
    public sealed class CameraSettingsPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator UnloadArena() => MainArenaTestScene.Unload();

        private float _horizontal, _vertical, _smoothing, _distance;
        private bool _invert;

        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;

        [SetUp]
        public void Snapshot()
        {
            _horizontal = GameSettings.CameraHorizontalSensitivity;
            _vertical = GameSettings.CameraVerticalSensitivity;
            _invert = GameSettings.CameraInvertY;
            _smoothing = GameSettings.CameraSmoothing;
            _distance = GameSettings.CameraDefaultDistance;
        }

        [TearDown]
        public void Restore()
        {
            RestoreValues();
            Time.timeScale = 1f;
        }

        private void RestoreValues()
        {
            GameSettings.CameraHorizontalSensitivity = _horizontal;
            GameSettings.CameraVerticalSensitivity = _vertical;
            GameSettings.CameraInvertY = _invert;
            GameSettings.CameraSmoothing = _smoothing;
            GameSettings.CameraDefaultDistance = _distance;
        }

        [UnityTest]
        public IEnumerator CameraStartsAtSavedDefaultDistance()
        {
            GameSettings.CameraDefaultDistance = 5f;
            yield return MainArenaTestScene.Load();
            Assert.That(CameraRig.Distance, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(CameraRig.ViewDistance, Is.EqualTo(5f).Within(1e-4f), "first pose is placed at the saved distance");

            CameraRig.ApplyZoomInput(-1f);
            CameraRig.ResetView();
            Assert.That(CameraRig.Distance, Is.EqualTo(5f).Within(1e-4f), "reset returns to the saved distance");
        }

        [UnityTest]
        public IEnumerator SensitivityAndInvertChangeOrbitResponse()
        {
            yield return MainArenaTestScene.Load();
            GameSettings.CameraHorizontalSensitivity = 1f;
            float yaw = CameraRig.Yaw;
            CameraRig.ApplyOrbitInput(new Vector2(10f, 0f));
            float normal = Mathf.DeltaAngle(yaw, CameraRig.Yaw);

            GameSettings.CameraHorizontalSensitivity = 2f;
            yaw = CameraRig.Yaw;
            CameraRig.ApplyOrbitInput(new Vector2(10f, 0f));
            Assert.That(Mathf.DeltaAngle(yaw, CameraRig.Yaw), Is.EqualTo(normal * 2f).Within(1e-3f), "horizontal sensitivity");

            CameraRig.SetOrbit(CameraRig.Yaw, 40f, CameraRig.Distance);
            GameSettings.CameraVerticalSensitivity = 1f;
            GameSettings.CameraInvertY = false;
            CameraRig.ApplyOrbitInput(new Vector2(0f, 10f));
            float upNormal = CameraRig.Pitch - 40f;

            CameraRig.SetOrbit(CameraRig.Yaw, 40f, CameraRig.Distance);
            GameSettings.CameraInvertY = true;
            CameraRig.ApplyOrbitInput(new Vector2(0f, 10f));
            float upInverted = CameraRig.Pitch - 40f;
            Assert.That(upInverted, Is.EqualTo(-upNormal).Within(1e-3f), "invert Y flips the vertical direction");

            CameraRig.SetOrbit(CameraRig.Yaw, 40f, CameraRig.Distance);
            GameSettings.CameraInvertY = false;
            GameSettings.CameraVerticalSensitivity = 0.5f;
            CameraRig.ApplyOrbitInput(new Vector2(0f, 10f));
            Assert.That(CameraRig.Pitch - 40f, Is.EqualTo(upNormal * 0.5f).Within(1e-3f), "vertical sensitivity");
        }

        [UnityTest]
        public IEnumerator SmoothingControlsHowFastTheViewCatchesUp()
        {
            yield return MainArenaTestScene.Load();
            GameSettings.CameraSmoothing = 0f;
            CameraRig.SetOrbit(CameraRig.Yaw + 60f, CameraRig.Pitch, CameraRig.Distance);
            yield return null;
            Assert.That(Mathf.DeltaAngle(CameraRig.ViewYaw, CameraRig.Yaw), Is.EqualTo(0f).Within(1e-3f), "no smoothing: instant");

            GameSettings.CameraSmoothing = 1f;
            CameraRig.SetOrbit(CameraRig.Yaw + 60f, CameraRig.Pitch, CameraRig.Distance);
            yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(CameraRig.ViewYaw, CameraRig.Yaw)), Is.GreaterThan(1f), "full smoothing eases in");
            yield return new WaitForSecondsRealtime(3f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(CameraRig.ViewYaw, CameraRig.Yaw)), Is.LessThan(0.5f), "and still arrives");
        }

        [UnityTest]
        public IEnumerator DistanceChangeInPausedSettingsPreviewsWithoutResumingGameplay()
        {
            yield return MainArenaTestScene.Load();
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);

            var settings = Match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings(); // Loads the saved values and pauses.
            Assert.That(Time.timeScale, Is.Zero);
            float target = Mathf.Approximately(GameSettings.CameraDefaultDistance, 12f) ? 6f : 12f;
            GameSettings.CameraDefaultDistance = target;
            yield return new WaitForSecondsRealtime(1.5f);

            Assert.That(Time.timeScale, Is.Zero, "the camera never resumes gameplay");
            Assert.That(CameraRig.Distance, Is.EqualTo(target).Within(1e-4f));
            Assert.That(CameraRig.ViewDistance, Is.EqualTo(target).Within(0.1f), "camera eased on unscaled time while paused");

            RestoreValues();
            settings.ToggleSettings();
            Assert.That(Time.timeScale, Is.EqualTo(1f), "settings panel still owns pause/resume");
        }

        [UnityTest]
        public IEnumerator MatchResetDoesNotChangeCameraSettings()
        {
            GameSettings.CameraHorizontalSensitivity = 1.4f;
            GameSettings.CameraInvertY = true;
            GameSettings.CameraSmoothing = 0.3f;
            GameSettings.CameraDefaultDistance = 9.5f;
            yield return MainArenaTestScene.Load();
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            Match.Defeat(false);
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            Match.Prepare();

            Assert.That(GameSettings.CameraHorizontalSensitivity, Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(GameSettings.CameraInvertY, Is.True);
            Assert.That(GameSettings.CameraSmoothing, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(GameSettings.CameraDefaultDistance, Is.EqualTo(9.5f).Within(1e-5f));
        }
    }
}
