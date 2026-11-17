using System;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Match;

namespace RobotSavaslari.Tests
{
    public sealed class MatchRulesTests
    {
        private string directory;
        private MatchHistoryStore store;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "RobotMatchHistory-" + Guid.NewGuid().ToString("N"));
            store = new MatchHistoryStore(Path.Combine(directory, "history.json"));
        }

        [TearDown]
        public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [TestCase(80, 100, 100, 200, MatchController.MatchOutcome.PlayerVictory)]
        [TestCase(50, 100, 150, 200, MatchController.MatchOutcome.OpponentVictory)]
        [TestCase(50, 100, 100, 200, MatchController.MatchOutcome.Draw)]
        public void TimeOverComparesHealthRatios(float health, float maximum, float other, float otherMaximum, MatchController.MatchOutcome expected)
        {
            Assert.That(MatchRules.CompareHealth(health, maximum, other, otherMaximum), Is.EqualTo(expected));
        }

        [TestCase(-1)] [TestCase(.5f)] [TestCase(3601)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTimeLimitsAreRejected(float seconds) => Assert.IsFalse(new MatchRules { timeLimitSeconds = seconds }.IsValid);

        [Test]
        public void HistorySurvivesANewStoreAndDoesNotDuplicateResults()
        {
            var record = Record("one");
            Assert.IsTrue(store.Append(record)); Assert.IsTrue(store.Append(record));
            var records = new MatchHistoryStore(store.FilePath).Load();
            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].rules.mode, Is.EqualTo(MatchMode.FirstToTwo));
            Assert.That(records[0].playerWins, Is.EqualTo(2));
            Assert.That(records[0].player.dealtDamage, Is.EqualTo(200f));
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
        }

        [Test]
        public void HistoryIsBoundedAndNewestFirst()
        {
            for (int i = 0; i < 25; i++) Assert.IsTrue(store.Append(Record(i.ToString())));
            var records = store.Load();
            Assert.That(records.Count, Is.EqualTo(MatchHistoryStore.Capacity));
            Assert.That(records[0].id, Is.EqualTo("24"));
            Assert.That(records[19].id, Is.EqualTo("5"));
        }

        [Test]
        public void CorruptAndFutureHistoryFailSafely()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, "not json"); Assert.IsEmpty(store.Load()); Assert.IsNotNull(store.LastError);
            File.WriteAllText(store.FilePath, "{\"version\":999,\"records\":[]}"); Assert.IsEmpty(store.Load());
            Assert.IsTrue(store.Append(Record("recovered")));
            Assert.That(store.Load().Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidStatisticsCannotOverwriteValidHistory()
        {
            Assert.IsTrue(store.Append(Record("safe")));
            var invalid = Record("bad"); invalid.player.dealtDamage = float.NaN;
            Assert.IsFalse(store.Append(invalid));
            Assert.That(store.Load()[0].id, Is.EqualTo("safe"));
        }

        [Test]
        public void FailedWriteIsReportedWithoutThrowing()
        {
            Directory.CreateDirectory(directory);
            var invalidPath = new MatchHistoryStore(directory);
            Assert.IsFalse(invalidPath.Append(Record("one"))); Assert.IsNotNull(invalidPath.LastError);
        }

        private static MatchRecord Record(string id) => new MatchRecord
        {
            id = id, endedAtUtc = DateTimeOffset.UtcNow.ToString("O"), rules = new MatchRules { mode = MatchMode.FirstToTwo, timeLimitSeconds = 60 },
            outcome = MatchController.MatchOutcome.PlayerVictory, playerWins = 2, roundsPlayed = 2, durationSeconds = 20,
            player = new RobotMatchStatistics { remainingHealth = 80, maximumHealth = 100, dealtDamage = 200, receivedDamage = 20 },
            opponent = new RobotMatchStatistics { remainingHealth = 0, maximumHealth = 100, dealtDamage = 20, receivedDamage = 200 }
        };
    }
}
