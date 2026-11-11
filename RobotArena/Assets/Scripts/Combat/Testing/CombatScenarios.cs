using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RobotSavaslari.Combat.Testing
{
    /// <summary>
    /// E1-E4 kabul vakalarinin tekrar oynanabilir senaryolari. Her senaryo gercek collider'larla
    /// yeni bir deneme duzenegi kurar, zamani elle ilerletir ve sonunda duzenegi siler.
    /// Hem deneme sahnesindeki dugmeden (Play Mode) hem de editor kontrolunden calistirilir.
    /// </summary>
    public static class CombatScenarios
    {
        public readonly struct Result
        {
            public readonly string Card;
            public readonly string Name;
            public readonly bool Passed;
            public readonly string Detail;

            public Result(string card, string name, bool passed, string detail)
            {
                Card = card;
                Name = name;
                Passed = passed;
                Detail = detail;
            }

            public override string ToString()
            {
                return (Passed ? "PASS" : "FAIL") + " [" + Card + "] " + Name + (string.IsNullOrEmpty(Detail) ? "" : " - " + Detail);
            }
        }

        private sealed class ScenarioFailure : Exception
        {
            public ScenarioFailure(string message) : base(message) { }
        }

        // Sahnedeki robotlardan uzakta calisir; sahne icerigini etkilemez.
        private static readonly Vector3 ScenarioCenter = new Vector3(1000f, 0f, 1000f);

        public static List<Result> RunAll()
        {
            var results = new List<Result>();
            CombatProfile p25 = CombatProfile.CreateRuntime("Profil 25", 25f);
            CombatProfile p40 = CombatProfile.CreateRuntime("Profil 40", 40f);
            try
            {
                // ---------------- E1: can ve tek yenilgi bildirimi ----------------
                Run(results, "E1", "100 cana 25 hasar yalniz hedefi 75'e dusurur", p25, rig =>
                {
                    rig.B.Health.TakeDamage(25f);
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    Expect(rig.A.Health.CurrentHealth, 100f, "diger robot can");
                    return "hedef 75, diger 100";
                });

                Run(results, "E1", "Kalan candan buyuk hasar 0 gosterir", p25, rig =>
                {
                    rig.B.Health.TakeDamage(60f);
                    rig.B.Health.TakeDamage(1000f);
                    Expect(rig.B.Health.CurrentHealth, 0f, "can");
                    return "can 0, eksiye inmedi";
                });

                Run(results, "E1", "Sifirdan sonra ek hasar ikinci yenilgi bildirimi uretmez", p25, rig =>
                {
                    rig.B.Health.TakeDamage(100f);
                    rig.B.Health.TakeDamage(25f);
                    rig.B.Health.TakeDamage(1000f);
                    Expect(rig.B.DefeatNotifications, 1, "yenilgi bildirimi");
                    Expect(rig.A.DefeatNotifications, 0, "diger robot bildirimi");
                    return "1 bildirim";
                });

                Run(results, "E1", "Yeni denemede iki robot kendi baslangic canina doner", p25, rig =>
                {
                    rig.A.StartHealth = 100f;
                    rig.B.StartHealth = 120f;
                    rig.NewMatch();
                    rig.A.Health.TakeDamage(40f);
                    rig.B.Health.TakeDamage(500f);
                    rig.NewMatch();
                    Expect(rig.A.Health.CurrentHealth, 100f, "A can");
                    Expect(rig.B.Health.CurrentHealth, 120f, "B can");
                    Expect(rig.B.DefeatNotifications, 0, "B bildirim sayaci");
                    return "A 100/100, B 120/120";
                });

                // ---------------- E2: tek silahla gecerli isabet ve bekleme ----------------
                Run(results, "E2", "Bir saldirinin iki collider'a temasi 100 cani yalniz 75'e indirir", p25, rig =>
                {
                    int inReach = CountInReach(rig.A.Attack, rig.B);
                    Check(inReach >= 2, "on kosul: en az iki hedef collider'i menzilde olmali, bulunan " + inReach);
                    Expect(rig.Swing(rig.A), AttackOutcome.Started, "istek");
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Hit, "sonuc");
                    return inReach + " collider menzilde, tek hasar";
                });

                Run(results, "E2", "Yandan yaklasma: govde + tekerlek yine tek hasar", p25, rig =>
                {
                    rig.Place(rig.B, rig.A, DefaultSpacing, 0f, 90f);
                    int inReach = CountInReach(rig.A.Attack, rig.B);
                    Check(inReach >= 2, "on kosul: en az iki collider menzilde olmali, bulunan " + inReach);
                    rig.Swing(rig.A);
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    return inReach + " collider menzilde, tek hasar";
                });

                Run(results, "E2", "Suren temas ikinci hasar yazmaz", p25, rig =>
                {
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "istek");
                    for (int i = 0; i < 10; i++) rig.A.Attack.Tick(); // ayni anda tekrarli temas
                    rig.Advance(0.05f);
                    rig.Advance(0.05f);
                    rig.Advance(0.1f);
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    return "pencere boyunca 14 temas kontrolu, tek hasar";
                });

                Run(results, "E2", "Menzil disinda hedef cani degismez", p25, rig =>
                {
                    rig.Place(rig.B, rig.A, 4f, 0f);
                    rig.Swing(rig.A);
                    Expect(rig.B.Health.CurrentHealth, 100f, "hedef can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Miss, "sonuc");
                    return "ISKA";
                });

                Run(results, "E2", "Pencere kapandiktan sonra yaklasan hedef hasar almaz", p25, rig =>
                {
                    rig.Place(rig.B, rig.A, 4f, 0f);
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "istek");
                    rig.Place(rig.B, rig.A, DefaultSpacing, 0f);
                    rig.Advance(rig.A.Attack.ActiveDuration + 0.02f);
                    Expect(rig.B.Health.CurrentHealth, 100f, "gec fizik adiminda hedef can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Miss, "sonuc");
                    return "gec adim onceki saldiriyi uzatmadi";
                });

                Run(results, "E2", "Kendi robotuna temas hasar vermez", p25, rig =>
                {
                    rig.Place(rig.B, rig.A, 20f, 0f);
                    Check(CountOwnInSphere(rig.A) > 0, "on kosul: kendi collider'lari saldiri alaninda olmali");
                    rig.Swing(rig.A);
                    Expect(rig.A.Health.CurrentHealth, 100f, "saldiran can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Miss, "sonuc");
                    return "kendi collider'lari alanda, can 100";
                });

                Run(results, "E2", "Bekleme surerken tekrarli istek yeni saldiri uretmez", p25, rig =>
                {
                    rig.Swing(rig.A);
                    Expect(rig.B.Health.CurrentHealth, 75f, "ilk isabet");
                    int rejected = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        if (rig.A.Attack.RequestAttack() == AttackOutcome.Cooldown) rejected++;
                        rig.Advance(0.1f);
                    }

                    Expect(rejected, 5, "reddedilen istek");
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    return "5 hizli istek reddedildi";
                });

                Run(results, "E2", "Bekleme bittikten sonraki gecerli saldiri cani 50'ye dusurur", p25, rig =>
                {
                    rig.Swing(rig.A);
                    rig.Advance(rig.A.Attack.Cooldown);
                    Expect(rig.Swing(rig.A), AttackOutcome.Started, "ikinci istek");
                    Expect(rig.B.Health.CurrentHealth, 50f, "hedef can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Hit, "ikinci sonuc");
                    return "75 -> 50; onceki temas kaydi yeni saldiriyi engellemedi";
                });

                Run(results, "E2", "Klavyesiz deneme bileseni saldiri istegi gonderir", p25, rig =>
                {
                    Expect(rig.A.Trigger.Fire(), AttackOutcome.Started, "tetikleyici");
                    rig.Advance(0.2f);
                    Expect(rig.B.Health.CurrentHealth, 75f, "hedef can");
                    return "AttackTestTrigger.Fire() -> ISABET";
                });

                // ---------------- E3: mac izinleri ve profiller ----------------
                Run(results, "E3", "Mac kapatildiktan sonra yeni hasar cikmaz", p25, rig =>
                {
                    rig.Gate.SetOpen(false);
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.MatchClosed, "istek");
                    rig.Advance(0.5f);
                    Expect(rig.B.Health.CurrentHealth, 100f, "hedef can");
                    return "MAC KAPALI, can 100";
                });

                Run(results, "E3", "Saldiri surerken mac kapanirsa hasar yazilmaz", p25, rig =>
                {
                    rig.Place(rig.B, rig.A, 4f, 0f);
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "istek");
                    rig.Gate.SetOpen(false);
                    rig.Place(rig.B, rig.A, DefaultSpacing, 0f); // temas pencere icinde basliyor
                    rig.Advance(0.05f);
                    rig.Advance(0.2f);
                    Expect(rig.B.Health.CurrentHealth, 100f, "hedef can");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.MatchClosed, "sonuc");
                    return "saldiri kesildi";
                });

                Run(results, "E3", "Yenilen robot yeni saldiri baslatamaz", p25, rig =>
                {
                    rig.A.Health.TakeDamage(1000f);
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.AttackerDefeated, "istek");
                    rig.Advance(0.3f);
                    Expect(rig.B.Health.CurrentHealth, 100f, "hedef can");
                    return "SALDIRAN YENIK";
                });

                Run(results, "E3", "Bir robot beklerken digerinin saldirisi kendi suresine gore calisir", p25, rig =>
                {
                    // Bekleme 1 s. A t0'da, B t0+0.3'te saldirir.
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "A istegi");
                    rig.Advance(0.3f);
                    Expect(rig.B.Attack.RequestAttack(), AttackOutcome.Started, "B istegi A beklerken");
                    rig.Advance(0.3f);                      // t0+0.6
                    Expect(rig.A.Health.CurrentHealth, 75f, "A can");
                    Expect(rig.B.Health.CurrentHealth, 75f, "B can");
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Cooldown, "A t0+0.6'da beklemede");
                    rig.Advance(0.45f);                     // t0+1.05: A hazir, B t0+1.3'e kadar beklemede
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "A t0+1.05'te hazir");
                    Expect(rig.B.Attack.RequestAttack(), AttackOutcome.Cooldown, "B t0+1.05'te beklemede");
                    return "beklemeler bagimsiz";
                });

                RunHitsToZero(results, p25, 4);
                RunHitsToZero(results, p40, 3);

                Run(results, "E3", "Yeni macta can, bekleme ve hedef kayitlari baslangic durumunda", p25, rig =>
                {
                    Expect(rig.A.Attack.RequestAttack(), AttackOutcome.Started, "istek");
                    rig.B.Health.TakeDamage(30f);
                    rig.NewMatch();
                    Expect(rig.A.Health.CurrentHealth, 100f, "A can");
                    Expect(rig.B.Health.CurrentHealth, 100f, "B can");
                    Expect(rig.A.Attack.CooldownRemaining, 0f, "A bekleme");
                    Expect(rig.A.Attack.IsAttacking, false, "suren saldiri");
                    Expect(rig.A.Attack.TargetsHitThisAttack, 0, "hedef kaydi");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.None, "son sonuc");
                    Expect(rig.Gate.IsOpen, true, "mac acik");
                    Expect(rig.Swing(rig.A), AttackOutcome.Started, "yeni mactaki ilk istek");
                    Expect(rig.B.Health.CurrentHealth, 75f, "yeni macta ilk isabet");
                    return "temiz baslangic";
                });

                // ---------------- E4: regresyon ----------------
                Run(results, "E4", "Menzil sinirinin hemen ici isabet, hemen disi iska", p25, rig =>
                {
                    float edge = CombatTestRig.BodyHalfLength + rig.A.Attack.Range;
                    rig.Place(rig.B, rig.A, edge - 0.02f, 0f);
                    rig.Swing(rig.A);
                    Expect(rig.B.Health.CurrentHealth, 75f, "sinir ici");
                    rig.Advance(rig.A.Attack.Cooldown);
                    rig.Place(rig.B, rig.A, edge + 0.02f, 0f);
                    rig.Swing(rig.A);
                    Expect(rig.B.Health.CurrentHealth, 75f, "sinir disi");
                    Expect(rig.A.Attack.LastReport.Outcome, AttackOutcome.Miss, "sinir disi sonuc");
                    return string.Format(CultureInfo.InvariantCulture, "sinir {0:0.00} m: -0.02 isabet, +0.02 iska", edge);
                });

                Run(results, "E4", "Farkli yaklasma acilari (yay 120 derece)", p25, rig =>
                {
                    var expected = new[] { (0f, true), (30f, true), (45f, true), (-45f, true), (90f, false), (135f, false), (180f, false) };
                    var summary = new List<string>();
                    foreach (var (angle, shouldHit) in expected)
                    {
                        rig.NewMatch();
                        rig.Place(rig.B, rig.A, 1.6f, angle);
                        rig.Swing(rig.A);
                        bool hit = rig.B.Health.CurrentHealth < 100f;
                        Check(hit == shouldHit, angle + " derece: beklenen " + (shouldHit ? "isabet" : "iska") + ", sonuc " + (hit ? "isabet" : "iska"));
                        Check(!hit || Mathf.Approximately(rig.B.Health.CurrentHealth, 75f), angle + " derece: tek hasar beklenirdi");
                        summary.Add(angle.ToString("0", CultureInfo.InvariantCulture) + (hit ? ":I" : ":-"));
                    }

                    return string.Join(" ", summary);
                });

                Run(results, "E4", "Art arda yeni maclar ayni sonucu verir", p25, rig =>
                {
                    for (int match = 1; match <= 3; match++)
                    {
                        rig.NewMatch();
                        int hits = HitUntilDefeated(rig, rig.A, rig.B);
                        Check(hits == 4, match + ". mac: 4 vurus beklenirdi, " + hits);
                        Expect(rig.B.DefeatNotifications, 1, match + ". mac bildirim");
                    }

                    return "3 macta da 4 vurus, 1 bildirim";
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(p25);
                UnityEngine.Object.DestroyImmediate(p40);
            }

            return results;
        }

        private const float DefaultSpacing = CombatTestRig.DefaultSpacing;

        private static void RunHitsToZero(List<Result> results, CombatProfile profile, int expected)
        {
            Run(results, "E3", string.Format(CultureInfo.InvariantCulture, "100 can {0:0} hasarla {1} gecerli vurusta sifira iner",
                profile.damage, expected), profile, rig =>
            {
                Check(profile.HitsToDefeat(100f) == expected, "profil hesabi " + profile.HitsToDefeat(100f));
                int hits = HitUntilDefeated(rig, rig.A, rig.B);
                Expect(hits, expected, "vurus sayisi");
                Expect(rig.B.Health.CurrentHealth, 0f, "son can");
                Expect(rig.B.DefeatNotifications, 1, "yenilgi bildirimi");
                return hits + " vurus, 1 bildirim";
            });
        }

        private static int HitUntilDefeated(CombatTestRig rig, CombatTestRobot attacker, CombatTestRobot target)
        {
            int hits = 0;
            for (int attempt = 0; attempt < 20 && !target.Health.IsDefeated; attempt++)
            {
                rig.Swing(attacker);
                if (attacker.Attack.LastReport.Outcome == AttackOutcome.Hit) hits++;
                rig.Advance(attacker.Attack.Cooldown);
            }

            return hits;
        }

        private static void Run(List<Result> results, string card, string name, CombatProfile profile, Func<CombatTestRig, string> body)
        {
            CombatTestRig rig = null;
            try
            {
                rig = CombatTestRig.Create(ScenarioCenter, true);
                rig.ApplyProfile(profile);
                string detail = body(rig);
                results.Add(new Result(card, name, true, detail));
            }
            catch (ScenarioFailure failure)
            {
                results.Add(new Result(card, name, false, failure.Message));
            }
            catch (Exception exception)
            {
                results.Add(new Result(card, name, false, exception.GetType().Name + ": " + exception.Message));
            }
            finally
            {
                rig?.Dispose();
            }
        }

        private static int CountInReach(MeleeAttack attack, CombatTestRobot target)
        {
            int count = 0;
            foreach (Collider collider in target.Colliders)
                if (attack.IsInReach(collider, out _)) count++;
            return count;
        }

        private static int CountOwnInSphere(CombatTestRobot robot)
        {
            int count = 0;
            foreach (Collider collider in Physics.OverlapSphere(robot.Attack.Origin, robot.Attack.Range))
                if (collider.GetComponentInParent<RobotHealth>() == robot.Health) count++;
            return count;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new ScenarioFailure(message);
        }

        private static void Expect<T>(T actual, T expected, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
                throw new ScenarioFailure(what + ": beklenen " + expected + ", gelen " + actual);
        }

        private static void Expect(float actual, float expected, string what)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
                throw new ScenarioFailure(string.Format(CultureInfo.InvariantCulture, "{0}: beklenen {1}, gelen {2}", what, expected, actual));
        }
    }
}
