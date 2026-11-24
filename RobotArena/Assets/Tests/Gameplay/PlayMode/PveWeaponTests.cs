using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        [UnityTest, Timeout(480000)]
        public IEnumerator FiveNaturalToolDuels()
        {
            for (int i = 2; i < WorkshopWeapons.Count; i++)
            {
                var loadout = Parts("hard", "heavy", WorkshopWeapons.Id(i));
                Apply(loadout);
                yield return StartDuel(DuelSetup.Quick());
                yield return PilotDuel(loadout, checkBothTools: true);
                yield return null;
                match.Prepare();
                yield return null;
                CheckToolArchive(loadout.weaponId);
            }
        }

        private void CheckToolArchive(string playerId)
        {
            Assert.That(Selection.SelectStock(false), Is.True);
            var archive = match.GetComponent<MatchReplayArchivePlayer>();
            var file = archive.Store.List().First();
            Assert.That(archive.OpenArchiveMode(), Is.True, archive.LastError);
            try
            {
                Assert.That(archive.Open(file.Path), Is.True, archive.LastError);
                foreach (string id in new[] { playerId, WorkshopWeapons.Pair(playerId) })
                {
                    string suffix = "/" + WorkshopWeapons.Part(id);
                    int track = archive.Info.Tracks.FindIndex(entry => !entry.Wear && entry.Key.EndsWith(suffix, StringComparison.Ordinal));
                    Assert.That(track, Is.GreaterThanOrEqualTo(0), suffix);
                    var mesh = archive.GetProxy(track).GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.vertexCount, Is.EqualTo(archive.Info.Tracks[track].MeshSignature));
                }
                Assert.That(match.EquippedPlayerId, Is.EqualTo(DuelSetup.Spinner), "archive preserved the live selection");
            }
            finally { archive.CloseArchiveMode(); }
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator FiveNewToolsInBothProductionSlotsAndRematches()
        {
            for (int i = 2; i < WorkshopWeapons.Count; i++)
            {
                string id = WorkshopWeapons.Id(i);
                var loadout = Parts("hard", "heavy", id);
                Apply(loadout);
                var store = new WorkshopStore(Path.Combine(temporary, "workshop.json"));
                Assert.That(store.Load().lastModular, Is.EqualTo(loadout));
                Assert.That(Selection.UseStore(store), Is.True);
                yield return StartDuel(DuelSetup.Quick());
                var start = match.LockedSetup;
                string botId = DuelSetup.OtherRobot(start.PlayerRobotId);
                Assert.That(start.PlayerRobotId, Does.EndWith("/" + id));
                Assert.That(start.OpponentRobotId, Is.EqualTo(botId));
                Assert.That(Selection.TryApply(WorkshopLoadout.Default), Is.False);
                ProbeTool(match.Player, match.Opponent, id, false);
                // Complete the fixture's first encounter, then exercise the actual result-screen action.
                match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000);
                yield return null;
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                menu.ContinueResult();
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
                Assert.That(menu.OpenWorkshop(), Is.False);
                Assert.That(match.LockedSetup.EncounterId, Is.Not.EqualTo(start.EncounterId));
                Assert.That(match.LockedSetup.PlayerRobotId, Is.EqualTo(start.PlayerRobotId));
                Assert.That(match.LockedSetup.OpponentRobotId, Is.EqualTo(botId));
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                ProbeTool(match.Opponent, match.Player, botId, true);
                FinishFixtureAndPrepare();
                yield return null;
            }
        }

        private void FinishFixtureAndPrepare()
        {
            if (match.State == MatchController.MatchState.Playing)
                match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000);
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
            match.Prepare();
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Ready));
        }

        // The fixture lines up a repeatable contact inside a live production round.
        // Only the attack clock is advanced. The bot must request its own strike through its real controller.
        private void ProbeTool(RobotPrototypeController owner, RobotPrototypeController target, string id, bool bot)
        {
            var attack = owner.GetComponent<MeleeAttack>();
            var health = target.GetComponent<RobotHealth>();
            Assert.That(attack.Profile.name, Is.EqualTo(id));
            Assert.That(owner.GetComponent<RobotDamageVisuals>().Parts.Single(p => p.Kind == RobotDamageVisuals.PartKind.Weapon).Name,
                Is.EqualTo(WorkshopWeapons.Part(id)));
            GameplayTestScene.Place(owner, new Vector3(0, .82f, -.82f), false, Quaternion.identity);
            GameplayTestScene.Place(target, new Vector3(0, .82f, .82f), false, Quaternion.Euler(0, 180, 0));
            float now = Time.time;
            float hp = health.CurrentHealth;
            Vector3 point = Vector3.zero;
            int hits = 0;
            void Capture(RobotHit hit)
            {
                if (hit.Weapon && hit.Source == owner.GetComponent<RobotHealth>()) { hits++; point = hit.Point; }
            }
            health.Damaged += Capture;
            attack.Clock = () => now;
            try
            {
                Assert.That(attack.PhaseLabel, Is.EqualTo("Hazır"));
                if (bot)
                {
                    var pilot = owner.GetComponent<RobotBotController>();
                    pilot.Tick(Time.fixedDeltaTime);
                    Assert.That(pilot.AttackRequested, Is.True, id + " bot intent");
                    owner.TickCombatControls();
                }
                else
                {
                    var previous = Keyboard.current;
                    var keyboard = InputSystem.AddDevice<Keyboard>();
                    try
                    {
                        keyboard.MakeCurrent();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                        InputSystem.Update();
                        owner.TickCombatControls();
                    }
                    finally
                    {
                        InputSystem.RemoveDevice(keyboard);
                        if (previous != null && previous.added) previous.MakeCurrent();
                    }
                }
                Assert.That(attack.IsAttacking, Is.True, id);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var cue = GameplayTestScene.GetField<AudioClip>(attack, "_toolCue");
                    Assert.That(cue, Is.Not.Null);
                    Assert.That(cue.name, Is.EqualTo(attack.Profile.style + " servo"));
                    Assert.That(cue.samples, Is.GreaterThan(1000));
                }
                if (attack.Profile.windup > 0)
                {
                    Assert.That(attack.WindingUp, Is.True);
                    Assert.That(attack.PhaseLabel, Is.EqualTo(id == "drum" ? "Devir alıyor" : "Hazırlanıyor"));
                    Assert.That(health.CurrentHealth, Is.EqualTo(hp), "no early damage");
                    now += attack.Profile.windup + .01f;
                    attack.Tick();
                }
                Assert.That(hits, Is.EqualTo(1), id + " first contact");
                Assert.That(hp - health.CurrentHealth, Is.EqualTo(attack.Damage).Within(.01f));
                Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Cooldown));
                if (id == "hammer")
                    Assert.That(point.y, Is.GreaterThan(target.GetComponent<BoxCollider>().bounds.center.y));
                if (id == "clamp")
                {
                    Assert.That(attack.HoldingTarget, Is.True);
                    Assert.That(attack.PhaseLabel, Is.EqualTo("Kavradı"));
                    Assert.That(owner.GetComponent<FixedJoint>().connectedBody, Is.SameAs(target.GetComponent<Rigidbody>()));
                    now += .65f;
                    attack.Tick();
                    Assert.That(attack.HoldingTarget, Is.False, "timed release");
                }
                if (id == "saw")
                {
                    now += .1f;
                    attack.Tick();
                    Assert.That(hits, Is.EqualTo(1), "interval bound");
                    now += .10f;
                    attack.Tick();
                    Assert.That(hits, Is.EqualTo(2), "sustained contact");
                    GameplayTestScene.Place(target, new Vector3(0, .82f, 4), false);
                    now += .02f;
                    attack.Tick();
                    Assert.That(attack.IsAttacking, Is.False);
                    Assert.That(hits, Is.EqualTo(2), "no damage after separation");
                }
                now += attack.ActiveDuration + .01f;
                attack.Tick();
                Assert.That(attack.PhaseLabel, Does.StartWith("Bekliyor"));
                TestContext.WriteLine($"PVE_TOOL {id} slot={(bot ? "bot" : "player")} hits={hits} hpPerHit={attack.Damage} windup={attack.Profile.windup} cooldown={attack.Cooldown}");
            }
            finally
            {
                health.Damaged -= Capture;
                attack.Clock = null;
            }
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator StrikeMotionLiftAndMissRecovery()
        {
            foreach (string id in new[] { "hammer", "drum", "flipper" })
            {
                Apply(Parts("soft", "heavy", id));
                yield return StartDuel(DuelSetup.Quick());
                var owner = match.Player;
                var target = match.Opponent;
                var attack = owner.GetComponent<MeleeAttack>();
                var tool = owner.GetComponent<RobotDamageVisuals>().Parts.Single(p => p.Kind == RobotDamageVisuals.PartKind.Weapon);
                // The bot remains in the match, but does not counterattack during this isolated effect measurement.
                target.GetComponent<RobotBotController>().enabled = false;
                GameplayTestScene.Place(owner, new Vector3(0, .82f, -.82f), false, Quaternion.identity);
                GameplayTestScene.Place(target, new Vector3(0, .82f, id == "hammer" ? 3.5f : .82f), false, Quaternion.Euler(0, 180, 0));
                Quaternion rest = tool.Transform.localRotation;
                var health = target.GetComponent<RobotHealth>();
                float hp = health.CurrentHealth;
                Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
                float deadline = Time.time + attack.Profile.windup + attack.ActiveDuration + .1f;
                bool moved = false;
                float maxUp = 0f;
                while (Time.time < deadline)
                {
                    yield return null;
                    moved |= Quaternion.Angle(rest, tool.Transform.localRotation) > 3f;
                    maxUp = Mathf.Max(maxUp, target.GetComponent<Rigidbody>().linearVelocity.y);
                }
                Assert.That(moved, Is.True, id + " visible weapon movement");
                if (id == "hammer")
                {
                    Assert.That(health.CurrentHealth, Is.EqualTo(hp), "miss must not deal damage");
                    Assert.That(attack.CooldownRemaining, Is.GreaterThan(.7f));
                }
                else
                {
                    Assert.That(hp - health.CurrentHealth, Is.GreaterThanOrEqualTo(attack.Damage - .01f), id);
                    Assert.That(maxUp, Is.GreaterThan(.32f), id + " upward motion");
                }
                TestContext.WriteLine($"PVE_TOOL_MOTION {id} maxUp={maxUp:F3} cooldown={attack.CooldownRemaining:F2}");
                FinishFixtureAndPrepare();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ClampReleasesOnDetachmentAndRoundEnd()
        {
            Apply(Parts("soft", "heavy", "clamp"));
            for (int i = 0; i < 2; i++)
            {
                yield return StartDuel(DuelSetup.Quick());
                var owner = match.Player;
                var target = match.Opponent;
                GameplayTestScene.Place(owner, new Vector3(0, .82f, -.82f), false, Quaternion.identity);
                GameplayTestScene.Place(target, new Vector3(0, .82f, .82f), false, Quaternion.Euler(0, 180, 0));
                var attack = owner.GetComponent<MeleeAttack>();
                float now = Time.time;
                attack.Clock = () => now;
                try
                {
                    Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
                    now += attack.Profile.windup + .01f;
                    attack.Tick();
                    Assert.That(attack.HoldingTarget, Is.True);
                    if (i == 0)
                    {
                        var visuals = owner.GetComponent<RobotDamageVisuals>();
                        var weapon = visuals.Parts.Single(p => p.Kind == RobotDamageVisuals.PartKind.Weapon);
                        var health = owner.GetComponent<RobotHealth>();
                        Vector3 pos = weapon.Transform.TransformPoint(weapon.Mesh.bounds.center);
                        health.TakeDamage(weapon.Threshold + 1f, pos, Vector3.forward, Vector3.zero, true, target.GetComponent<RobotHealth>());
                        Assert.That(visuals.WeaponOperational, Is.False, "real part detach");
                    }
                    else
                    {
                        target.GetComponent<RobotHealth>().TakeDamage(1000);
                        Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                    }
                    Assert.That(attack.HoldingTarget, Is.False);
                }
                finally { attack.Clock = null; }
                FinishFixtureAndPrepare();
                yield return null;
            }
        }
    }
}
