using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.Match
{
    // Explicit opt-in only: controlled rule checks, followed by a separate-process history check.
    public sealed class MatchRulesSmoke : MonoBehaviour
    {
        private MatchController match;
        private string historyPath, phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-match-rules-smoke");
            if (index < 0) return;
            var smoke = new GameObject("Match Rules Verification").AddComponent<MatchRulesSmoke>();
            smoke.phase = index + 1 < args.Length ? args[index + 1] : "write";
            int path = Array.IndexOf(args, "-match-history-path");
            if (path >= 0 && path + 1 < args.Length) smoke.historyPath = args[path + 1];
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            match = FindFirstObjectByType<MatchController>();
            IEnumerator check = Verify();
            var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(check);
            while (stack.Count > 0)
            {
                object wait;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    wait = stack.Peek().Current;
                }
                catch (Exception error)
                {
                    Debug.LogError("MATCH_RULES FAIL " + error);
                    Application.Quit(1);
                    yield break;
                }
                if (wait is IEnumerator nested) stack.Push(nested); else yield return wait;
            }
            Debug.Log("MATCH_RULES PASS phase=" + phase);
            Application.Quit(0);
        }

        private IEnumerator Verify()
        {
            Require(match != null && match.IsInitialized, "MainArena initialized");
            Require(!string.IsNullOrEmpty(historyPath), "Use an explicit verification history path");
            match.HistoryStore = new MatchHistoryStore(historyPath);
            if (phase == "read")
            {
                var history = match.HistoryStore.Load();
                Require(history.Count >= 3, "History survives a fresh Windows process");
                Require(match.Rematch(history[0]), "Rematch accepts saved basic rules");
                Require(match.PlayerWins + match.OpponentWins == 0 && match.ResultCount == 0, "Rematch clean scores");
                Require(match.Player.GetComponent<RobotHealth>().CurrentHealth == match.Player.GetComponent<RobotHealth>().MaxHealth, "Rematch clean health");
                match.SetManualMode(true);
                yield return new WaitForSeconds(3.1f);
                Require(match.State == MatchController.MatchState.Playing, "Rematch uses countdown");
                Require(match.HistoryStore.Load().Count == history.Count, "No record from unfinished rematch");
                yield break;
            }

            match.SetManualMode(true);
            Require(match.ConfigureRules(new MatchRules { mode = MatchMode.FirstToTwo, timeLimitSeconds = 60 }), "Choose series");
            match.StartMatch(); End(false);
            Require(match.PlayerWins == 1 && !match.EncounterComplete, "First round only");
            yield return Next(); End(false);
            Require(match.IsSeriesComplete && match.PlayerWins == 2 && !match.ContinueSeries(), "2-0 stops series");
            Debug.Log("MATCH_RULES 2-0 PASS " + JsonUtility.ToJson(match.LastMatch));
            Require(match.HistoryStore.Load().Count == 1, "One completed encounter saved");
            yield return Capture("series-two-zero");

            match.Prepare(); match.StartMatch(); End(false);
            yield return Next(); End(true);
            Require(!match.IsSeriesComplete && match.PlayerWins == 1 && match.OpponentWins == 1, "1-1 continues");
            yield return Next(); End(false);
            Require(match.IsSeriesComplete && match.LastMatch.roundsPlayed == 3 && !match.ContinueSeries(), "2-1 stops, no fourth scored round");
            Debug.Log("MATCH_RULES 2-1 PASS " + JsonUtility.ToJson(match.LastMatch));
            yield return Capture("series-two-one");

            match.Prepare();
            Require(match.ConfigureRules(new MatchRules { mode = MatchMode.Single, timeLimitSeconds = 1 }), "Choose timed round");
            match.StartMatch();
            float remaining = match.RemainingSeconds;
            var settings = match.GetComponent<UI.SettingsPanelController>(); settings.ToggleSettings();
            yield return new WaitForSecondsRealtime(.25f);
            Require(Mathf.Approximately(remaining, match.RemainingSeconds) && match.ResultCount == 0, "Pause freezes timer/result");
            settings.ToggleSettings();
            yield return new WaitForSeconds(1.1f);
            Require(match.Outcome == MatchController.MatchOutcome.Draw && match.LastRound.reason == RoundFinishReason.TimeOver, "Equal ratios draw");
            Require(match.HistoryStore.Load().Count == 3, "Draw saved once");
            Debug.Log("MATCH_RULES TIMEOVER DRAW PASS " + JsonUtility.ToJson(match.LastMatch));
            Require(!match.NotifyDefeat(match.Opponent.gameObject, match.MatchCount), "No double result");
            Require(match.HistoryStore.Load().Count == 3, "No duplicate history");
            yield return Capture("time-over-draw");
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(historyPath)), name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }

        private void End(bool playerLoses)
        {
            var victim = (playerLoses ? match.Player : match.Opponent).GetComponent<RobotHealth>();
            var source = (playerLoses ? match.Opponent : match.Player).GetComponent<RobotHealth>();
            victim.TakeDamage(1000, victim.transform.position, Vector3.up, Vector3.zero, true, source);
            Require(match.State == MatchController.MatchState.Result && match.ResultCount == 1, "One actual health result");
            Require(match.LastRound.player.dealtDamage + match.LastRound.opponent.dealtDamage == 100, "Clamped final hit counted");
        }

        private IEnumerator Next()
        {
            Require(match.RestartMatch(), "Next controlled round");
            yield return null; yield return null;
            Require(match.State == MatchController.MatchState.Playing, "Round resumed");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
