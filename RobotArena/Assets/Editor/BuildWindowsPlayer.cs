using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace RobotSavaslari.Editor
{
    public static class BuildWindowsPlayer
    {
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (!scenes.Any(scene => scene == "Assets/Scenes/Arena.unity"))
            {
                throw new InvalidOperationException("Arena scene must be enabled in Build Settings.");
            }

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