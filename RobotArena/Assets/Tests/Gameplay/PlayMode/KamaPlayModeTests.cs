using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Combat;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed class KamaPlayModeTests
    {
        private MatchController match;
        private PlayerRobotSelection panel;
        private MatchReplayArchiveStore archive;
        private string directory;

        [UnitySetUp]
        public IEnumerator Load()
        {
            directory = Path.Combine(Path.GetTempPath(), "RobotArenaKama", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            SceneManager.sceneLoaded += IsolateRecords;
            try { yield return GameplayTestScene.LoadMainArena(); }
            finally { SceneManager.sceneLoaded -= IsolateRecords; }
            match = GameplayTestScene.Match;
            panel = match.GetComponent<PlayerRobotSelection>();
            archive = new MatchReplayArchiveStore(Path.Combine(directory, "replays"));
            MainArenaPresentationInstaller.Install(match.gameObject.scene);
            Assert.That(MainArenaPresentationInstaller.UseArchiveStore(match, archive), Is.True);
            Assert.That(panel.CanSelect, Is.True);
            var arena = UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>();
            int open = arena.Layouts.ToList().FindIndex(layout => layout.name.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), "Open duel layout");
            Assert.That(arena.Select(open), Is.True);
        }

        private void IsolateRecords(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameplayTestScene.MainArena) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<MatchController>(true))
                    controller.HistoryStore = new MatchHistoryStore(Path.Combine(directory, "history.json"));
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            yield return GameplayTestScene.Unload();
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator NormalMenuTwoDuelsAndArchivedModelIdentity()
        {
            var owner = match.Player;
            var spinner = owner.GetComponent<MeleeAttack>().Profile;
            var collider = owner.GetComponent<BoxCollider>();
            var driver = owner.GetComponent<PlayerKeyboardDriver>();
            var visuals = owner.GetComponent<RobotDamageVisuals>();
            int parts = visuals.Parts.Count;
            var weapon = visuals.Parts.Single(p => p.Kind == RobotDamageVisuals.PartKind.Weapon);
            var original = weapon.Mesh;
            Assert.That(spinner.pushSpeed, Is.Zero);
            Assert.That(panel.ForkSelected, Is.False);
            var playback = match.GetComponent<MatchReplayArchivePlayer>();
            string historical = Environment.GetEnvironmentVariable("ROBOT_ARENA_LEGACY_REPLAY");
            if (!string.IsNullOrEmpty(historical))
            {
                Assert.That(playback.OpenArchiveMode(), Is.True, playback.LastError);
                Assert.That(playback.Open(historical), Is.True, playback.LastError);
                Assert.That(playback.IsActive, Is.True);
                playback.CloseArchiveMode();
            }
            yield return WinFromMenu(spinner);

            var oldRecording = archive.List().Single();
            Assert.That(playback.OpenArchiveMode(), Is.True, playback.LastError);
            Assert.That(playback.Open(oldRecording.Path), Is.True, playback.LastError);
            playback.CloseArchiveMode();
            float scale = MainMenuScreen.ReadyScale(Screen.width, Screen.height);
            ImmediateGui.Click(match.GetComponent<MainMenuScreen>(),
                new Vector2(Screen.width * .5f - 22f * scale, Screen.height - 100f * scale));
            Assert.That(panel.ForkSelected, Is.True);
            var fork = owner.GetComponent<MeleeAttack>().Profile;
            Assert.That(fork.range, Is.LessThan(spinner.range));
            Assert.That(fork.arcDegrees, Is.LessThan(spinner.arcDegrees));
            Assert.That(weapon.Mesh.triangles.Length / 3, Is.EqualTo(72));
            Assert.That(visuals.Parts.Sum(part => part.Mesh.triangles.Length / 3), Is.EqualTo(117032));
            foreach (Vector3 vertex in weapon.Mesh.vertices)
            {
                Vector3 local = owner.transform.InverseTransformPoint(weapon.Transform.TransformPoint(vertex));
                Assert.That(new Bounds(collider.center, collider.size).Contains(local), Is.True, "fork clearance " + local);
            }
            Assert.That(weapon.Mesh, Is.SameAs(weapon.Wear.GetComponent<MeshFilter>().sharedMesh));
            StringAssert.Contains("Uyumsuz", playback.CheckCompatibility(oldRecording.Info));
            yield return WinFromMenu(fork);

            var forkRecording = archive.List().Single(entry => entry.Info.PlayerName == owner.name);
            Assert.That(playback.OpenArchiveMode(), Is.True, playback.LastError);
            Assert.That(playback.Open(forkRecording.Path), Is.True, playback.LastError);
            playback.CloseArchiveMode();
            Assert.That(panel.SelectFork(false), Is.True);
            Assert.That(weapon.Mesh, Is.SameAs(original));
            Assert.That(owner.GetComponent<MeleeAttack>().Profile, Is.SameAs(spinner));
            Assert.That(match.Player, Is.SameAs(owner));
            Assert.That(owner.GetComponent<BoxCollider>(), Is.SameAs(collider));
            Assert.That(owner.GetComponent<PlayerKeyboardDriver>(), Is.SameAs(driver));
            Assert.That(visuals.Parts.Count, Is.EqualTo(parts));
            Assert.That(playback.CheckCompatibility(oldRecording.Info), Is.Null);
            StringAssert.Contains("Uyumsuz", playback.CheckCompatibility(forkRecording.Info));
        }

        private IEnumerator WinFromMenu(CombatProfile profile)
        {
            float scale = MainMenuScreen.ReadyScale(Screen.width, Screen.height);
            ImmediateGui.Click(match.GetComponent<MainMenuScreen>(), new Vector2(155, 218) * scale);
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(panel.SelectFork(!panel.ForkSelected), Is.False);
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            yield return null;
            Assert.That(match.Opponent.GetComponent<RobotBotController>().Target, Is.SameAs(match.Player.transform));

            GameplayTestScene.Place(match.Player, Vector3.up * .8f, false, Quaternion.identity);
            GameplayTestScene.Place(match.Opponent, new Vector3(0, .8f, 1.80f), false, Quaternion.identity);
            var attack = match.Player.GetComponent<MeleeAttack>();
            float clock = Time.time;
            attack.Clock = () => clock;
            var health = match.Opponent.GetComponent<RobotHealth>();
            int hits = profile.HitsToDefeat(health.MaxHealth);
            for (int i = 0; i < hits; i++)
            {
                Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
                Assert.That(health.CurrentHealth, Is.EqualTo(Mathf.Max(0f, health.MaxHealth - (i + 1) * profile.damage)).Within(.01f));
                if (i == 0)
                {
                    Assert.That(match.Opponent.GetComponent<Rigidbody>().linearVelocity.z, Is.EqualTo(profile.pushSpeed).Within(.01f));
                    Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Cooldown));
                }
                clock += profile.cooldown + .02f;
                attack.Tick();
            }
            attack.Clock = null;
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory));
            yield return null;
            match.Prepare();
            yield return null;
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Ready));
        }

        [UnityTest]
        public IEnumerator MissingKitKeepsTheOriginalPlayerAndProfile()
        {
            var owner = match.Player;
            var melee = owner.GetComponent<MeleeAttack>();
            var profile = melee.Profile;
            GameplayTestScene.SetField(panel, "kit", null);
            Assert.That(panel.SelectFork(true), Is.False);
            Assert.That(panel.ForkSelected, Is.False);
            Assert.That(melee.Profile, Is.SameAs(profile));
            Assert.That(match.Player, Is.SameAs(owner));
            Assert.That(owner.name, Is.EqualTo("Mavi Robot"));
            StringAssert.Contains("Döner", panel.Notice);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissDetachedWeaponAndRetry()
        {
            Assert.That(panel.SelectFork(true), Is.True);
            Assert.That(match.StartMatch(), Is.True);
            GameplayTestScene.Place(match.Player, new Vector3(-4, .8f, -3), false);
            GameplayTestScene.Place(match.Opponent, new Vector3(4, .8f, 3), false);
            var attack = match.Player.GetComponent<MeleeAttack>();
            float clock = Time.time;
            attack.Clock = () => clock;
            Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
            clock += attack.Cooldown + .1f;
            attack.Tick();
            Assert.That(attack.LastReport.Outcome, Is.EqualTo(AttackOutcome.Miss));
            var damage = match.Player.GetComponent<RobotDamageVisuals>();
            var weapon = damage.Parts.Single(p => p.Kind == RobotDamageVisuals.PartKind.Weapon);
            var point = weapon.Transform.position;
            match.Player.GetComponent<RobotHealth>().TakeDamage(weapon.Threshold + 1, point, Vector3.up, Vector3.zero, true);
            Assert.That(weapon.Detached, Is.True);
            Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.MatchClosed));
            match.Player.GetComponent<RobotHealth>().TakeDamage(1000);
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.OpponentVictory));
            yield return null;
            match.Prepare();
            Assert.That(damage.WeaponOperational, Is.True);
            Assert.That(panel.ForkSelected, Is.True);
            Assert.That(attack.CooldownRemaining, Is.Zero);
        }
    }
}
