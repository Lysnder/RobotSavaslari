using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.UI
{
    internal sealed class ChallengeScreen
    {
        private int _archiveIndex = -1;
        private Vector2 _scroll;

        public void Open(ChallengeSessionController session)
        {
            if (!session.Open()) return;
            _archiveIndex = -1;
            _scroll = Vector2.zero;
        }

        public void DrawDialog(ChallengeSessionController session, float width, float height)
        {
            float panelWidth = Mathf.Min(560, width - 32);
            float panelHeight = Mathf.Min(450, height - 32);
            var theme = ArenaMenuStyle.Current;
            var panel = new Rect((width - panelWidth) / 2f, (height - panelHeight) / 2f, panelWidth, panelHeight);
            if (session.InChallenge)
                ArenaMenuStyle.Fill(new Rect(0, 0, width, height), ArenaMenuStyle.Canvas);
            else
                ArenaMenuStyle.DrawGarage(width, height, ArenaMenuStyle.Canvas);
            ArenaMenuStyle.Panel(panel);
            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.Label(session.Title, theme.Title);
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (!string.IsNullOrEmpty(session.Notice)) GUILayout.Label(session.Notice);
            if (_archiveIndex >= session.ArchiveCount) _archiveIndex = -1;
            var shown = _archiveIndex < 0 ? session.Session : session.Archived(_archiveIndex);
            if (shown != null) DrawSummary(shown);
            if (_archiveIndex < 0 && session.NextStage != null)
            {
                var stage = session.NextStage;
                string rules = stage.rules.mode == MatchMode.Single ? "Tek maç" : "İlk 2 galibiyet";
                string limit = stage.rules.timeLimitSeconds == 0 ? "Süresiz" : $"{stage.rules.timeLimitSeconds:0} sn / raund";
                GUILayout.Label($"Sıradaki aşama {(shown?.nextStageIndex ?? 0) + 1}/{session.StageCount}");
                GUILayout.Label($"{stage.arena.displayName} | {stage.Personality} / {stage.Difficulty} | {rules} | {limit}");
            }
            GUILayout.EndScrollView();

            if (session.ArchiveCount > 0)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Önceki kayıt")) _archiveIndex = (_archiveIndex + 1) % session.ArchiveCount;
                if (GUILayout.Button(_archiveIndex < 0 ? "Güncel oturum" : $"Güncel oturum ({_archiveIndex + 1}/{session.ArchiveCount})"))
                    _archiveIndex = -1;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (session.CanRecover)
            {
                if (GUILayout.Button("Okunamayan dosyayı sakla")) session.Recover();
            }
            else if (session.InChallenge)
            {
                if (!session.PendingSave && GUILayout.Button("Bırak ve kaydı koru")) session.Leave(false);
                if (!session.PendingSave && GUILayout.Button("Bırak ve kaydı sil")) session.Leave(true);
            }
            else
            {
                if (!session.Complete && session.NextStage != null &&
                    GUILayout.Button(session.HasCheckpoint ? "Kaldığın aşamadan başlat" : "Mücadeleyi başlat"))
                    session.Begin(false);
                if (session.HasCheckpoint && GUILayout.Button("Baştan başlat (özeti sakla)")) session.Begin(true);
            }
            if (GUILayout.Button("Geri", theme.Button)) session.Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public static void DrawProgress(ChallengeSessionController session, float width)
        {
            var last = session.Session.results.Count == 0 ? null : session.Session.results[session.Session.results.Count - 1];
            string progress = session.Complete ? "Mücadele tamamlandı" : $"Yerel mücadele | Aşama {session.DisplayStage}/{session.StageCount}";
            if (session.PendingSave) progress = "Mücadele sonucu kayıt bekliyor";
            GUI.Box(new Rect(16, 170, Mathf.Min(430, width - 32), 52), string.Empty);
            GUI.Label(new Rect(24, 172, 405, 22), progress);
            if (last != null)
                GUI.Label(new Rect(24, 194, 405, 22), $"Son biten aşama: {Outcome(last.encounter.outcome)} | {last.encounter.playerWins}-{last.encounter.opponentWins} | {last.encounter.durationSeconds:0.0} sn");
        }

        public void DrawResultButtons(ChallengeSessionController session, MatchController match, Rect left, Rect right, GUIStyle style)
        {
            if (session.PendingSave)
            {
                if (GUI.Button(left, "Kaydı tekrar dene", style)) session.SaveResult();
            }
            else if (!match.EncounterComplete)
            {
                string label = match.Outcome == MatchController.MatchOutcome.Draw ? "Raundu tekrarla (R)" : "Sonraki raund (R)";
                if (GUI.Button(left, label, style)) match.ContinueSeries();
            }
            else if (session.Complete)
            {
                if (GUI.Button(left, "Tamamla", style)) session.Leave(false);
            }
            else if (GUI.Button(left, "Devam", style)) session.Continue();

            if (GUI.Button(right, "Özet / Bırak", style)) Open(session);
        }

        private static void DrawSummary(ChallengeSession run)
        {
            int wins = 0, losses = 0, draws = 0;
            double seconds = 0;
            foreach (var stage in run.results)
            {
                var encounter = stage.encounter;
                GUILayout.Label($"{stage.stageId} | {stage.arenaId} | {stage.profileId}\n" +
                    $"{Outcome(encounter.outcome)}   {encounter.playerWins}-{encounter.opponentWins}   {encounter.durationSeconds:0.0} sn");
                seconds += encounter.durationSeconds;
                switch (encounter.outcome)
                {
                    case MatchController.MatchOutcome.PlayerVictory: wins++; break;
                    case MatchController.MatchOutcome.OpponentVictory: losses++; break;
                    case MatchController.MatchOutcome.Draw: draws++; break;
                }
            }
            GUILayout.Label($"Toplam: {wins} galibiyet | {losses} mağlubiyet | {draws} beraberlik | {seconds:0.0} sn");
        }

        private static string Outcome(MatchController.MatchOutcome outcome) =>
            outcome == MatchController.MatchOutcome.PlayerVictory ? "Galibiyet" :
            outcome == MatchController.MatchOutcome.OpponentVictory ? "Mağlubiyet" : "Berabere";
    }
}
