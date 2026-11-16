using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class ArenaAuthoringTests
    {
        private const string ScenePath="Assets/Scenes/Gameplay/MainArena.unity";
        [Test] public void SixRolesGenerateIdempotentlyAndPreserveExternalReferencesAfterReopen()
        {
            EditorSceneManager.OpenScene(ScenePath);
            string guid=AssetDatabase.AssetPathToGUID(ScenePath);
            var arena=Object.FindFirstObjectByType<ArenaEnvironment>();var match=arena.Match;var player=match.Player;var opponent=match.Opponent;var camera=Camera.main;
            var external=new GameObject("Arena Ownership Verification");external.transform.SetParent(arena.transform,false);var source=external.AddComponent<AudioSource>();
            var open=AssetDatabase.LoadAssetAtPath<ArenaLayout>("Assets/Settings/Arena/Open.asset");
            var tactical=AssetDatabase.LoadAssetAtPath<ArenaLayout>("Assets/Settings/Arena/Tactical.asset");
            try
            {
                foreach(var layout in new[]{open,tactical,open})
                {
                    arena.GenerateLayout(layout);int count=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
                    arena.GenerateLayout(layout);
                    Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,Is.EqualTo(count));
                    Assert.That(Object.FindObjectsByType<ArenaCell>(FindObjectsSortMode.None).Length,Is.EqualTo(64));
                    Assert.That(arena.Match,Is.SameAs(match));Assert.That(match.Player,Is.SameAs(player));Assert.That(match.Opponent,Is.SameAs(opponent));Assert.That(Camera.main,Is.SameAs(camera));Assert.That(source!=null,Is.True);
                    Assert.That(arena.Contents.GetComponentsInChildren<WarningBarrier>().Length,Is.EqualTo(1));
                    Assert.That(arena.Contents.GetComponentsInChildren<ConveyorSurface>().Length,Is.EqualTo(2));
                    Assert.That(arena.Contents.GetComponentsInChildren<Collider>().Count(c=>c.name=="Reinforced Perimeter"),Is.GreaterThan(0));
                }
                EditorSceneManager.SaveScene(arena.gameObject.scene);EditorSceneManager.OpenScene(ScenePath);
                arena=Object.FindFirstObjectByType<ArenaEnvironment>();match=arena.Match;
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath),Is.EqualTo(guid));
                Assert.That(match.Player!=null&&match.Opponent!=null&&Camera.main!=null,Is.True);
                Assert.That(GameObject.Find("Arena Ownership Verification").GetComponent<AudioSource>()!=null,Is.True);
                Assert.That(arena.Layout,Is.SameAs(open));Assert.That(arena.Layouts.Count,Is.EqualTo(2));
                Assert.That(Object.FindObjectsByType<ArenaCell>(FindObjectsSortMode.None).Length,Is.EqualTo(64));
            }
            finally
            {
                var sentinel=GameObject.Find("Arena Ownership Verification");if(sentinel!=null)Object.DestroyImmediate(sentinel);
                arena=Object.FindFirstObjectByType<ArenaEnvironment>();arena.GenerateLayout(open);EditorSceneManager.SaveScene(arena.gameObject.scene);
            }
        }
        [Test] public void InvalidGenerationDoesNotTouchCurrentScene()
        {
            EditorSceneManager.OpenScene(ScenePath);var arena=Object.FindFirstObjectByType<ArenaEnvironment>();
            var contents=arena.Contents;var selected=arena.Layout;int count=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            var invalid=ScriptableObject.CreateInstance<ArenaLayout>();invalid.CreateDefault(false);
            invalid.cells.Find(c=>c.spawn==ArenaSpawn.Player).content=ArenaContent.Hazard;
            try
            {
                Assert.Throws<System.InvalidOperationException>(()=>arena.GenerateLayout(invalid));
                Assert.That(arena.Contents,Is.SameAs(contents));Assert.That(arena.Layout,Is.SameAs(selected));Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,Is.EqualTo(count));
            }
            finally{Object.DestroyImmediate(invalid);}
        }
        [Test] public void SharedBuilderPreservesCustomLayoutChoice()
        {
            EditorSceneManager.OpenScene(ScenePath);var arena=Object.FindFirstObjectByType<ArenaEnvironment>();
            var original=arena.Layouts.ToArray();var selected=arena.Layout;
            var custom=ScriptableObject.CreateInstance<ArenaLayout>();custom.CreateDefault(false);
            try
            {
                arena.GenerateLayout(custom);
                arena.Configure(original.Concat(new[]{custom}).ToArray(),arena.Match,GameObject.Find("Player Spawn").transform,GameObject.Find("Opponent Spawn").transform,arena.Contents.GetComponentInChildren<Renderer>().sharedMaterial);
                Assert.That(arena.Layout,Is.SameAs(custom));
            }
            finally
            {
                // Reopen the saved production scene instead of retaining a transient test asset.
                Object.DestroyImmediate(custom);EditorSceneManager.OpenScene(ScenePath);
            }
        }
    }
}
