using NUnit.Framework;
using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class WarningBarrierTests
    {
        private GameObject root;
        private Material material;
        private WarningBarrier barrier;
        [SetUp] public void Setup()
        {
            root=new GameObject("Barrier Test Fixture");root.transform.position=new Vector3(0,.82f,0);
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            barrier=root.AddComponent<WarningBarrier>();barrier.Configure(null,material);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
        [Test] public void WarningAlwaysPrecedesPhysicalRiseAndThreeCyclesComplete()
        {
            bool warned=false;float warningTime=0;
            for(int step=0;step<4000 && barrier.CompletedCycles<3;step++)
            {
                if(barrier.Phase==BarrierPhase.Warning){warned=true;warningTime+=.01f;}
                barrier.Advance(.01f);
                if(barrier.Phase==BarrierPhase.Rising)Assert.That(warned&&warningTime>=barrier.WarningSeconds-.02f,Is.True);
            }
            Assert.That(barrier.CompletedCycles,Is.EqualTo(3));
            barrier.ResetCycle();
            Assert.That(barrier.Phase,Is.EqualTo(BarrierPhase.Idle));Assert.That(barrier.Elapsed,Is.Zero);
            Assert.That(barrier.BarrierCollider.enabled,Is.False);
        }
        [TestCase(0f)] [TestCase(.9f)]
        public void OccupancyDefersAtCentreAndEdge(float x)
        {
            var robot=new GameObject("Occupancy Test Fixture");
            try
            {
                robot.AddComponent<RobotPrototypeController>();
                robot.AddComponent<BoxCollider>().size=Vector3.one;
                robot.transform.position=root.transform.position+new Vector3(x,.25f,0);
                Physics.SyncTransforms();Assert.That(barrier.IsOccupied(),Is.True);
                for(int step=0;step<600;step++){barrier.Advance(.01f);Assert.That(barrier.BarrierCollider.enabled,Is.False);}
                Assert.That(barrier.DeferredCycles,Is.GreaterThan(0));
            }
            finally{Object.DestroyImmediate(robot);}
        }
    }
}
