using System.Collections.Generic;
using NUnit.Framework;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Camera values in <see cref="GameSettings"/>: safe defaults, clamping, corrupt data, save/load and
    /// a camera reset that leaves every other stored key alone. The developer's own saved settings
    /// are snapshotted and restored around each test.
    /// </summary>
    public sealed class CameraSettingsPersistenceTests
    {
        private const string ForeignKey = "Settings.Driving.Forward";
        private static readonly string[] FloatKeys =
        {
            "Settings.MasterVolume",
            GameSettings.CameraHorizontalSensitivityKey,
            GameSettings.CameraVerticalSensitivityKey,
            GameSettings.CameraSmoothingKey,
            GameSettings.CameraDefaultDistanceKey,
        };
        private static readonly string[] IntKeys = { "Settings.Fullscreen", "Settings.QualityLevel", GameSettings.CameraInvertYKey };
        private static readonly string[] CameraKeys =
        {
            GameSettings.CameraHorizontalSensitivityKey, GameSettings.CameraVerticalSensitivityKey,
            GameSettings.CameraInvertYKey, GameSettings.CameraSmoothingKey, GameSettings.CameraDefaultDistanceKey,
        };

        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
        private string _foreign;
        private float _volume;
        private bool _fullscreen;
        private int _quality;

        [SetUp]
        public void SnapshotPreferences()
        {
            _floats.Clear();
            _ints.Clear();
            foreach (string key in FloatKeys) if (PlayerPrefs.HasKey(key)) _floats[key] = PlayerPrefs.GetFloat(key);
            foreach (string key in IntKeys) if (PlayerPrefs.HasKey(key)) _ints[key] = PlayerPrefs.GetInt(key);
            _foreign = PlayerPrefs.HasKey(ForeignKey) ? PlayerPrefs.GetString(ForeignKey) : null;
            _volume = GameSettings.MasterVolume;
            _fullscreen = GameSettings.Fullscreen;
            _quality = GameSettings.QualityLevel;
        }

        [TearDown]
        public void RestorePreferences()
        {
            foreach (string key in FloatKeys)
                if (_floats.TryGetValue(key, out float value)) PlayerPrefs.SetFloat(key, value); else PlayerPrefs.DeleteKey(key);
            foreach (string key in IntKeys)
                if (_ints.TryGetValue(key, out int value)) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
            if (_foreign != null) PlayerPrefs.SetString(ForeignKey, _foreign); else PlayerPrefs.DeleteKey(ForeignKey);
            PlayerPrefs.Save();
            GameSettings.Load();
            GameSettings.MasterVolume = _volume;
            GameSettings.Fullscreen = _fullscreen;
            GameSettings.QualityLevel = _quality;
        }

        private static void DeleteCameraKeys()
        {
            foreach (string key in CameraKeys) PlayerPrefs.DeleteKey(key);
        }

        private static void AssertCameraDefaults()
        {
            Assert.That(GameSettings.CameraHorizontalSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity));
            Assert.That(GameSettings.CameraVerticalSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity));
            Assert.That(GameSettings.CameraInvertY, Is.False);
            Assert.That(GameSettings.CameraSmoothing, Is.EqualTo(GameSettings.DefaultCameraSmoothing));
            Assert.That(GameSettings.CameraDefaultDistance, Is.EqualTo(GameSettings.DefaultCameraDistance));
        }

        [Test]
        public void FirstLaunchWithoutSavedCameraValuesUsesDefaults()
        {
            DeleteCameraKeys();
            GameSettings.CameraHorizontalSensitivity = 2.5f;
            GameSettings.CameraInvertY = true;
            GameSettings.Load();
            AssertCameraDefaults();
        }

        [Test]
        public void OutOfRangeValuesAreClampedOnLoad()
        {
            PlayerPrefs.SetFloat(GameSettings.CameraHorizontalSensitivityKey, 50f);
            PlayerPrefs.SetFloat(GameSettings.CameraVerticalSensitivityKey, -4f);
            PlayerPrefs.SetFloat(GameSettings.CameraSmoothingKey, 3f);
            PlayerPrefs.SetFloat(GameSettings.CameraDefaultDistanceKey, 0.2f);
            GameSettings.Load();
            Assert.That(GameSettings.CameraHorizontalSensitivity, Is.EqualTo(GameSettings.CameraSensitivityMax));
            Assert.That(GameSettings.CameraVerticalSensitivity, Is.EqualTo(GameSettings.CameraSensitivityMin));
            Assert.That(GameSettings.CameraSmoothing, Is.EqualTo(GameSettings.CameraSmoothingMax));
            Assert.That(GameSettings.CameraDefaultDistance, Is.EqualTo(GameSettings.CameraDistanceMin));
        }

        [Test]
        public void CorruptValuesFallBackToDefaults()
        {
            PlayerPrefs.SetFloat(GameSettings.CameraHorizontalSensitivityKey, float.NaN);
            PlayerPrefs.SetFloat(GameSettings.CameraVerticalSensitivityKey, float.PositiveInfinity);
            PlayerPrefs.SetString(GameSettings.CameraSmoothingKey, "bozuk");
            PlayerPrefs.SetString(GameSettings.CameraDefaultDistanceKey, "on metre");
            PlayerPrefs.SetInt(GameSettings.CameraInvertYKey, 7);
            GameSettings.Load();
            AssertCameraDefaults();
        }

        [Test]
        public void SettersClampAndRejectInvalidNumbers()
        {
            GameSettings.CameraDefaultDistance = 100f;
            Assert.That(GameSettings.CameraDefaultDistance, Is.EqualTo(GameSettings.CameraDistanceMax));
            GameSettings.CameraSmoothing = -1f;
            Assert.That(GameSettings.CameraSmoothing, Is.EqualTo(GameSettings.CameraSmoothingMin));
            GameSettings.CameraHorizontalSensitivity = float.NaN;
            Assert.That(GameSettings.CameraHorizontalSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity));
        }

        [Test]
        public void SavedValuesSurviveReload()
        {
            GameSettings.CameraHorizontalSensitivity = 1.7f;
            GameSettings.CameraVerticalSensitivity = 0.4f;
            GameSettings.CameraInvertY = true;
            GameSettings.CameraSmoothing = 0.2f;
            GameSettings.CameraDefaultDistance = 11.5f;
            GameSettings.Save();

            GameSettings.ResetCameraToDefaults();
            AssertCameraDefaults();
            GameSettings.Load();

            Assert.That(GameSettings.CameraHorizontalSensitivity, Is.EqualTo(1.7f).Within(1e-5f));
            Assert.That(GameSettings.CameraVerticalSensitivity, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(GameSettings.CameraInvertY, Is.True);
            Assert.That(GameSettings.CameraSmoothing, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(GameSettings.CameraDefaultDistance, Is.EqualTo(11.5f).Within(1e-5f));
        }

        [Test]
        public void CameraResetKeepsAudioDisplayAndOtherPackagesKeys()
        {
            GameSettings.MasterVolume = 0.3f;
            GameSettings.QualityLevel = 0;
            PlayerPrefs.SetString(ForeignKey, "W");
            GameSettings.CameraDefaultDistance = 12f;
            GameSettings.CameraInvertY = true;

            GameSettings.ResetCameraToDefaults();
            GameSettings.Save();

            AssertCameraDefaults();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.3f));
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(0));
            Assert.That(PlayerPrefs.GetString(ForeignKey, null), Is.EqualTo("W"), "another package's key survives");
            foreach (string key in CameraKeys) Assert.That(PlayerPrefs.HasKey(key), Is.True, key + " written, not deleted");
        }

        [Test]
        public void FullResetAlsoRestoresCameraDefaults()
        {
            GameSettings.CameraSmoothing = 0.9f;
            GameSettings.CameraVerticalSensitivity = 2f;
            GameSettings.ResetToDefaults();
            AssertCameraDefaults();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(1f));
        }

        [Test]
        public void SmoothingSettingMapsToFollowSharpness()
        {
            Assert.That(CombatCameraFraming.SharpnessFromSmoothing(0f, 3f, 30f), Is.Zero, "0 = instant follow");
            Assert.That(CombatCameraFraming.SmoothingWeight(CombatCameraFraming.SharpnessFromSmoothing(0f, 3f, 30f), 0.016f), Is.EqualTo(1f));
            Assert.That(CombatCameraFraming.SharpnessFromSmoothing(1f, 3f, 30f), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(CombatCameraFraming.SharpnessFromSmoothing(0.5f, 3f, 30f), Is.EqualTo(16.5f).Within(1e-5f));
            Assert.That(CombatCameraFraming.SharpnessFromSmoothing(float.NaN, 3f, 30f), Is.Zero);
            Assert.That(CombatCameraFraming.SharpnessFromSmoothing(0.8f, 3f, 30f),
                Is.LessThan(CombatCameraFraming.SharpnessFromSmoothing(0.2f, 3f, 30f)), "more smoothing = softer");
        }
    }
}
