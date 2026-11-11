using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using RobotSavaslari.UI;

namespace RobotSavaslari.Match
{
    // Runs only by explicit command-line opt-in in Editor or standalone player.
    public sealed class ArenaGameplaySmoke : MonoBehaviour
    {
        private Keyboard keyboard;
        private Keyboard previousKeyboard;
        private InputSettings previousSettings;
        private InputSettings smokeSettings;
        private MatchController match;
        private bool playerDamagedOpponent;
        private bool botDamagedPlayer;
        private bool regionalDamage;
        private int results;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-smoke") < 0) return;
            new GameObject("Arena Gameplay Verification").AddComponent<ArenaGameplaySmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            previousKeyboard = Keyboard.current;
            previousSettings = InputSystem.settings;
            smokeSettings = Instantiate(previousSettings);
            // A batch Editor has no focused Game View. Route only this opt-in verification
            // session like a focused game without changing the project's input asset.
            smokeSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            smokeSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = smokeSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            IEnumerator run = Verify();
            while (true)
            {
                object wait;
                try
                {
                    if (!run.MoveNext()) break;
                    wait = run.Current;
                }
                catch (Exception error)
                {
                    Finish(false, error.ToString());
                    yield break;
                }
                yield return wait;
            }
            Finish(true, "two real keyboard-driven matches, bot movement and counterattack, contact damage, regional wear, single result, clean restart");
        }

        private IEnumerator Verify()
        {
            yield return null;
            Require(SceneManager.GetActiveScene().name == "MainArena", "MainArena must be the startup scene");
            match = FindFirstObjectByType<MatchController>();
            Require(match != null && match.IsInitialized && !match.ManualNotificationsOnly, "Real gameplay controller initialized");
            Require(FindObjectsByType<MatchController>().Length == 1 && FindObjectsByType<ArenaMatchController>().Length == 0, "Single result authority");
            Require(FindObjectsByType<AudioListener>().Count(listener => listener.isActiveAndEnabled) == 1, "One active AudioListener");
            Require(match.State == MatchController.MatchState.Ready && match.GetComponent<MainMenuScreen>() != null,
                "Menu belongs to the arena match");
            var settingsPanel = match.GetComponent<SettingsPanelController>();
            Require(settingsPanel != null && !settingsPanel.IsOpen, "One shared settings panel starts closed");
            Keys(Key.Escape);
            for (int frame = 0; frame < 4 && !settingsPanel.IsOpen; frame++) yield return null;
            Require(settingsPanel.IsOpen && Time.timeScale == 0f, "Settings opens from the arena menu and pauses it");
            Keys(Key.Enter);
            yield return null;
            yield return null;
            Require(match.State == MatchController.MatchState.Ready, "Settings ignores Enter to start");
            Keys(Key.Escape);
            for (int frame = 0; frame < 4 && settingsPanel.IsOpen; frame++) yield return null;
            Require(!settingsPanel.IsOpen && Time.timeScale > 0f, "Settings closes and restores time");
            Keys();
            yield return null;
            var player = match.Player;
            if (Application.isBatchMode)
                player.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            Debug.Log($"ARENA_SMOKE INPUT virtualKeyboard={keyboard.deviceId} enabled={keyboard.enabled} applicationFocused={Application.isFocused} batch={Application.isBatchMode} updateMode={InputSystem.settings.updateMode}");
            var opponent = match.Opponent;
            var playerHealth = player.GetComponent<RobotHealth>();
            var opponentHealth = opponent.GetComponent<RobotHealth>();
            var playerDamage = player.GetComponent<RobotDamageVisuals>();
            var opponentDamage = opponent.GetComponent<RobotDamageVisuals>();
            Vector3 playerSpawn = player.transform.position;
            Vector3 opponentSpawn = opponent.transform.position;
            playerHealth.Damaged += _ => botDamagedPlayer = true;
            opponentHealth.Damaged += _ => playerDamagedOpponent = true;
            match.ResultDetermined += _ => results++;
            for (int round = 1; round <= 2; round++)
            {
                Key startKey = round == 1 ? Key.Enter : Key.R;
                bool sawCountdown = false;
                float startDeadline = Time.realtimeSinceStartup + 10f;
                while (match.State != MatchController.MatchState.Playing || match.MatchCount != round)
                {
                    Require(Time.realtimeSinceStartup < startDeadline,
                        $"Keyboard start timed out round={round} state={match.State} matchCount={match.MatchCount} " +
                        $"currentKeyboard={Keyboard.current?.deviceId} injectedKeyboard={keyboard.deviceId} " +
                        $"enabled={keyboard.enabled} key={startKey} pressed={keyboard[startKey].isPressed} " +
                        $"applicationFocused={Application.isFocused} initialized={match.IsInitialized}");
                    if (match.State == MatchController.MatchState.Countdown)
                    {
                        sawCountdown = true;
                        Require(!match.PlayerAllowed && !match.OpponentAllowed &&
                                player.CombatStopped && opponent.CombatStopped && match.MatchCount == round - 1,
                            "Countdown locks both robots until the round starts");
                    }
                    Keys(startKey);
                    yield return null;
                }
                Keys();
                Require(sawCountdown, "Each keyboard-started round shows the countdown");
                Debug.Log($"ARENA_SMOKE START round={round} state={match.State} matchCount={match.MatchCount}");
                Require(match.ResultCount == 0 && match.Outcome == MatchController.MatchOutcome.None, "Result UI reset");
                Require(playerHealth.CurrentHealth == playerHealth.MaxHealth && opponentHealth.CurrentHealth == opponentHealth.MaxHealth, "Full health after start");
                Require(playerDamage.DetachedCount == 0 && opponentDamage.DetachedCount == 0, "Detached parts reset");
                Require(playerDamage.Parts.All(part => part.Damage == 0) && opponentDamage.Parts.All(part => part.Damage == 0), "Regional wear reset");
                Require(PlanarDistance(player.transform.position, playerSpawn) < .2f && PlanarDistance(opponent.transform.position, opponentSpawn) < .2f, "Spawn positions reset");
                Require(!player.WeaponActive && !opponent.WeaponActive, "Weapon command reset");
                var bot = opponent.GetComponent<RobotBotController>();
                Require(bot != null && bot.enabled && bot.MatchRunning && bot.Target == player.transform, "Bot restored with current target");
                playerDamagedOpponent = botDamagedPlayer = regionalDamage = false;
                float yaw = player.transform.eulerAngles.y;
                float turnEnd = Time.time + .22f;
                while (Time.time < turnEnd)
                {
                    if (round == 1) Keys(Key.W, Key.D);
                    else Keys(Key.UpArrow, Key.RightArrow);
                    yield return null;
                }
                var driver = player.GetComponent<RobotSavaslari.Driving.PlayerKeyboardDriver>();
                Require(Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)) > 3f,
                    $"Real keyboard turning round={round} yawStart={yaw} yawNow={player.transform.eulerAngles.y} " +
                    $"driverEnabled={driver.enabled} driverFocus={driver.HasFocus} forward={driver.ForwardInput} turn={driver.TurnInput} keyboard={Keyboard.current?.deviceId}");
                float deadline = Time.time + 100f;
                bool playerMoved = false;
                bool botMoved = false;
                while (match.State == MatchController.MatchState.Playing)
                {
                    Require(Time.time < deadline,
                        $"Real combat timeout round={round} playerHealth={playerHealth.CurrentHealth} opponentHealth={opponentHealth.CurrentHealth} " +
                        $"botState={bot.State} botDistance={bot.Distance} playerHits={playerDamagedOpponent} botHits={botDamagedPlayer}");
                    playerMoved |= PlanarDistance(player.transform.position, playerSpawn) > .4f;
                    botMoved |= PlanarDistance(opponent.transform.position, opponentSpawn) > .4f;
                    regionalDamage |= playerDamage.Parts.Any(part => part.Damage > 0) || opponentDamage.Parts.Any(part => part.Damage > 0);
                    Vector3 offset = opponent.transform.position - player.transform.position;
                    offset.y = 0;
                    float angle = Vector3.SignedAngle(player.transform.forward, offset, Vector3.up);
                    var keys = new List<Key> { Key.Space };
                    if (angle > 8f) keys.Add(Key.D);
                    if (angle < -8f) keys.Add(Key.A);
                    if (Mathf.Abs(angle) < 55f)
                        keys.Add(Time.time % 5f < .8f ? Key.S : Key.W);
                    Keys(keys.ToArray());
                    yield return null;
                }
                Keys();
                Require(playerMoved && botMoved, "Player and bot moved physically");
                Require(playerDamagedOpponent && botDamagedPlayer, "Both robots took genuine contact damage");
                Require(regionalDamage, "Contact changed regional wear");
                Require(match.ResultCount == 1 && results == round, "Exactly one result for each match");
                Require(playerHealth.IsDefeated || opponentHealth.IsDefeated, "Actual health loss determined result");
                Require(player.CombatStopped && opponent.CombatStopped && !bot.MatchRunning, "Combat and bot stop at result");
                Debug.Log($"ARENA_SMOKE ROUND {round} PASS outcome={match.Outcome} playerHealth={playerHealth.CurrentHealth} opponentHealth={opponentHealth.CurrentHealth} detached={playerDamage.DetachedCount + opponentDamage.DetachedCount}");
                yield return new WaitForSeconds(.2f);
                Require(match.ResultCount == 1 && results == round, "No repeated result while stopped");
            }
        }

        private void Keys(params Key[] keys)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Finish(bool passed, string detail)
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (previousSettings != null) InputSystem.settings = previousSettings;
            if (smokeSettings != null) Destroy(smokeSettings);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (passed) Debug.Log("ARENA_SMOKE PASS " + detail);
            else Debug.LogError("ARENA_SMOKE FAIL " + detail);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            Application.Quit(passed ? 0 : 1);
#endif
        }
    }
}
