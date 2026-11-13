using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class ArenaGeometryTests
    {
        [Test]
        public void ProductionGridMatchesFloorPerimeterAndOriginalScale()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/MainArena.unity");
            var bounds = Object.FindFirstObjectByType<ArenaBounds>();
            Assert.That(bounds.GridSize, Is.EqualTo(new Vector2Int(8,8)));
            Assert.That(bounds.CellSize, Is.EqualTo(2));
            var cells = Object.FindObjectsByType<ArenaCell>(FindObjectsSortMode.None);
            Assert.That(cells.Length, Is.EqualTo(64));
            Assert.That(cells.Select(c=>c.Coordinate).Distinct().Count(), Is.EqualTo(64));
            foreach (var cell in cells)
            {
                Assert.That(cell.transform.localScale, Is.EqualTo(Vector3.one));
                var r = cell.GetComponentInChildren<Renderer>();
                var target = bounds.CellCenter(cell.Coordinate.x,cell.Coordinate.y);
                Assert.That(Vector2.Distance(new Vector2(r.bounds.center.x,r.bounds.center.z),new Vector2(target.x,target.z)), Is.LessThan(.01f));
                Assert.That(r.bounds.size.x, Is.InRange(1.98f,2.05f));
                Assert.That(r.bounds.size.z, Is.InRange(1.98f,2.05f));
            }
            var floor = GameObject.Find("Continuous Floor Collider").GetComponent<BoxCollider>();
            Assert.That(floor.bounds.size, Is.EqualTo(new Vector3(16,.2f,16)).Using(Vector3Comparer));
            Assert.That(floor.bounds.max.y, Is.EqualTo(.82f).Within(.001f));
            var match = Object.FindFirstObjectByType<MatchController>();
            foreach (var robot in new[]{match.Player,match.Opponent})
            {
                Assert.That(robot.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Mathf.Abs(robot.transform.position.x), Is.LessThan(6));
            }
            foreach (string name in new[]{"North Safety Barrier","South Safety Barrier","East Safety Barrier","West Safety Barrier"})
            {
                var wall = GameObject.Find(name).GetComponent<Collider>().bounds;
                Assert.That(Mathf.Max(Mathf.Abs(wall.center.x),Mathf.Abs(wall.center.z)), Is.EqualTo(8).Within(.001f));
            }
        }

        private static readonly System.Collections.Generic.IEqualityComparer<Vector3> Vector3Comparer = new ApproximateVectorComparer();
        private sealed class ApproximateVectorComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a,b)<.001f;
            public int GetHashCode(Vector3 a) => 0;
        }

        [Test]
        public void RebuildingKeepsSceneConnectionsAndObjectCount()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/MainArena.unity");
            var match = Object.FindFirstObjectByType<MatchController>();
            var player = match.Player;
            var camera = Camera.main;
            int count = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            RobotSavaslari.Editor.ArenaSceneBuilder.ExpandGrid(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            RobotSavaslari.Editor.ArenaSceneBuilder.ExpandGrid(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length, Is.EqualTo(count));
            Assert.That(match.Player, Is.SameAs(player));
            Assert.That(Camera.main, Is.SameAs(camera));
        }
    }
}
