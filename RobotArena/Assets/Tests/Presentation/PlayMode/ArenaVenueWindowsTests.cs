using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsPlayer)]
    public sealed class ArenaVenueWindowsTests
    {
        private string folder, temporaryStore;
        private int width, height, vsync, rate;
        private FullScreenMode mode;
        private string preferences;
        private bool hadPreference;

        [SetUp]
        public void SetUp()
        {
            folder = Environment.GetEnvironmentVariable("ARENA_VENUE_EVIDENCE");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.Combine(Application.temporaryCachePath, "ArenaVenueEvidence");
            Directory.CreateDirectory(folder);
            temporaryStore = Path.Combine(Application.temporaryCachePath, "ArenaVenue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryStore);
            hadPreference = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            preferences = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            width = Screen.width;
            height = Screen.height;
            mode = Screen.fullScreenMode;
            vsync = QualitySettings.vSyncCount;
            rate = Application.targetFrameRate;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            yield return MainArenaTestScene.Unload();
            if (hadPreference) PlayerPrefs.SetString(DuelSetup.PreferenceKey, preferences);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = rate;
            Screen.SetResolution(width, height, mode);
            if (Directory.Exists(temporaryStore)) Directory.Delete(temporaryStore, true);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator CompareOpenAt720And1080()
        {
            File.WriteAllText(Path.Combine(folder, "frames.csv"), "width,height,venue,frame,cpu_ms,gpu_ms,physics_ms,gc_bytes,memory_bytes\n");
            foreach (int h in new[] { 720, 1080 })
            {
                // Invert the ordering at the second resolution to reduce warm-up bias.
                foreach (bool show in new[] { h == 1080, h != 1080 })
                {
                    yield return MainArenaTestScene.Load();
                    var match = MainArenaTestScene.Match;
                    match.HistoryStore = new MatchHistoryStore(Path.Combine(temporaryStore, "history.json"));
                    MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(temporaryStore, "replays")));
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    Screen.SetResolution(h * 16 / 9, h, FullScreenMode.Windowed);
                    for (int i = 0; i < 25; i++) yield return null;
                    var menu = match.GetComponent<MainMenuScreen>();
                    var venue = match.GetComponent<ArenaVenue>();
                    venue.enabled = show;
                    Assert.That(menu.SelectPreset(DuelSetup.Quick()), Is.True);
                    menu.StartLobbyMatch();
                    Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
                    yield return null;
                    yield return null;
                    yield return Capture(h, show, "countdown");
                    yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                    for (int i = 0; i < 45; i++) yield return null;
                    yield return Measure(h, show);
                    yield return Capture(h, show, "playing");
                    if (show)
                    {
                        var hp = match.Player.GetComponent<RobotHealth>();
                        bool hit = false;
                        hp.Damaged += contact => { if (contact.Weapon) hit = true; };
                        float limit = Time.realtimeSinceStartup + 65;
                        while (!hit && match.State == MatchController.MatchState.Playing && Time.realtimeSinceStartup < limit) yield return null;
                        Assert.That(hit, Is.True, "Arena bot must land a weapon hit");
                        yield return Capture(h, true, "hit");
                        while (match.State == MatchController.MatchState.Playing && Time.realtimeSinceStartup < limit) yield return null;
                        Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                        yield return Capture(h, true, "result");
                    }
                }
            }
        }

        private IEnumerator Capture(int h, bool show, string phase)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(image.width, Is.EqualTo(h * 16 / 9));
                Assert.That(image.height, Is.EqualTo(h));
                File.WriteAllBytes(Path.Combine(folder, $"{h}-{(show ? "on" : "off")}-{phase}.png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(image); }
        }

        private IEnumerator Measure(int h, bool show)
        {
            const int count = 300;
            var timing = new FrameTiming[1];
            var cpu = new double[count];
            var gpu = new double[count];
            var physics = new long[count];
            var gc = new long[count];
            var mem = new long[count];
            using (var sim = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate"))
            using (var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                for (int i = 0; i < count; i++)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                    {
                        cpu[i] = timing[0].cpuFrameTime;
                        gpu[i] = timing[0].gpuFrameTime;
                    }
                    physics[i] = sim.Valid && sim.Count > 0 ? sim.LastValue : -1;
                    gc[i] = alloc.Valid && alloc.Count > 0 ? alloc.LastValue : -1;
                    mem[i] = Profiler.GetTotalAllocatedMemoryLong();
                }
            }
            var lines = new List<string>(count);
            for (int i = 0; i < count; i++)
                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F4},{5:F4},{6:F6},{7},{8}",
                    h * 16 / 9, h, show ? "on" : "off", i, cpu[i], gpu[i], physics[i] < 0 ? -1 : physics[i] / 1e6, gc[i], mem[i]));
            File.AppendAllLines(Path.Combine(folder, "frames.csv"), lines);
            Assert.That(Array.Exists(cpu, t => t > 0), Is.True, "CPU frame timing samples");
            Assert.That(Array.Exists(gpu, t => t > 0), Is.True, "GPU frame timing samples");
        }
    }
}
