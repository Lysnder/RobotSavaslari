using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// The standalone arena controller used by older test scenes: it spawns its own robots when the
    /// scene has none, ends the match on a defeat, restarts, and blocks selection behind its panels.
    /// Robot selection by mouse ray is checked on the same setup.
    /// </summary>
    public sealed class ArenaMatchControllerPlayModeTests : InputTestFixture
    {
        private ArenaMatchController _arena;
        private RobotHealth _red, _blue;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private IEnumerator CreateArena()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            var host = new GameObject("Arena");
            _arena = host.AddComponent<ArenaMatchController>();
            yield return null;
            // Selection collects the robots in its Start, so it is added once the arena has spawned them.
            host.AddComponent<RobotSelectionController>();
            yield return null;
            _red = GameObject.Find("Kirmizi Robot").GetComponent<RobotHealth>();
            _blue = GameObject.Find("Mavi Robot").GetComponent<RobotHealth>();
        }

        [UnityTest]
        public IEnumerator SpawnsRobotsAndPlacesTheCamera()
        {
            yield return CreateArena();
            Assert.That(_red, Is.Not.Null);
            Assert.That(_blue, Is.Not.Null);
            Assert.That(_red.GetComponent<RobotPrototypeController>().IsComputer, Is.True, "red is the bot");
            Assert.That(_blue.GetComponent<RobotPrototypeController>().IsComputer, Is.False);
            Assert.That(_red.GetComponent<RobotBotController>(), Is.Not.Null);
            Assert.That(RobotPrototypeController.Selected, Is.EqualTo(_blue.GetComponent<RobotPrototypeController>()));
            Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(0f, 14f, -10f)));
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(50f));
            Assert.That(_arena.MatchFinished, Is.False);
            ImmediateGui.Draw(_arena);
        }

        [UnityTest]
        public IEnumerator DefeatEndsTheMatchAndRestartRestoresBothRobots()
        {
            yield return CreateArena();
            _blue.TakeDamage(20f);
            _red.TakeDamage(1000f);
            Assert.That(_arena.MatchFinished, Is.True);
            Assert.That(_red.GetComponent<RobotPrototypeController>().CombatStopped, Is.True);
            Assert.That(_blue.GetComponent<RobotPrototypeController>().CombatStopped, Is.True);
            Assert.That(_arena.BlocksRobotSelection(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True,
                "result panel blocks selection");
            ImmediateGui.Draw(_arena);

            var panel = new Rect((Screen.width - 360f) / 2f, (Screen.height - 150f) / 2f, 360f, 150f);
            ImmediateGui.Click(_arena, new Rect(panel.x + 80f, panel.y + 82f, 200f, 42f).center);
            Assert.That(_arena.MatchFinished, Is.False, "restart button");
            Assert.That(_red.CurrentHealth, Is.EqualTo(_red.MaxHealth));
            Assert.That(_blue.CurrentHealth, Is.EqualTo(_blue.MaxHealth));
            Assert.That(_red.GetComponent<RobotPrototypeController>().CombatStopped, Is.False);

            _arena.RestartMatch();
            Assert.That(_arena.MatchFinished, Is.False);
        }

        [UnityTest]
        public IEnumerator BothRobotsDefeatedIsADraw()
        {
            yield return CreateArena();
            // The second defeat arrives after the first already stopped the match.
            _blue.TakeDamage(1000f);
            _red.TakeDamage(1000f);
            Assert.That(_arena.MatchFinished, Is.True);
            ImmediateGui.Draw(_arena);
        }

        [UnityTest]
        public IEnumerator PanelsBlockSelectionOnlyWhereTheyAre()
        {
            yield return CreateArena();
            Assert.That(_arena.BlocksRobotSelection(new Vector2(50f, Screen.height - 120f)), Is.True, "left robot panel");
            Assert.That(_arena.BlocksRobotSelection(new Vector2(Screen.width - 50f, Screen.height - 120f)), Is.True, "right robot panel");
            Assert.That(_arena.BlocksRobotSelection(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.False);
        }

        [UnityTest]
        public IEnumerator RaySelectsThePlayerRobotButNeverTheBot()
        {
            yield return CreateArena();
            var selection = _arena.GetComponent<RobotSelectionController>();
            RobotPrototypeController blue = _blue.GetComponent<RobotPrototypeController>();
            RobotPrototypeController.Select(null);

            selection.SelectFromRay(new Ray(_red.transform.position + Vector3.up * 10f, Vector3.down));
            Assert.That(RobotPrototypeController.Selected, Is.Null, "the bot cannot be selected");
            selection.SelectFromRay(new Ray(new Vector3(0f, 10f, 30f), Vector3.down));
            Assert.That(RobotPrototypeController.Selected, Is.Null, "empty floor");
            selection.SelectFromRay(new Ray(_blue.transform.position + Vector3.up * 10f, Vector3.down));
            Assert.That(RobotPrototypeController.Selected, Is.EqualTo(blue));
        }

        [UnityTest]
        public IEnumerator MouseClickSelectsThroughTheMainCamera()
        {
            yield return CreateArena();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            RobotPrototypeController blue = _blue.GetComponent<RobotPrototypeController>();
            RobotPrototypeController.Select(null);
            // Centre of the view, clear of the robot panels in the top corners.
            GameplayTestScene.Place(_blue, new Vector3(0f, 1f, 0f), true);
            Vector3 screen = Camera.main.WorldToScreenPoint(_blue.transform.position);
            Assume.That(_arena.BlocksRobotSelection(screen), Is.False);
            Set(mouse.position, new Vector2(screen.x, screen.y));
            Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            Release(mouse.leftButton, queueEventOnly: true);
            yield return null;
            Assert.That(RobotPrototypeController.Selected, Is.EqualTo(blue));
        }
    }
}
