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
        private int mode;
        private bool inputCleaned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-boundary-check") >= 0)
                Begin();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-layout-check") >= 0)
                BeginLayouts();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-barrier-check") >= 0)
                BeginBarriers();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-conveyor-check") >= 0)
                BeginConveyors();
        }

        public static void Begin(bool record = false)
        {
            if (FindFirstObjectByType<ArenaAcceptance>() != null) throw new InvalidOperationException("Verification already active");
            new GameObject("Arena Acceptance Verification").AddComponent<ArenaAcceptance>().capture = record;
        }

        public static void BeginLayouts()
        {
            if(FindFirstObjectByType<ArenaAcceptance>()!=null) throw new InvalidOperationException("Verification already active");
            var check=new GameObject("Arena Layout Verification").AddComponent<ArenaAcceptance>();
            check.mode=1;check.capture=true;
        }
        public static void BeginBarriers()
        {
            if(FindFirstObjectByType<ArenaAcceptance>()!=null)throw new InvalidOperationException("Verification already active");
            var check=new GameObject("Arena Barrier Verification").AddComponent<ArenaAcceptance>();check.mode=2;check.capture=true;
        }
        public static void BeginConveyors()
        {
            if(FindFirstObjectByType<ArenaAcceptance>()!=null)throw new InvalidOperationException("Verification already active");
            var check=new GameObject("Arena Conveyor Verification").AddComponent<ArenaAcceptance>();check.mode=3;check.capture=true;
        }

        private IEnumerator Start()
        {
            Status = "Running";
            Application.runInBackground = true;
            string[] args = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Arena/" + (Application.isEditor ? "Editor" : "Windows")));
            if(mode==1) output=Path.Combine(output,"Layouts");
            if(mode==2) output=Path.Combine(output,"Barriers");
            if(mode==3) output=Path.Combine(output,"Conveyors");
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
            IEnumerator run = mode==0 ? VerifyBoundary() : mode==1 ? VerifyLayouts() : mode==2 ? VerifyBarriers() : VerifyConveyors();
            var stack = new Stack<IEnumerator>();stack.Push(run);
            while (stack.Count>0)
            {
                object wait;
                try
                {
                    if (!stack.Peek().MoveNext()) {stack.Pop();continue;}
                    wait = stack.Peek().Current;
                }
                catch (Exception ex) { Finish(false, ex.ToString()); yield break; }
                if(wait is IEnumerator nested) stack.Push(nested);else yield return wait;
            }
            Finish(true, mode==0 ? "64 cells; eight perimeter waypoints in two rounds; rotated footprint; bot bounds; clean reset" : mode==1 ? "Open/Tactical selection guards, routes, generation, two actual matches per layout, reset persistence" : mode==2 ? "Warning order, centre/edge occupancy, pause, three cycles, two matches, reset and one instance" : "Equivalent player/bot transport, compound contacts, transition, corner, reverse, exit, disable and two-match reset");
        }

        private IEnumerator VerifyLayouts()
        {
            match=FindFirstObjectByType<MatchController>();bounds=FindFirstObjectByType<ArenaBounds>();
            var arena=FindFirstObjectByType<ArenaEnvironment>();
            Require(arena!=null && arena.CanSelect,"Layout selection in Ready");
            var panel=match.GetComponent<UI.SettingsPanelController>();
            panel.ToggleSettings();Require(!arena.Select(1),"Settings blocks selection");panel.ToggleSettings();
            var menu=match.GetComponent<UI.MainMenuScreen>();menu.enabled=false;Require(!arena.Select(1),"Inactive menu blocks selection");menu.enabled=true;
            foreach(int i in new[]{0,1,0,1,0}) {Require(arena.Select(i),"Ready selection");yield return null;}
            foreach(var c in arena.Contents.GetComponentsInChildren<BoxCollider>())Require(!c.name.StartsWith("Arena Obstacle"),"Open has no retired obstacle colliders");
            for(int layoutIndex=0;layoutIndex<2;layoutIndex++)
            {
                Require(arena.Select(layoutIndex),"Choose layout "+layoutIndex);yield return null;
                var chosen=arena.Layout;
                var route=ArenaLayoutValidator.FindRoute(chosen,arena.ClearanceRadius);
                Require(route.Count>1,"Robot-width route");
                checks.Add(chosen.displayName+" clearance="+arena.ClearanceRadius+" route samples="+route.Count);
                for(int round=0;round<2;round++)
                {
                    Require(match.StartMatch(),"Layout match starts");yield return null;
                    Require(!arena.Select(1-layoutIndex),"Playing blocks selection");
                    match.Opponent.StopCombat();
                    var driver=match.Player.GetComponent<Driving.PlayerKeyboardDriver>();
                    driver.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
                    // Keep only endpoints/turns; use actual keyboard input and production physics.
                    for(int k=1;k<route.Count;k++)
                    {
                        // The far spawn is physically occupied; enter combat at approach distance.
                        if(k>=route.Count-4) break;
                        if(k<route.Count-1 && (route[k]-route[k-1])==(route[k+1]-route[k])) continue;
                        yield return DriveTo(route[k]);
                    }
                    Keys();
                    // Return the real bot to normal combat. A real health event must end the round.
                    match.Player.ConfigureCombat(false,match.Opponent);
                    match.Opponent.ConfigureCombat(true,match.Player);
                    float deadline=Time.realtimeSinceStartup+100;
                    while(match.State==MatchController.MatchState.Playing)
                    {
                        Require(Time.realtimeSinceStartup<deadline,"Real combat timeout on "+chosen.displayName);
                        Vector3 delta=match.Opponent.transform.position-match.Player.transform.position;delta.y=0;
                        float angle=Vector3.SignedAngle(match.Player.transform.forward,delta,Vector3.up);
                        var keys=new List<Key>{Key.Space};
                        if(angle>8)keys.Add(Key.D);if(angle< -8)keys.Add(Key.A);
                        if(Mathf.Abs(angle)<50)keys.Add(Time.time%5<.7f?Key.S:Key.W);
                        Keys(keys.ToArray());yield return null;
                    }
                    Keys();
                    Require(match.ResultCount==1 && (match.Player.GetComponent<RobotHealth>().IsDefeated||match.Opponent.GetComponent<RobotHealth>().IsDefeated),"Actual health result");
                    match.Prepare();yield return null;
                    Require(arena.Layout==chosen && match.ResultCount==0 && FindObjectsByType<ArenaCell>(FindObjectsSortMode.None).Length==64,"Layout persists through clean reset");
                    checks.Add(chosen.displayName+" actual match "+(round+1)+" passed");
                }
            }
        }

        private IEnumerator DriveTo(Vector2 target)
        {
            var player=match.Player;var body=player.GetComponent<Rigidbody>();
            float deadline=Time.realtimeSinceStartup+20;
            while(Vector2.Distance(new Vector2(body.position.x,body.position.z),target)>.25f)
            {
                Require(Time.realtimeSinceStartup<deadline,"Route drive timeout "+target+" at "+body.position);
                Vector3 delta=new Vector3(target.x,body.position.y,target.y)-body.position;
                float angle=Vector3.SignedAngle(player.transform.forward,delta,Vector3.up);
                var keys=new List<Key>();
                if(angle>4)keys.Add(Key.D);if(angle< -4)keys.Add(Key.A);
                if(Mathf.Abs(angle)<20 && (delta.magnitude>1.4f||Time.frameCount%4==0))keys.Add(Key.W);
                Keys(keys.ToArray());yield return null;
            }
            Keys();yield return new WaitForSeconds(.25f);
        }

        private IEnumerator VerifyBarriers()
        {
            match=FindFirstObjectByType<MatchController>();bounds=FindFirstObjectByType<ArenaBounds>();
            var arena=FindFirstObjectByType<ArenaEnvironment>();
            Require(arena.Select(1),"Select Tactical for barrier scenario");yield return null;
            var barrier=arena.Contents.GetComponentInChildren<WarningBarrier>();
            Require(barrier!=null,"Production barrier present");int identity=barrier.GetInstanceID();
            var player=match.Player;var body=player.GetComponent<Rigidbody>();Vector3 spawn=player.transform.position;
            Require(match.StartMatch(),"First barrier match");yield return null;match.Opponent.StopCombat();Keys();
            foreach(float offset in new[]{0f,.9f})
            {
                barrier.ResetCycle();body.position=barrier.transform.position+Vector3.right*offset;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();Require(barrier.IsOccupied(),"Occupancy centre/edge "+offset);
                float stop=Time.realtimeSinceStartup+5.2f;bool warned=false;
                while(Time.realtimeSinceStartup<stop)
                {
                    warned|=barrier.Phase==BarrierPhase.Warning;
                    Require(barrier.Phase!=BarrierPhase.Rising&&barrier.Phase!=BarrierPhase.Active&&!barrier.BarrierCollider.enabled,"No blind rise under robot");
                    Require((body.position-new Vector3(barrier.transform.position.x+offset,body.position.y,barrier.transform.position.z)).magnitude<.2f,"No occupancy teleport");
                    Require(body.linearVelocity.magnitude<.5f,"No occupancy launch impulse");yield return null;
                }
                Require(warned&&barrier.DeferredCycles>0,"Warning precedes safe cancellation");checks.Add("occupied offset "+offset+" deferred safely");
            }
            body.position=spawn;body.linearVelocity=Vector3.zero;barrier.ResetCycle();Physics.SyncTransforms();
            float warningDeadline=Time.realtimeSinceStartup+4;
            while(barrier.Phase!=BarrierPhase.Warning){Require(Time.realtimeSinceStartup<warningDeadline,"Warning deadline");yield return null;}
            var settingsPanel=match.GetComponent<UI.SettingsPanelController>();
            float elapsed=barrier.Elapsed;settingsPanel.ToggleSettings();yield return new WaitForSecondsRealtime(.3f);
            Require(barrier.Phase==BarrierPhase.Warning&&Mathf.Abs(barrier.Elapsed-elapsed)<.001f,"Pause freezes phase and timer");settingsPanel.ToggleSettings();
            float deadline=Time.realtimeSinceStartup+23;BarrierPhase previous=barrier.Phase;
            while(barrier.CompletedCycles<3)
            {
                Require(Time.realtimeSinceStartup<deadline,"Three barrier cycles deadline");
                if(barrier.Phase!=previous){checks.Add(previous+" -> "+barrier.Phase);Require((int)barrier.Phase==((int)previous+1)%5,"Ordered lifecycle");previous=barrier.Phase;}
                yield return null;
            }
            match.Defeat(false);match.Prepare();yield return null;
            Require(barrier.Phase==BarrierPhase.Idle&&barrier.Elapsed==0&&!barrier.BarrierCollider.enabled&&barrier.CompletedCycles==0,"Reset clears phase timer collider");
            Require(barrier.GetInstanceID()==identity && arena.Contents.GetComponentsInChildren<WarningBarrier>().Length==1,"Single barrier instance after reset");
            Require(match.StartMatch(),"Second barrier match");yield return null;match.Opponent.StopCombat();
            deadline=Time.realtimeSinceStartup+8;
            while(barrier.CompletedCycles<1){Require(Time.realtimeSinceStartup<deadline,"Second match starts a fresh cycle");yield return null;}
            match.Defeat(true);match.Prepare();checks.Add("two matches preserve identity and reset the cycle");
        }

        private IEnumerator VerifyConveyors()
        {
            match=FindFirstObjectByType<MatchController>();bounds=FindFirstObjectByType<ArenaBounds>();
            var arena=FindFirstObjectByType<ArenaEnvironment>();Require(arena.Select(0),"Open conveyor layout");yield return null;
            var belts=arena.Contents.GetComponentsInChildren<ConveyorSurface>();Require(belts.Length==2,"Two adjacent conveyors");
            Require(match.StartMatch(),"First conveyor match");yield return null;Keys();
            match.Opponent.GetComponent<RobotBotController>().enabled=false;
            match.Player.GetComponent<Driving.PlayerKeyboardDriver>().enabled=false;
            var playerBody=match.Player.GetComponent<Rigidbody>();var botBody=match.Opponent.GetComponent<Rigidbody>();
            float[] displacement=new float[2];int sampleIndex=0;
            foreach(var robot in new[]{match.Player,match.Opponent})
            {
                var body=robot.GetComponent<Rigidbody>();var motor=robot.GetComponent<Robots.RobotMotor>();
                playerBody.position=new Vector3(4,.82f,-4);botBody.position=new Vector3(4,.82f,3);
                ConveyorSurface.RemovePreviousMotion();motor.ResetMotion();
                body.position=belts[0].transform.position;body.rotation=Quaternion.Euler(0,90,0);body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                Vector3 initial=body.position;
                for(int step=0;step<30;step++)
                {
                    yield return new WaitForFixedUpdate();
                    Require(Vector3.Distance(ConveyorSurface.AppliedVelocity(body),Vector3.right*ConveyorSurface.SurfaceSpeed)<.001f,"One bounded contribution per compound robot");
                    Require(new Vector2(body.linearVelocity.x,body.linearVelocity.z).magnitude<1.1f,"Idle conveyor speed stays bounded");
                }
                displacement[sampleIndex++]=body.position.x-initial.x;
                Require(displacement[sampleIndex-1]>.12f,"Measurable transport");
                checks.Add((robot==match.Player?"Player":"Bot")+" idle displacement="+displacement[sampleIndex-1]+" m / "+(30*Time.fixedDeltaTime)+"s; surface="+ConveyorSurface.AppliedVelocity(body));
            }
            Require(Mathf.Abs(displacement[0]-displacement[1])<.04f,"Equivalent player/bot transport");
            botBody.position=new Vector3(4,.82f,3);
            var playerMotor=match.Player.GetComponent<Robots.RobotMotor>();
            ConveyorSurface.RemovePreviousMotion();playerMotor.ResetMotion();playerBody.position=new Vector3(-5,.82f,-5);playerBody.rotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();
            var driver=match.Player.GetComponent<Driving.PlayerKeyboardDriver>();driver.enabled=true;
            driver.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
            yield return DriveTo(new Vector2(2,-5));Keys();yield return new WaitForFixedUpdate();
            Require(ConveyorSurface.AppliedVelocity(playerBody)==Vector3.zero,"Physical keyboard traversal exits the belts");
            driver.enabled=false;checks.Add("Real keyboard entry, adjoining-belt traversal and physical exit");
            ConveyorSurface.RemovePreviousMotion();playerMotor.ResetMotion();playerBody.position=new Vector3(-2,.82f,-5);playerBody.rotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();
            for(int step=0;step<100;step++)
            {
                yield return new WaitForFixedUpdate();
                Require(ConveyorSurface.AppliedVelocity(playerBody).magnitude<=ConveyorSurface.SurfaceSpeed+.001f,"Adjacent belts do not multiply speed");
                Require(Mathf.Abs(playerBody.linearVelocity.x)<1.1f,"No unbounded idle accumulation");
            }
            checks.Add("Adjacent transition and compound colliders: <=0.8 m/s surface contribution");
            ConveyorSurface.RemovePreviousMotion();playerMotor.ResetMotion();playerBody.position=belts[0].transform.position+new Vector3(.9f,0,.9f);Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();Require(ConveyorSurface.AppliedVelocity(playerBody).magnitude<=.801f,"Corner contact bounded");
            playerBody.position=belts[0].transform.position;playerBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            for(int step=0;step<20;step++){match.Player.GetComponent<RobotDriveMotor>().Submit(-1,0);yield return new WaitForFixedUpdate();}
            Require(playerBody.linearVelocity.x<-.1f && playerBody.linearVelocity.x> -3.2f,"Reverse driving overcomes belt without accumulation");
            checks.Add("Reverse velocity="+playerBody.linearVelocity.x+" m/s");
            ConveyorSurface.RemovePreviousMotion();playerMotor.ResetMotion();playerBody.position=belts[0].transform.position;playerBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();Require(ConveyorSurface.AppliedVelocity(playerBody).sqrMagnitude>0,"Contact active before exit");
            // Controlled exit isolates stale surface force from the existing motor's natural braking.
            playerBody.position=new Vector3(0,.82f,-2);playerBody.linearVelocity=Vector3.right*2;Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();Require(ConveyorSurface.AppliedVelocity(playerBody)==Vector3.zero,"Exit clears contribution by next physics step");
            Require(playerBody.linearVelocity.x>0,"Residual natural momentum distinguished from applied surface velocity");
            playerBody.position=belts[0].transform.position;playerMotor.ResetMotion();Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            foreach(var belt in belts)belt.enabled=false;
            Require(ConveyorSurface.AppliedVelocity(playerBody)==Vector3.zero,"Disable clears surface state immediately");
            foreach(var belt in belts)belt.enabled=true;
            match.Defeat(false);match.Prepare();yield return null;
            Require(ConveyorSurface.AppliedVelocity(playerBody)==Vector3.zero,"Reset has no cached force");
            Require(match.StartMatch(),"Second conveyor match");yield return null;
            Require(ConveyorSurface.AppliedVelocity(playerBody)==Vector3.zero,"Fresh spawn receives no stale force");
            Require(arena.Contents.GetComponentsInChildren<ConveyorSurface>().Length==2,"Reset has exactly two original belts");
            match.Defeat(true);match.Prepare();checks.Add("exit, disable and second match clean");
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
            CleanupInput();
            File.WriteAllText(Path.Combine(output,mode==0?"boundary-results.json":mode==1?"layout-results.json":mode==2?"barrier-results.json":"conveyor-results.json"), JsonUtility.ToJson(new Report { passed=passed,detail=detail,checks=checks.ToArray(),frames=frame,unity=Application.unityVersion },true));
            if (passed) Debug.Log("ARENA_BOUNDARY " + Status); else Debug.LogError("ARENA_BOUNDARY " + Status);
            if (!Application.isEditor) Application.Quit(passed ? 0 : 1);
        }

        [Serializable] private sealed class Report { public bool passed; public string detail; public string[] checks; public int frames; public string unity; }
        private void OnDestroy()
        {
            CleanupInput();
            if (image != null) Destroy(image);
            if (imageTarget != null) Destroy(imageTarget);
            if (settings != null && InputSystem.settings!=settings) Destroy(settings);
        }
        private void OnDisable() => CleanupInput();
        private void CleanupInput()
        {
            if(inputCleaned)return;
            inputCleaned=true;
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(previousSettings!=null)InputSystem.settings=previousSettings;
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
        }
    }
}
