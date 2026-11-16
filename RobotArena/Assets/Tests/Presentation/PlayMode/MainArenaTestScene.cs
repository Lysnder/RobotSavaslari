using System.Collections;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>Shared loading, placement and visibility checks for presentation tests on MainArena.</summary>
    internal static class MainArenaTestScene
    {
        public const float Floor = 0.82f;
        // Top of the arena's safety barriers; below it the camera must stay inside the inner faces.
        public const float BarrierTop = 3.69f;
        public const float InnerHalfWidth = 5.91f;
        public const float InnerHalfDepth = 4.91f;

        public static MatchController Match { get; private set; }
        public static CombatCameraController Camera { get; private set; }

        /// <summary>
        /// Replaces every loaded scene with an empty one, so tests that build their own objects later
        /// do not end up inside the arena's colliders.
        /// </summary>
        public static IEnumerator Unload()
        {
            Time.timeScale = 1f;
            Scene empty = SceneManager.CreateScene("Presentation Test Cleanup " + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Match = null;
            Camera = null;
        }

        public static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(MainArenaPresentationInstaller.SceneName, LoadSceneMode.Single);
            yield return null; // MatchController.Start prepares the robots.
            yield return null; // First camera pose.
            Match = Object.FindAnyObjectByType<MatchController>();
            Camera = Object.FindAnyObjectByType<CombatCameraController>();
            Assert.That(Match, Is.Not.Null, "MainArena match");
            Assert.That(Match.IsInitialized, Is.True, "match initialised");
            Assert.That(Camera, Is.Not.Null, "camera controller installed on MainArena");
        }

        /// <summary>Starts a match through the countdown and waits until it is playing.</summary>
        public static IEnumerator StartPlaying()
        {
            Assert.That(Match.RequestCountdown(), Is.True, "countdown requested");
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
        }

        public static void AssertSingleCameraAndListener()
        {
            Assert.That(Object.FindObjectsByType<UnityEngine.Camera>().Count(c => c.isActiveAndEnabled), Is.EqualTo(1), "active cameras");
            Assert.That(Object.FindObjectsByType<AudioListener>().Count(l => l.isActiveAndEnabled), Is.EqualTo(1), "active listeners");
            Assert.That(Object.FindObjectsByType<CombatCameraController>().Length, Is.EqualTo(1), "camera controllers");
        }

        /// <summary>Moves a robot; <paramref name="freeze"/> makes it kinematic so scripted paths are not disturbed by contacts.</summary>
        public static void PlaceRobot(RobotPrototypeController robot, Vector3 position, bool freeze = false)
        {
            var body = robot.GetComponent<Rigidbody>();
            if (freeze) body.isKinematic = true;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = position;
            robot.transform.position = position;
            Physics.SyncTransforms();
        }

        public static Bounds RenderBounds(Component robot)
        {
            return robot.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.enabled && !r.forceRenderingOff)
                .Select(r => r.bounds)
                .Aggregate((a, b) => { a.Encapsulate(b); return a; });
        }

        public static bool IsStatic(Collider collider) => !collider.isTrigger && collider.attachedRigidbody == null;

        /// <summary>The camera is above the real floor, outside walls and has a clear line to its pivot.</summary>
        public static void AssertCameraClear(string label)
        {
            Vector3 position = Camera.transform.position;
            float radius = Camera.NearPlaneRadius;
            Assert.That(Camera.FloorHeight, Is.EqualTo(Floor).Within(0.02f), label + ": detected floor");
            Assert.That(position.y, Is.GreaterThanOrEqualTo(Floor + radius - 0.001f), label + ": above floor");
            Collider[] overlaps = Physics.OverlapSphere(position, radius * 0.95f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(overlaps.Where(IsStatic).Select(c => c.name), Is.Empty, label + ": near plane inside geometry");
            bool blocked = Physics.Linecast(Camera.Pivot, position, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && IsStatic(hit.collider);
            Assert.That(blocked, Is.False, label + ": wall between camera and pivot");
            if (position.y < BarrierTop)
            {
                Assert.That(Mathf.Abs(position.x), Is.LessThan(InnerHalfWidth), label + ": behind east/west wall");
                Assert.That(Mathf.Abs(position.z), Is.LessThan(InnerHalfDepth), label + ": behind north/south wall");
            }
        }

        /// <summary>Every corner of the robot's visible bounds projects inside the viewport.</summary>
        public static void AssertInView(Component robot, string label)
        {
            var view = Camera.GetComponent<UnityEngine.Camera>();
            Bounds bounds = RenderBounds(robot);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        Vector3 point = view.WorldToViewportPoint(corner);
                        Assert.That(point.z, Is.GreaterThan(view.nearClipPlane), label + ": " + robot.name + " behind camera");
                        Assert.That(point.x, Is.InRange(-0.01f, 1.01f), label + ": " + robot.name + " horizontally out of frame");
                        Assert.That(point.y, Is.InRange(-0.01f, 1.01f), label + ": " + robot.name + " vertically out of frame");
                    }
        }
    }
}
