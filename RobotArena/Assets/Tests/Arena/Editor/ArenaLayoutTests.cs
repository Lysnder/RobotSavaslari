using NUnit.Framework;
using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class ArenaLayoutTests
    {
        private ArenaLayout layout;
        [SetUp] public void Setup(){layout=ScriptableObject.CreateInstance<ArenaLayout>();layout.CreateDefault(false);}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(layout);}
        [TestCase(false)] [TestCase(true)]
        public void DefaultLayoutsHaveRobotWidthRoutes(bool tactical)
        {
            layout.CreateDefault(tactical);
            Assert.That(ArenaLayoutValidator.Validate(layout,1.6f),Is.Empty);
            Assert.That(ArenaLayoutValidator.FindRoute(layout,1.6f).Count,Is.GreaterThan(1));
        }
        [Test] public void TacticalRequiresADetour()
        {
            int open=ArenaLayoutValidator.FindRoute(layout,1.6f).Count;
            layout.CreateDefault(true);
            Assert.That(ArenaLayoutValidator.FindRoute(layout,1.6f).Count,Is.GreaterThan(open));
        }
        [Test] public void RejectsOutsideCoordinates(){layout.cells[0].coordinate=new Vector2Int(-1,0);Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Not.Empty);}
        [Test] public void RejectsOverlappingDefinitions(){layout.cells[0].coordinate=layout.cells[1].coordinate;Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Not.Empty);}
        [Test] public void RejectsMissingSpawn(){layout.cells.Find(c=>c.spawn==ArenaSpawn.Player).spawn=ArenaSpawn.None;Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Not.Empty);}
        [Test] public void RejectsObstructedSpawn(){layout.cells.Find(c=>c.spawn==ArenaSpawn.Player).content=ArenaContent.Obstacle;Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Not.Empty);}
        [Test] public void RejectsInteriorWallVariation(){layout.cells.Find(c=>c.coordinate==new Vector2Int(2,2)).wall=ArenaWall.Reinforced;Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Not.Empty);}
        [Test] public void RejectsDisconnectedArena()
        {
            foreach(var c in layout.cells) if(c.coordinate.x==3)c.content=ArenaContent.Obstacle;
            Assert.That(ArenaLayoutValidator.Validate(layout,1.6f),Is.Not.Empty);
        }
        [Test] public void ClearanceRejectsPointRobotOnlyPassage()
        {
            foreach(var c in layout.cells) if(c.coordinate.x==3&&c.coordinate.y!=3)c.content=ArenaContent.Obstacle;
            Assert.That(ArenaLayoutValidator.FindRoute(layout,.1f),Is.Not.Empty);
            Assert.That(ArenaLayoutValidator.FindRoute(layout,1.9f),Is.Empty);
        }
        [TestCase(0f,0f)] [TestCase(float.NaN,1f)] [TestCase(float.PositiveInfinity,0f)]
        public void RejectsInvalidConveyorDirection(float x,float y)
        {
            var cell=layout.cells.Find(c=>c.content==ArenaContent.Conveyor);cell.direction=new Vector2(x,y);
            Assert.That(ArenaLayoutValidator.Validate(layout,1),Has.Some.Contains("direction"));
        }
        [Test] public void FloorAndSpawnAreCompatibleButDuplicateSpawnIsRejected()
        {
            Assert.That(ArenaLayoutValidator.Validate(layout,1),Is.Empty);
            layout.cells[0].spawn=ArenaSpawn.Player;
            Assert.That(ArenaLayoutValidator.Validate(layout,1),Has.Some.Contains("distinct"));
        }
        [TestCase(ArenaContent.Obstacle)] [TestCase(ArenaContent.Hazard)] [TestCase(ArenaContent.Conveyor)]
        public void RejectsIncompatibleSpawnContent(ArenaContent content)
        {
            layout.cells.Find(c=>c.spawn==ArenaSpawn.Player).content=content;
            Assert.That(ArenaLayoutValidator.Validate(layout,1),Has.Some.Contains("Spawn requires"));
        }
        [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidRobotClearance(float radius)
        {
            Assert.That(ArenaLayoutValidator.Validate(layout,radius),Has.Some.Contains("radius"));
        }
    }
}
