using System;
using NUnit.Framework;
using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class DuelContractTests
    {
        private const string Id = "d09ac15d119941d086c8c798a0c6e7ba";

        [TestCase("spinner", "fork")]
        [TestCase("fork", "spinner")]
        [TestCase("workshop/standard/hard/heavy/fork", "spinner")]
        public void ValidCatalogPairFormsASeparateStart(string player, string bot)
        {
            var lobby = DuelSetup.Quick();
            var start = lobby.ForEncounter(Id, player, bot);
            Assert.That(start.Valid);
            Assert.That(start.catalogRevision, Is.EqualTo(DuelSetup.CatalogRevision));
            Assert.That(lobby.version, Is.EqualTo(1));
            Assert.That(DuelSetup.Read(JsonUtility.ToJson(lobby)), Is.Not.Null);
            Assert.That(DuelSetup.Read(JsonUtility.ToJson(start)), Is.Null);
        }

        [TestCase(null, "fork")]
        [TestCase("future", "fork")]
        [TestCase("workshop/standard/soft/light/drum", "fork")]
        [TestCase("workshop/standard/soft/heavy", "fork")]
        [TestCase("spinner", "spinner")]
        [TestCase("fork", "unknown")]
        public void InvalidCatalogPairIsRejected(string player, string bot)
        {
            Assert.That(DuelSetup.Quick().ForEncounter(Id, player, bot).Valid, Is.False);
        }

        [Test]
        public void StartChecksRolesRevisionEncounterAndFiniteDuration()
        {
            var start = DuelSetup.Quick().ForEncounter(Id, "spinner", "fork");
            start.opponentRole = "player";
            Assert.That(start.Valid, Is.False);
            start.opponentRole = "bot";
            start.catalogRevision++;
            Assert.That(start.Valid, Is.False);
            start.catalogRevision = DuelSetup.CatalogRevision;
            start.encounterId = "previous game";
            Assert.That(start.Valid, Is.False);
            start.encounterId = Guid.NewGuid().ToString("N");
            start.seconds = float.NaN;
            Assert.That(start.Valid, Is.False);
        }
    }
}
