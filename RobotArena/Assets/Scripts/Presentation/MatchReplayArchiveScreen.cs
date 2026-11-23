using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Screen side of the replay archive. On the normal Ready menu it adds one "Tekrar arşivi" button in
    /// the top-right second row (y 60-96 at the 720p reference). The archive itself is a modal panel:
    /// list, open, confirmed delete and quota clean-up; then the playback bar with the event timeline,
    /// camera targets and part inspection. On the result screen it reports what happened to the
    /// encounter's recording. Nothing is drawn (so nothing can be clicked) while settings are open or
    /// when the archive is not allowed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchReplayArchiveScreen : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        // MainMenuScreen draws its result panel centred at this size; ResultPresentationScreen adds a 44 px button row under it.
        private const float ResultPanelHeight = 218f;
        public static readonly Rect EntryRect = new Rect(-174f, 60f, 158f, 36f);

        private sealed class Row
        {
            public MatchReplayArchiveStore.Entry Entry;
            public string Compatibility;
            public bool InHistory;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<KeyValuePair<string, bool>> _parts = new List<KeyValuePair<string, bool>>();
        private MatchController _match;
        private SettingsPanelController _settings;
        private MatchReplayArchiveRecorder _recorder;
        private MatchReplayArchivePlayer _player;
        private MatchReplayPlayer _liveReplay;
        private PostMatchInspection _inspection;
        private MatchReplayArchiveStore.Entry _pending;
        private string _notice;
        private string _confirmDelete;
        private bool _confirmDiscardPending;
        private long _usedBytes;
        private Vector2 _listScroll, _eventScroll;
        private GUIStyle _titleStyle, _smallStyle, _warnStyle, _goodStyle, _wrapStyle;

        public IReadOnlyList<MatchReplayArchiveStore.Entry> Entries
        {
            get
            {
                var list = new List<MatchReplayArchiveStore.Entry>();
                foreach (Row row in _rows) list.Add(row.Entry);
                return list;
            }
        }

        public string Notice => _notice;
        public MatchReplayArchiveStore.Entry PendingEntry => _pending;
        /// <summary>The entry button is offered this frame (Ready menu, nothing else open).</summary>
        public bool IsEntryVisible => _player != null && !_player.IsArchiveModeOpen && _player.CanOpenArchiveMode(out _);

        public void Bind(MatchController match, MatchReplayArchiveRecorder recorder, MatchReplayArchivePlayer player,
            MatchReplayPlayer liveReplay, PostMatchInspection inspection)
        {
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _recorder = recorder;
            _player = player;
            _liveReplay = liveReplay;
            _inspection = inspection;
        }

        // ---- Actions (also used by tests and the panel's buttons) ------------------------------------

        /// <summary>Opens the archive panel from the Ready menu. False with <see cref="Notice"/> when not allowed.</summary>
        public bool OpenArchive()
        {
            if (_player == null || !_player.OpenArchiveMode())
            {
                _notice = _player != null ? _player.LastError : "Tekrar arşivi kurulmadı.";
                return false;
            }
            _notice = null;
            _confirmDelete = null;
            _confirmDiscardPending = false;
            _listScroll = Vector2.zero;
            Refresh();
            return true;
        }

        public void CloseArchive()
        {
            _player?.CloseArchiveMode();
            _confirmDelete = null;
            _confirmDiscardPending = false;
            _rows.Clear();
        }

        public void Refresh()
        {
            _rows.Clear();
            MatchReplayArchiveStore store = _player != null ? _player.Store : null;
            if (store == null) return;
            if (store.TryPromotePending(out string promoted)) _notice = promoted;
            List<MatchReplayArchiveStore.Entry> entries = store.List();
            if (store.LastError != null) _notice = store.LastError;
            _usedBytes = MatchReplayArchiveStore.TotalBytes(entries);
            _pending = store.Pending();
            HashSet<string> history = HistoryIds();
            foreach (MatchReplayArchiveStore.Entry entry in entries)
                _rows.Add(new Row
                {
                    Entry = entry,
                    Compatibility = entry.IsReadable ? _player.CheckCompatibility(entry.Info) : "Bozuk: " + entry.Error,
                    InHistory = entry.Info != null && entry.Info.EncounterId != null && history.Contains(entry.Info.EncounterId),
                });
        }

        /// <summary>Plays one archive entry. False with <see cref="Notice"/> when it is broken, incompatible or not allowed.</summary>
        public bool Play(MatchReplayArchiveStore.Entry entry)
        {
            if (_player == null || entry == null) return false;
            if (!_player.Open(entry.Path))
            {
                _notice = _player.LastError;
                Refresh();
                return false;
            }
            _notice = null;
            _eventScroll = Vector2.zero;
            return true;
        }

        /// <summary>Deletes an archive the player confirmed. Only that archive's own file is removed.</summary>
        public bool ConfirmDelete(string id)
        {
            MatchReplayArchiveStore store = _player != null ? _player.Store : null;
            if (store == null || (_player.IsActive)) return false;
            bool deleted = store.Delete(id, out string error);
            _notice = deleted ? "Kayıt silindi." : error;
            _confirmDelete = null;
            Refresh();
            return deleted;
        }

        public bool DiscardPending()
        {
            MatchReplayArchiveStore store = _player != null ? _player.Store : null;
            if (store == null || _pending == null) return false;
            bool deleted = store.DiscardPending(_pending.Id, out string error);
            _notice = deleted ? "Bekleyen kayıt silindi." : error;
            _confirmDiscardPending = false;
            Refresh();
            return deleted;
        }

        private HashSet<string> HistoryIds()
        {
            var ids = new HashSet<string>();
            if (_match == null) return ids;
            foreach (MatchRecord record in _match.HistoryStore.Load())
                if (record != null && record.id != null) ids.Add(record.id);
            return ids;
        }

        // ---- Drawing ---------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_match == null || _player == null || !_match.IsInitialized || (_settings != null && _settings.IsOpen)) return;
            bool archive = _player.IsArchiveModeOpen;
            bool result = _match.State == MatchController.MatchState.Result;
            if (!archive && !result && _match.State != MatchController.MatchState.Ready) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (archive)
                {
                    if (_player.IsActive) DrawPlayback(width, height);
                    else DrawArchive(width, height);
                }
                else if (result) DrawResultStatus(width, height);
                else if (_player.CanOpenArchiveMode(out _))
                {
                    if (GUI.Button(new Rect(width + EntryRect.x, EntryRect.y, EntryRect.width, EntryRect.height), "Tekrar arşivi"))
                        OpenArchive();
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.enabled = previousEnabled;
                GUI.color = previousColor;
            }
        }

        private void DrawResultStatus(float width, float height)
        {
            if (_recorder == null || (_liveReplay != null && _liveReplay.IsActive) || (_inspection != null && _inspection.IsActive)) return;
            string text = _recorder.IsRecording
                ? $"Tekrar kaydı sürüyor: seri bitince arşive eklenecek ({_recorder.Current?.Rounds.Count ?? 0} raund)."
                : _recorder.LastStatusMatch == _match.MatchCount ? _recorder.LastStatus : null;
            if (string.IsNullOrEmpty(text)) return;
            float y = (height + ResultPanelHeight) * 0.5f + 64f;
            float w = Mathf.Min(560f, width - 32f);
            GUI.Label(new Rect((width - w) * 0.5f, y, w, 40f), text, _wrapStyle);
        }

        private void DrawArchive(float width, float height)
        {
            float panelWidth = Mathf.Min(780f, width - 32f);
            float panelHeight = Mathf.Min(600f, height - 32f);
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            GUI.Box(panel, string.Empty);
            GUI.Box(panel, string.Empty);
            float x = panel.x + 14f, w = panel.width - 28f, y = panel.y + 10f;
            GUI.Label(new Rect(x, y, w, 28f), "TEKRAR ARŞİVİ", _titleStyle);
            y += 30f;
            int count = _rows.Count;
            GUI.Label(new Rect(x, y, w, 20f),
                $"{count}/{MatchReplayArchiveStore.MaxRecordings} kayıt · {_usedBytes / (1024f * 1024f):0.0}/{MatchReplayArchiveStore.MaxArchiveBytes / (1024 * 1024)} MiB · " +
                "Eski kayıtlar kendiliğinden silinmez.", _smallStyle);
            y += 22f;
            if (count >= MatchReplayArchiveStore.MaxRecordings)
            {
                GUI.Label(new Rect(x, y, w, 20f), "Arşiv dolu: yeni karşılaşmaların kaydı için silinecek kaydı seçin.", _warnStyle);
                y += 22f;
            }
            if (_pending != null)
            {
                string title = _pending.Info != null ? _pending.Info.Title : "Okunamayan kayıt";
                GUI.Label(new Rect(x, y, w - 330f, 22f), "Bekleyen kayıt: " + title, _warnStyle);
                MatchReplayArchiveStore store = _player.Store;
                GUI.enabled = store != null && store.HasRoomFor(_pending.Bytes);
                if (GUI.Button(new Rect(x + w - 320f, y - 2f, 150f, 26f), "Arşive ekle"))
                {
                    if (store.TryPromotePending(out string message)) _notice = message;
                    else _notice = message;
                    Refresh();
                }
                GUI.enabled = true;
                if (!_confirmDiscardPending)
                {
                    if (GUI.Button(new Rect(x + w - 160f, y - 2f, 160f, 26f), "Bekleyeni sil...")) _confirmDiscardPending = true;
                }
                else if (GUI.Button(new Rect(x + w - 160f, y - 2f, 160f, 26f), "Evet, bekleyeni sil")) DiscardPending();
                y += 28f;
            }
            if (!string.IsNullOrEmpty(_notice))
            {
                GUI.Label(new Rect(x, y, w, 36f), _notice, _wrapStyle);
                y += 38f;
            }

            float listBottom = panel.yMax - 54f;
            var view = new Rect(x, y, w, Mathf.Max(40f, listBottom - y));
            const float rowHeight = 66f;
            if (count == 0) GUI.Label(new Rect(x, y + 6f, w, 24f), "Henüz kayıtlı karşılaşma tekrarı yok. Bir maç veya seri bitince burada görünür.");
            _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0f, 0f, w - 20f, Mathf.Max(1, count) * rowHeight));
            // A row's button can delete, refresh or start playback, which rebuilds the list: draw from a copy.
            Row[] rows = _rows.ToArray();
            for (int i = 0; i < rows.Length; i++) DrawRow(rows[i], i * rowHeight, w - 20f);
            GUI.EndScrollView();

            float buttonY = panel.yMax - 46f;
            if (GUI.Button(new Rect(x, buttonY, 150f, 36f), "Yenile"))
            {
                _notice = null;
                Refresh();
            }
            if (GUI.Button(new Rect(panel.xMax - 164f, buttonY, 150f, 36f), "Kapat")) CloseArchive();
        }

        private void DrawRow(Row row, float y, float w)
        {
            MatchReplayArchiveStore.Entry entry = row.Entry;
            ReplayArchiveInfo info = entry.Info;
            GUI.Box(new Rect(0f, y, w, 62f), string.Empty);
            float textWidth = w - 250f;
            if (info != null)
            {
                string date = info.CreatedLocal == System.DateTime.MinValue ? "tarih yok" : info.CreatedLocal.ToString("dd.MM.yyyy HH:mm");
                GUI.Label(new Rect(8f, y + 2f, textWidth, 20f), $"{info.Title}   {date}");
                GUI.Label(new Rect(8f, y + 21f, textWidth, 20f),
                    $"{FormatTime(info.Duration)} · {info.Rounds.Count} raund · {info.ArenaName ?? "arena"} · {info.Events.Count} olay · {entry.Bytes / (1024f * 1024f):0.0} MiB" +
                    (row.InHistory ? " · maç geçmişinde" : ""), _smallStyle);
                GUIStyle style = info.IsComplete ? _goodStyle : _warnStyle;
                string state = MatchReplayArchive.DescribeCompleteness(info) + (row.Compatibility == null ? " · Uyumlu" : " · " + row.Compatibility);
                GUI.Label(new Rect(8f, y + 40f, textWidth, 20f), state, row.Compatibility == null ? style : _warnStyle);
            }
            else
            {
                GUI.Label(new Rect(8f, y + 2f, textWidth, 20f), "Okunamayan kayıt " + entry.Id.Substring(0, 8));
                GUI.Label(new Rect(8f, y + 22f, textWidth, 36f), row.Compatibility, _warnStyle);
            }

            if (_confirmDelete == entry.Id)
            {
                if (GUI.Button(new Rect(w - 240f, y + 14f, 112f, 34f), "Evet, sil")) ConfirmDelete(entry.Id);
                if (GUI.Button(new Rect(w - 120f, y + 14f, 112f, 34f), "Vazgeç")) _confirmDelete = null;
                return;
            }
            GUI.enabled = info != null && row.Compatibility == null;
            if (GUI.Button(new Rect(w - 240f, y + 14f, 112f, 34f), "Aç")) Play(entry);
            GUI.enabled = true;
            if (GUI.Button(new Rect(w - 120f, y + 14f, 112f, 34f), "Sil...")) _confirmDelete = entry.Id;
        }

        private void DrawPlayback(float width, float height)
        {
            ReplayArchiveInfo info = _player.Info;
            if (info == null) return;
            float time = _player.PlaybackTime;
            float duration = Mathf.Max(0.01f, _player.Duration);
            bool showEvents = width >= 900f;
            float barWidth = Mathf.Min(showEvents ? width - 360f : width - 32f, 900f);
            var bar = new Rect(16f, height - 176f, barWidth, 160f);
            GUI.Box(bar, string.Empty);
            GUI.Box(bar, string.Empty);
            float x = bar.x + 12f, w = bar.width - 24f;

            string state = _player.IsSettingsOpen ? "duraklatıldı" : _player.InspectedRobot >= 0 ? "inceleme" : _player.IsPaused ? "duraklatıldı"
                : _player.IsFinished ? "bitti" : _player.Speed >= 1f ? "1x" : _player.Speed + "x";
            int roundIndex = RoundAt(info, time);
            string round = roundIndex >= 0 ? $"Raund {info.Rounds[roundIndex].RoundNumber} ({roundIndex + 1}/{info.Rounds.Count})" : "";
            GUI.Label(new Rect(x, bar.y + 6f, w, 24f), $"TEKRAR: {info.Title}   {FormatTime(time)} / {FormatTime(duration)}   {round}   ({state})", _titleStyle);

            var slider = new Rect(x, bar.y + 38f, w, 18f);
            DrawTimelineMarks(info, slider, duration);
            float seek = GUI.HorizontalSlider(slider, time, 0f, duration);
            if (!Mathf.Approximately(seek, time)) _player.Seek(seek);

            // The widest row is 656 px at full size.
            _buttonScale = Mathf.Clamp01(w / 656f);
            float row = bar.y + 64f;
            float bx = x;
            if (Button(ref bx, row, 104f, _player.IsPaused || _player.IsFinished ? "Oynat" : "Duraklat"))
                _player.SetPaused(!(_player.IsPaused || _player.IsFinished));
            if (Button(ref bx, row, 76f, SpeedLabel(_player.Speed))) _player.SetSpeed(NextSpeed(_player.Speed));
            if (Button(ref bx, row, 92f, "◀ Olay")) _player.StepEvent(-1);
            if (Button(ref bx, row, 92f, "Olay ▶")) _player.StepEvent(1);
            bx += 8f;
            GUI.enabled = _player.CurrentView != MatchReplayArchivePlayer.View.Player || _player.InspectedRobot >= 0;
            if (Button(ref bx, row, 84f, "Oyuncu")) { _player.SetView(MatchReplayArchivePlayer.View.Player); }
            GUI.enabled = _player.CurrentView != MatchReplayArchivePlayer.View.Opponent || _player.InspectedRobot >= 0;
            if (Button(ref bx, row, 76f, "Rakip")) { _player.SetView(MatchReplayArchivePlayer.View.Opponent); }
            GUI.enabled = _player.CurrentView != MatchReplayArchivePlayer.View.Duel;
            if (Button(ref bx, row, 76f, "Düello")) { _player.SetView(MatchReplayArchivePlayer.View.Duel); }
            GUI.enabled = true;

            row += 42f;
            bx = x;
            if (Button(ref bx, row, 150f, "Oyuncuyu incele")) _player.Inspect(MatchReplayRecorder.PlayerRobot);
            if (Button(ref bx, row, 150f, "Rakibi incele")) _player.Inspect(MatchReplayRecorder.OpponentRobot);
            if (Button(ref bx, row, 130f, "Listeye dön"))
            {
                _player.Stop();
                Refresh();
            }
            if (Button(ref bx, row, 110f, "Kapat")) CloseArchive();
            GUI.Label(new Rect(x, bar.yMax - 22f, w, 20f), "Sağ tık + sürükle: döndür · Tekerlek: yakınlaştır · C: kamera modu" +
                (info.IsComplete ? "" : " · " + MatchReplayArchive.DescribeCompleteness(info)), _smallStyle);

            if (_player.InspectedRobot >= 0) DrawInspection(height);
            if (showEvents) DrawEventList(info, width, height, time);
        }

        private float _buttonScale = 1f;

        /// <summary>Lays buttons out left to right; on narrow screens they shrink so none leaves the bar.</summary>
        private bool Button(ref float x, float y, float width, string label)
        {
            width *= _buttonScale;
            bool clicked = GUI.Button(new Rect(x, y, width, 34f), label);
            x += width + 6f * _buttonScale;
            return clicked;
        }

        private void DrawTimelineMarks(ReplayArchiveInfo info, Rect slider, float duration)
        {
            float start = _player.Reader != null ? _player.Reader.StartTime : 0f;
            Color previous = GUI.color;
            float lastX = float.NegativeInfinity;
            foreach (ReplayEvent item in info.Events)
            {
                float px = slider.x + Mathf.Clamp01((item.Time - start) / duration) * slider.width;
                bool boundary = item.Type != ReplayEventType.Impact;
                if (!boundary && px - lastX < 2f) continue;
                lastX = px;
                GUI.color = EventColor(item.Type);
                GUI.DrawTexture(new Rect(px - 1f, slider.y - 6f, boundary ? 3f : 2f, boundary ? 28f : 8f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void DrawEventList(ReplayArchiveInfo info, float width, float height, float time)
        {
            var area = new Rect(width - 336f, 16f, 320f, height - 32f);
            GUI.Box(area, "Olaylar");
            float start = _player.Reader != null ? _player.Reader.StartTime : 0f;
            var view = new Rect(area.x + 6f, area.y + 24f, area.width - 12f, area.height - 30f);
            const float rowHeight = 24f;
            _eventScroll = GUI.BeginScrollView(view, _eventScroll, new Rect(0f, 0f, view.width - 20f, info.Events.Count * rowHeight));
            int first = Mathf.Max(0, Mathf.FloorToInt(_eventScroll.y / rowHeight));
            int last = Mathf.Min(info.Events.Count, first + Mathf.CeilToInt(view.height / rowHeight) + 1);
            Color previous = GUI.color;
            for (int i = first; i < last; i++)
            {
                ReplayEvent item = info.Events[i];
                bool past = item.Time - start <= time + 0.001f;
                GUI.color = past ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (GUI.Button(new Rect(0f, i * rowHeight, view.width - 20f, rowHeight - 2f),
                        FormatTime(item.Time - start) + "  " + MatchReplayArchive.DescribeEvent(item, info), _smallStyle))
                    _player.Seek(item.Time - start);
            }
            GUI.color = previous;
            GUI.EndScrollView();
        }

        private void DrawInspection(float height)
        {
            int robot = _player.InspectedRobot;
            _player.GetPartStates(robot, _parts);
            var area = new Rect(16f, 16f, 300f, Mathf.Min(height - 210f, 60f + _parts.Count * 20f));
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 10f, area.y + 6f, area.width - 20f, 24f), robot == MatchReplayRecorder.PlayerRobot ? "OYUNCU PARÇALARI" : "RAKİP PARÇALARI", _titleStyle);
            float y = area.y + 32f;
            foreach (KeyValuePair<string, bool> part in _parts)
            {
                if (y > area.yMax - 20f) break;
                GUI.Label(new Rect(area.x + 10f, y, 160f, 20f), part.Key, part.Value ? _smallStyle : _warnStyle);
                GUI.Label(new Rect(area.x + 170f, y, 120f, 20f), part.Value ? "yerinde" : "kopmuş", part.Value ? _goodStyle : _warnStyle);
                y += 20f;
            }
        }

        private static int RoundAt(ReplayArchiveInfo info, float time)
        {
            int found = -1;
            for (int i = 0; i < info.Rounds.Count; i++)
                if (info.Rounds[i].StartTime <= time + 0.001f) found = i;
            return found;
        }

        private static Color EventColor(ReplayEventType type)
        {
            switch (type)
            {
                case ReplayEventType.Impact: return new Color(1f, 0.55f, 0.15f);
                case ReplayEventType.PartDetached: return new Color(1f, 0.25f, 0.2f);
                case ReplayEventType.RoundStart: return new Color(0.4f, 0.8f, 1f);
                case ReplayEventType.RoundEnd: return new Color(1f, 0.95f, 0.3f);
                default: return new Color(0.5f, 1f, 0.5f);
            }
        }

        private static string SpeedLabel(float speed) => speed >= 1f ? "1x" : speed >= 0.5f ? "0.5x" : "0.25x";

        private static float NextSpeed(float speed)
        {
            for (int i = 0; i < MatchReplayArchivePlayer.Speeds.Length; i++)
                if (Mathf.Approximately(MatchReplayArchivePlayer.Speeds[i], speed))
                    return MatchReplayArchivePlayer.Speeds[(i + 1) % MatchReplayArchivePlayer.Speeds.Length];
            return 1f;
        }

        public static string FormatTime(float seconds)
        {
            if (!MatchReplayArchive.IsFinite(seconds) || seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.0}";
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _warnStyle = new GUIStyle(_smallStyle) { fontStyle = FontStyle.Bold };
            _warnStyle.normal.textColor = new Color(1f, 0.55f, 0.35f);
            _goodStyle = new GUIStyle(_smallStyle);
            _goodStyle.normal.textColor = new Color(0.55f, 1f, 0.6f);
        }
    }
}
