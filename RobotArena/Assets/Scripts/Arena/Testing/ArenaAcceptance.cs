using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RobotSavaslari.Arena.Testing
{
    // Explicit opt-in only. Exercises the production MainArena in Editor and Windows player.
    public sealed class ArenaAcceptance : MonoBehaviour
    {
        public static string Status { get; private set; } = "Not run";
        private MatchController match;
        private ArenaBounds bounds;
        private Keyboard keyboard, previousKeyboard;
        private InputSettings previousSettings, settings;
        private readonly List<string> checks = new List<string>();
        private string output;
        private bool capture;
        private float nextFrame;
        private int frame;
        private RenderTexture imageTarget;
        private Texture2D image;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-boundary-check") >= 0)
                Begin();
        }

        public static void Begin(bool record = false)
        {
            if (FindFirstObjectByType<ArenaAcceptance>() != null) throw new InvalidOperationException("Verification already active");
            new GameObject("Arena Acceptance Verification").AddComponent<ArenaAcceptance>().capture = record;
        }

        private IEnumerator Start()
        {
            Status = "Running";
            Application.runInBackground = true;
            string[] args = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Arena/" + (Application.isEditor ? "Editor" : "Windows")));
            int index = Array.IndexOf(args, "-arena-check-output");
            if (index >= 0 && index + 1 < args.Length) output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            capture |= Array.IndexOf(args, "-arena-capture") >= 0;
            if (capture)
            {
                Directory.CreateDirectory(Path.Combine(output, "frames"));
                imageTarget = new RenderTexture(960, 540, 24);
                image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            }
            previousKeyboard = Keyboard.current;
            previousSettings = InputSystem.settings;
            settings = Instantiate(previousSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            yield return null;
            IEnumerator run = VerifyBoundary();
            while (true)
            {
                object wait;
                try
                {
                    if (!run.MoveNext()) break;
                    wait = run.Current;
                }
                catch (Exception ex) { Finish(false, ex.ToString()); yield break; }
                yield return wait;
            }
            Finish(true, "64 cells; eight perimeter waypoints in two rounds; rotated footprint; bot bounds; clean reset");
        }

        private IEnumerator VerifyBoundary()
        {
            match = FindFirstObjectByType<MatchController>();
            bounds = FindFirstObjectByType<ArenaBounds>();
            Require(match != null && bounds != null && match.IsInitialized, "Production scene references");
            Require(FindObjectsByType<ArenaCell>(FindObjectsSortMode.None).Length == 64, "64 cells");
            Require(bounds.HalfExtents == new Vector2(8, 8), "Physical dimensions 16x16");
            var player = match.Player;
            var body = player.GetComponent<Rigidbody>();
            Vector3 spawn = player.transform.position;
            var driver = player.GetComponent<Driving.PlayerKeyboardDriver>();
            driver.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            Vector2 limits = bounds.CenterLimits(player.ArenaFootprintRadius);
            Require(limits.x > 6 && limits.y > 6, "New outer cells available to real footprint");
            for (int rotation = 0; rotation < 360; rotation += 30)
            {
                body.rotation = Quaternion.Euler(0, rotation, 0);
                body.position = new Vector3(20, spawn.y, -20);
                body.linearVelocity = new Vector3(2, 0, -2);
                player.ConstrainToArena();
                Require(Mathf.Abs(body.position.x - limits.x) < .001f && Mathf.Abs(body.position.z + limits.y) < .001f, "Rotated corner clamp " + rotation);
                Require(body.linearVelocity.x == 0 && body.linearVelocity.z == 0, "Outward velocity removed");
            }
            match.Prepare();
            var waypoints = new[] { new Vector2(-6.1f,0), new Vector2(-6.1f,-6.1f), new Vector2(0,-6.1f), new Vector2(6.1f,-6.1f), new Vector2(6.1f,0), new Vector2(6.1f,6.1f), new Vector2(0,6.1f), new Vector2(-6.1f,6.1f) };
            for (int round = 1; round <= 2; round++)
            {
                Require(match.StartMatch(), "Start round " + round);
                yield return null;
                bounds.SynchronizeBotBounds();
                var bot = match.Opponent.GetComponent<RobotBotController>();
                Require(bot != null && bot.tacticalHalfExtents.x > 6 && bot.tacticalHalfExtents.y > 6, "Runtime bot receives expanded limits");
                // Isolate wall traversal from combat damage; do not replace the player's motor/input.
                match.Opponent.StopCombat();
                foreach (Vector2 point in waypoints)
                {
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (Vector2.Distance(new Vector2(body.position.x, body.position.z), point) > .35f)
                    {
                        Require(Time.realtimeSinceStartup < deadline, "Drive timeout at " + point + " from " + body.position);
                        Vector3 delta = new Vector3(point.x, body.position.y, point.y) - body.position;
                        float angle = Vector3.SignedAngle(player.transform.forward, delta, Vector3.up);
                        var keys = new List<Key>();
                        if (angle > 5) keys.Add(Key.D);
                        if (angle < -5) keys.Add(Key.A);
                        if (Mathf.Abs(angle) < 40) keys.Add(Key.W);
                        Keys(keys.ToArray());
                        Require(body.position.y > .7f, "No floor fall-through");
                        Require(Mathf.Abs(body.position.x) <= limits.x + .05f && Mathf.Abs(body.position.z) <= limits.y + .05f, "No perimeter exit");
                        yield return null;
                    }
                    checks.Add("round " + round + " physically reached " + point);
                }
                Keys();
                match.Defeat(false);
                Require(match.ResultCount == 1, "One result");
                match.Prepare();
                Require(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(spawn.x,spawn.z)) < .01f, "Reset to valid original spawn");
                Require(match.State == MatchController.MatchState.Ready && match.ResultCount == 0, "Clean next round");
            }
        }

        private void Keys(params Key[] keys) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        private void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }

        private void LateUpdate()
        {
            if (!capture || Status != "Running" || Time.realtimeSinceStartup < nextFrame || Camera.main == null) return;
            nextFrame = Time.realtimeSinceStartup + .2f;
            var camera = Camera.main;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            camera.targetTexture = imageTarget;
            camera.Render();
            RenderTexture.active = imageTarget;
            image.ReadPixels(new Rect(0,0,960,540),0,0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output,"frames",(frame++).ToString("D5")+".jpg"), image.EncodeToJPG(75));
            camera.targetTexture = previous;
            RenderTexture.active = active;
        }

        private void Finish(bool passed, string detail)
        {
            Status = (passed ? "PASS: " : "FAIL: ") + detail;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (previousSettings != null) InputSystem.settings = previousSettings;
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            File.WriteAllText(Path.Combine(output,"boundary-results.json"), JsonUtility.ToJson(new Report { passed=passed,detail=detail,checks=checks.ToArray(),frames=frame,unity=Application.unityVersion },true));
            if (passed) Debug.Log("ARENA_BOUNDARY " + Status); else Debug.LogError("ARENA_BOUNDARY " + Status);
            if (!Application.isEditor) Application.Quit(passed ? 0 : 1);
        }

        [Serializable] private sealed class Report { public bool passed; public string detail; public string[] checks; public int frames; public string unity; }
        private void OnDestroy()
        {
            if (image != null) Destroy(image);
            if (imageTarget != null) Destroy(imageTarget);
            if (settings != null) Destroy(settings);
        }
    }
}
