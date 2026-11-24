#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(RobotSavaslari.Gameplay.Tests.WorkshopPlayerResults))]

namespace RobotSavaslari.Gameplay.Tests
{
    // An opt-in local NUnit result for running the packaged test player independently of
    // the Editor connection. Ordinary Test Runner sessions keep their default lifecycle.
    [Preserve]
    public sealed class WorkshopPlayerResults : ITestRunCallback
    {
        private readonly string path = Environment.GetEnvironmentVariable("WORKSHOP_PLAYER_XML");
        public void RunStarted(ITest tests)
        {
            if (!string.IsNullOrEmpty(path)) Debug.Log("WORKSHOP_WINDOWS_STARTED " + Application.platform);
        }

        public void TestStarted(ITest test)
        {
            if (!string.IsNullOrEmpty(path) && !test.IsSuite) Debug.Log("WORKSHOP_WINDOWS_TEST " + test.FullName);
        }

        public void TestFinished(ITestResult result) { }

        public void RunFinished(ITestResult result)
        {
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, result.ToXml(true).OuterXml);
            Debug.Log($"WORKSHOP_WINDOWS_FINISHED passed={result.PassCount} failed={result.FailCount}");
            Application.Quit(result.FailCount == 0 && result.PassCount > 0 ? 0 : 1);
        }
    }
}
#endif
