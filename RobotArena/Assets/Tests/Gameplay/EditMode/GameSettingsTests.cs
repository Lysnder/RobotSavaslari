using NUnit.Framework;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>Audio and display settings: loading, clamping, saving and resetting. Camera values have their own tests.</summary>
    public sealed class GameSettingsTests
    {
        private const string VolumeKey = "Settings.MasterVolume";
        private const string FullscreenKey = "Settings.Fullscreen";
        private const string QualityKey = "Settings.QualityLevel";
        private static readonly string[] Keys = { VolumeKey, FullscreenKey, QualityKey };

        private readonly object[] _saved = new object[Keys.Length];
        private readonly bool[] _had = new bool[Keys.Length];
        private float _volume;
        private bool _fullscreen;
        private int _quality;

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                _had[i] = PlayerPrefs.HasKey(Keys[i]);
                _saved[i] = i == 0 ? (object)PlayerPrefs.GetFloat(Keys[i]) : PlayerPrefs.GetInt(Keys[i]);
            }
            _volume = GameSettings.MasterVolume;
            _fullscreen = GameSettings.Fullscreen;
            _quality = GameSettings.QualityLevel;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!_had[i]) PlayerPrefs.DeleteKey(Keys[i]);
                else if (i == 0) PlayerPrefs.SetFloat(Keys[i], (float)_saved[i]);
                else PlayerPrefs.SetInt(Keys[i], (int)_saved[i]);
            }
            PlayerPrefs.Save();
            GameSettings.MasterVolume = _volume;
            GameSettings.Fullscreen = _fullscreen;
            GameSettings.QualityLevel = _quality;
            AudioListener.volume = _volume;
        }

        [Test]
        public void LoadClampsVolumeAndQuality()
        {
            PlayerPrefs.SetFloat(VolumeKey, 4f);
            PlayerPrefs.SetInt(FullscreenKey, 0);
            PlayerPrefs.SetInt(QualityKey, 999);
            GameSettings.Load();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(1f));
            Assert.That(GameSettings.Fullscreen, Is.False);
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(QualitySettings.names.Length - 1));

            PlayerPrefs.SetFloat(VolumeKey, -2f);
            PlayerPrefs.SetInt(FullscreenKey, 1);
            PlayerPrefs.SetInt(QualityKey, -5);
            GameSettings.Load();
            Assert.That(GameSettings.MasterVolume, Is.Zero);
            Assert.That(GameSettings.Fullscreen, Is.True);
            Assert.That(GameSettings.QualityLevel, Is.Zero);
        }

        [Test]
        public void SaveWritesWhatLoadReadsBack()
        {
            GameSettings.MasterVolume = 0.35f;
            GameSettings.Fullscreen = false;
            GameSettings.QualityLevel = 0;
            GameSettings.Save();
            Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(0.35f));
            Assert.That(PlayerPrefs.GetInt(FullscreenKey), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(QualityKey), Is.Zero);

            GameSettings.MasterVolume = 1f;
            GameSettings.Load();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.35f));
            Assert.That(GameSettings.Fullscreen, Is.False);
        }

        [Test]
        public void ApplyPushesVolumeToTheListener()
        {
            GameSettings.MasterVolume = 0.25f;
            GameSettings.Fullscreen = Screen.fullScreen;
            GameSettings.QualityLevel = QualitySettings.GetQualityLevel();
            GameSettings.Apply();
            Assert.That(AudioListener.volume, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void ResetRestoresFullVolumeFullscreenAndBestQuality()
        {
            GameSettings.MasterVolume = 0.1f;
            GameSettings.Fullscreen = false;
            GameSettings.QualityLevel = 0;
            GameSettings.ResetToDefaults();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(1f));
            Assert.That(GameSettings.Fullscreen, Is.True);
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(QualitySettings.names.Length - 1));
        }
    }
}
