using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        [UnityTest]
        public IEnumerator BothModularWeaponsLockAgainstOppositeBotAndTriggerVenue()
        {
            var workshop = Selection;
            var venue = match.GetComponent<ArenaVenue>();
            foreach (string id in new[] {"fork", "spinner"})
            {
                Assert.That(menu.SelectPreset(DuelSetup.Quick()));
                Assert.That(menu.OpenWorkshop());
                var pick = WorkshopLoadout.Default;
                pick.weaponId = id;
                pick.armorId = "heavy";
                Assert.That(menu.SetWorkshopDraft(pick));
                Assert.That(menu.ApplyWorkshop());
                menu.StartLobbyMatch();
                Assert.That(match.State,Is.EqualTo(MatchController.MatchState.Countdown));
                Assert.That(match.LockedSetup.PlayerRobotId,Is.EqualTo("workshop/standard/soft/heavy/" + id));
                Assert.That(match.LockedSetup.OpponentRobotId,Is.EqualTo(id == "fork" ? "spinner" : "fork"));
                Assert.That(menu.OpenWorkshop(), Is.False);
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting);
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                Assert.That(match.Opponent.GetComponent<MeleeAttack>().Profile.damage, Is.EqualTo(id == "fork" ? 25 : 18));
                match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000);
                Assert.That(match.EncounterComplete);
                string oldId = match.EncounterId;
                menu.ContinueResult();
                Assert.That(match.EncounterId,Is.Not.EqualTo(oldId));
                Assert.That(match.LockedSetup.PlayerRobotId,Is.EqualTo(workshop.ModularRobotId));
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000);
                match.Prepare();
            }
        }
    }
}
