using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace RobotSavaslari.Editor
{
    public static class BuildWindowsPlayer
    {
        [MenuItem("RobotSavaslari/Build Windows Player")]
        public static void Build()
        {
            if (!File.Exists(GameplayStartup.ScenePath))
                throw new InvalidOperationException("MainArena scene is missing.");

            // Ana menu varsa oyun ondan acilir; OYNA butonu MainArena'yi yukler.
            const string menuScene = "Assets/Scenes/MainMenu.unity";
            string[] scenes = File.Exists(menuScene)
                ? new[] { menuScene, GameplayStartup.ScenePath }
                : new[] { GameplayStartup.ScenePath };

            string projectPath = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectPath, "Builds", "Windows");
            Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDirectory, "RobotSavaslari.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Format("Windows build failed: {0}", report.summary.result));
            }

            UnityEngine.Debug.Log(string.Format("Windows build created at {0}", options.locationPathName));
        }
    }
}
