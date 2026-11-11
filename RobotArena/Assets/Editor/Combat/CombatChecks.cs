using System;
using System.Linq;
using RobotSavaslari.Combat.Testing;
using UnityEditor;
using UnityEngine;

namespace RobotSavaslari.Combat.EditorChecks
{
    /// <summary>
    /// Dovus kurallarinin kontrolleri (E1-E4). Mevcut RobotDamageChecks duzeni gibi Play Mode gerektirmez;
    /// gecici robotlari sahne disinda kurar, gercek collider'larla senaryolari oynatir ve siler.
    /// Menuden: RobotSavaslari/Combat/Run Melee Checks
    /// Komut satirindan: Unity -batchmode -projectPath ... -executeMethod RobotSavaslari.Combat.EditorChecks.CombatChecks.RunBatch
    /// </summary>
    public static class CombatChecks
    {
        [MenuItem("RobotSavaslari/Combat/Run Melee Checks")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void RunBatch()
        {
            int exitCode;
            try
            {
                exitCode = Run() ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        private static bool Run()
        {
            var results = CombatScenarios.RunAll();
            foreach (CombatScenarios.Result result in results)
            {
                if (result.Passed) Debug.Log("COMBATCHECK " + result);
                else Debug.LogError("COMBATCHECK " + result);
            }

            int passed = results.Count(r => r.Passed);
            string summary = "COMBATCHECK SUMMARY " + passed + "/" + results.Count + (passed == results.Count ? " PASS" : " FAIL");
            if (passed == results.Count) Debug.Log(summary);
            else Debug.LogError(summary);
            return passed == results.Count;
        }
    }
}
