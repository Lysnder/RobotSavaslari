using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>Scene setup shared by the gameplay play mode tests.</summary>
    internal static class GameplayTestScene
    {
        public const string MainArena = "MainArena";
        public const float Floor = 0.82f;

        public static MatchController Match { get; private set; }

        /// <summary>Loads the production arena and waits until its match has prepared the robots.</summary>
        public static IEnumerator LoadMainArena()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(MainArena, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Match = Object.FindAnyObjectByType<MatchController>();
            Assert.That(Match, Is.Not.Null, "MainArena match");
            Assert.That(Match.IsInitialized, Is.True, "match initialised");
        }

        public static IEnumerator StartPlaying()
        {
            Assert.That(Match.RequestCountdown(), Is.True, "countdown requested");
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
        }

        /// <summary>Replaces the loaded scenes with an empty one that has a floor and a main camera.</summary>
        public static IEnumerator LoadEmptyScene(bool withFloor = true)
        {
            yield return Unload();
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            if (withFloor)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(60f, 1f, 60f);
            }
            yield return null;
        }

        /// <summary>
        /// Replaces every loaded scene with an empty one, so later tests that build their own objects
        /// never end up inside another test's arena.
        /// </summary>
        public static IEnumerator Unload()
        {
            Time.timeScale = 1f;
            Scene empty = SceneManager.CreateScene("Gameplay Test " + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Match = null;
        }

        /// <summary>Kinematic robots in opposite corners, so the bot cannot reach the player.</summary>
        public static void SeparateRobots()
        {
            Place(Match.Player, new Vector3(-4.5f, Floor, -3.5f), true);
            Place(Match.Opponent, new Vector3(4.5f, Floor, 3.5f), true);
        }

        public static void Place(Component robot, Vector3 position, bool kinematic, Quaternion? rotation = null)
        {
            var body = robot.GetComponent<Rigidbody>();
            body.isKinematic = kinematic;
            if (!kinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Quaternion facing = rotation ?? robot.transform.rotation;
            body.position = position;
            body.rotation = facing;
            robot.transform.SetPositionAndRotation(position, facing);
            Physics.SyncTransforms();
        }

        /// <summary>Sets a private serialized field, the way the Inspector would.</summary>
        public static void SetField(object target, string name, object value)
        {
            FieldInfo field = null;
            for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        public static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        /// <summary>Waits a number of physics steps.</summary>
        public static IEnumerator FixedSteps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>Snapshot of PlayerPrefs settings keys so UI tests can save freely.</summary>
        public sealed class SettingsSnapshot
        {
            private static readonly string[] FloatKeys =
            {
                "Settings.MasterVolume", "Settings.Camera.HorizontalSensitivity", "Settings.Camera.VerticalSensitivity",
                "Settings.Camera.Smoothing", "Settings.Camera.DefaultDistance",
            };
            private static readonly string[] IntKeys = { "Settings.Fullscreen", "Settings.QualityLevel", "Settings.Camera.InvertY" };
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public SettingsSnapshot()
            {
                foreach (string key in FloatKeys) if (PlayerPrefs.HasKey(key)) _values[key] = PlayerPrefs.GetFloat(key);
                foreach (string key in IntKeys) if (PlayerPrefs.HasKey(key)) _values[key] = PlayerPrefs.GetInt(key);
            }

            public void Restore()
            {
                foreach (string key in FloatKeys)
                    if (_values.TryGetValue(key, out object value)) PlayerPrefs.SetFloat(key, (float)value); else PlayerPrefs.DeleteKey(key);
                foreach (string key in IntKeys)
                    if (_values.TryGetValue(key, out object value)) PlayerPrefs.SetInt(key, (int)value); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                RobotSavaslari.UI.GameSettings.Load();
            }
        }
    }
}
