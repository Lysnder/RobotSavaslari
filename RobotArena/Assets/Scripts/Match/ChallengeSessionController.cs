using System;
using System.Collections.Generic;
using System.IO;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Presentation;
using RobotSavaslari.Training;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Match
{
    [DisallowMultipleComponent]
    public sealed class ChallengeSessionController : MonoBehaviour
    {
        private MatchController _match;
        private MainMenuScreen _menu;
        private ArenaEnvironment _arena;
        private ChallengeSessionStore _store;
        private ChallengeSave _save;
        private ChallengeDefinition _definition;
        private ChallengeSession _pending;
        private MatchRules _normalRules;
        private ArenaLayout _normalArena;
        private BotPersonality _normalPersonality;
        private BotDifficulty _normalDifficulty;
        private int _playedStage = -1;
        private readonly List<(Behaviour component, bool enabled)> _muted = new List<(Behaviour, bool)>();

        public bool DialogOpen { get; private set; }
        public bool InChallenge => _normalRules != null;
        public string Notice { get; private set; }
        public bool CanRecover => _store != null && _store.ReadFailed;
        public bool PendingSave => _pending != null;
        public bool HasCheckpoint => _save?.Active != null;
        public int ArchiveCount => _save?.archive.Count ?? 0;
        public ChallengeSession Session => _pending ?? _save?.Active;
        public ChallengeSession Archived(int index) => _save.archive[index];
        public bool Complete => Session != null && Session.nextStageIndex == StageCount;
        public int StageCount => _definition != null ? _definition.stages.Length : 0;
        public string Title => _definition != null ? _definition.title : "Yerel mücadele";
        public ChallengeStage NextStage => _definition != null && (Session?.nextStageIndex ?? 0) < StageCount
            ? _definition.stages[Session?.nextStageIndex ?? 0] : null;
        public int DisplayStage => _playedStage >= 0 ? _playedStage + 1 : Mathf.Min(Session?.nextStageIndex + 1 ?? 1, StageCount);
        private OpponentSelectionPanel OpponentPanel => GetComponent<OpponentSelectionPanel>();

        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _menu = GetComponent<MainMenuScreen>();
            _definition = Resources.Load<ChallengeDefinition>("Challenges/LocalCircuit");
            if (_definition == null || !_definition.IsValid)
            {
                _definition = null;
                Notice = "Yerel mücadele tanımı bulunamadı veya geçersiz.";
                return;
            }
            UseSavePath(Path.Combine(Application.persistentDataPath, "challenge-session.json"));
        }

        public void UseSavePath(string path)
        {
            _store = new ChallengeSessionStore(_definition, path);
            _save = _store.Load();
            Notice = _store.Notice;
        }

        private void OnEnable()
        {
            _match.ResultDetermined += OnResult;
            OnResult(_match.Outcome);
        }

        private void OnDisable()
        {
            _match.ResultDetermined -= OnResult;
            Close();
        }

        private bool CanInteract => isActiveAndEnabled && _menu != null && _menu.isActiveAndEnabled &&
            _match.isActiveAndEnabled && _match.IsInitialized && !_match.IsPaused && !PresentationOpen;

        private bool PresentationOpen
        {
            get
            {
                var camera = FindAnyObjectByType<CombatCameraController>();
                return camera != null && camera.HasPresentationTargets;
            }
        }

        public bool Open()
        {
            if (DialogOpen || !CanInteract) return false;
            if (InChallenge ? _match.State != MatchController.MatchState.Result : !_menu.IsShowingReadyMenu) return false;

            var driving = FindAnyObjectByType<DrivingFeatureInstaller>();
            var bindings = _match.Player.GetComponent<DriveBindingsScreen>();
            if ((bindings != null && (bindings.IsOpen || bindings.Capturing.HasValue)) ||
                (driving != null && driving.TimeTrial != null && driving.TimeTrial.State != TimeTrialState.Inactive))
                return false;

            DialogOpen = true;
            Mute(GetComponent<SettingsPanelController>());
            Mute(OpponentPanel);
            Mute(bindings);
            Mute(_match.Player.GetComponent<PlayerKeyboardDriver>());
            if (driving != null) Mute(driving.GetComponent<TimeTrialHud>());
            Mute(FindAnyObjectByType<CombatCameraController>());
            Mute(FindAnyObjectByType<ResultPresentationScreen>());
            Mute(FindAnyObjectByType<MatchReplayPlayer>());
            Mute(FindAnyObjectByType<PostMatchInspection>());
            return true;
        }

        private void Mute(Behaviour component)
        {
            if (component == null) return;
            _muted.Add((component, component.enabled));
            component.enabled = false;
        }

        public void Close()
        {
            DialogOpen = false;
            foreach (var (component, enabled) in _muted)
                if (component != null) component.enabled = enabled;
            _muted.Clear();
        }

        public bool Recover()
        {
            if (!DialogOpen || !CanInteract || !CanRecover) return false;
            bool restored = _store.PreserveUnreadableSave();
            Notice = _store.Notice;
            return restored;
        }

        public bool Begin(bool restart)
        {
            if (!DialogOpen || !CanInteract || InChallenge || _match.State != MatchController.MatchState.Ready ||
                _store == null || _store.ReadFailed) return false;
            if (Complete && !restart) return false;
            var next = restart ? null : _save.Active;
            int index = next?.nextStageIndex ?? 0;
            if (!ResolveStage(index, out _)) return false;
            if (next == null)
            {
                next = new ChallengeSession
                {
                    definitionId = _definition.definitionId,
                    definitionRevision = _definition.revision,
                    sessionId = Guid.NewGuid().ToString("N")
                };
                if (!Commit(_save.ChangeActive(next, restart))) return false;
            }

            _normalRules = _match.Rules;
            _normalArena = _arena.Layout;
            _normalPersonality = OpponentPanel.Personality;
            _normalDifficulty = OpponentPanel.Difficulty;
            Close();
            return StartStage();
        }

        public bool Continue()
        {
            if (DialogOpen || !CanInteract || !InChallenge || _match.State != MatchController.MatchState.Result ||
                !_match.EncounterComplete || Complete || PendingSave || _playedStage >= 0) return false;
            return StartStage();
        }

        private bool ResolveStage(int index, out int arenaIndex)
        {
            arenaIndex = -1;
            if (_arena == null)
                foreach (var arena in FindObjectsByType<ArenaEnvironment>(FindObjectsSortMode.None))
                    if (arena.Match == _match) { _arena = arena; break; }

            if (_definition != null && index >= 0 && index < StageCount && _arena != null && _arena.isActiveAndEnabled &&
                _arena.Layouts != null && OpponentPanel != null)
            {
                var stage = _definition.stages[index];
                for (int i = 0; i < _arena.Layouts.Count; i++)
                {
                    if (_arena.Layouts[i] != stage.arena) continue;
                    arenaIndex = i;
                    return true;
                }
            }
            Notice = "Bu aşamanın arena veya rakip bağlantısı kullanılamıyor.";
            return false;
        }

        private bool StartStage()
        {
            int index = _save.Active.nextStageIndex;
            if (!ResolveStage(index, out int arenaIndex)) return false;
            var stage = _definition.stages[index];
            _match.Prepare();
            if (!_arena.CanSelect || (_arena.Layout != stage.arena && !_arena.Select(arenaIndex)) ||
                !OpponentPanel.Select(stage.Personality, stage.Difficulty) ||
                !_match.ConfigureRules(stage.rules) || !_match.RequestCountdown())
            {
                Notice = "Aşama hazırlanamadı. Ana menüden kayıtlı aşamayı yeniden aç.";
                RestoreNormal();
                return false;
            }
            _playedStage = index;
            Notice = null;
            return true;
        }

        private void OnResult(MatchController.MatchOutcome outcome)
        {
            var encounter = _match.LastMatch;
            if (!InChallenge || _playedStage < 0 || _pending != null || !_match.EncounterComplete ||
                encounter == null || !encounter.IsValid) return;

            var active = _save.Active;
            if (active.nextStageIndex != _playedStage || active.results.Exists(result => result.encounter.id == encounter.id)) return;
            _pending = active.WithResult(_definition.stages[_playedStage], encounter);
            SaveResult();
        }

        public bool SaveResult()
        {
            if (_pending == null) return false;
            if (!Commit(_save.ChangeActive(_pending, false))) return false;
            _pending = null;
            _playedStage = -1;
            return true;
        }

        public bool Leave(bool discard)
        {
            if (!CanInteract || PendingSave || !InChallenge || _match.State != MatchController.MatchState.Result ||
                (!DialogOpen && (!Complete || discard))) return false;

            if ((discard || Complete) && !Commit(_save.ChangeActive(null, Complete && !discard))) return false;
            Close();
            RestoreNormal();
            return true;
        }

        private void RestoreNormal()
        {
            _match.Prepare();
            for (int i = 0; i < _arena.Layouts.Count; i++)
                if (_arena.Layouts[i] == _normalArena && _arena.Layout != _normalArena) { _arena.Select(i); break; }
            OpponentPanel.Select(_normalPersonality, _normalDifficulty);
            _match.ConfigureRules(_normalRules);
            _normalRules = null;
            _playedStage = -1;
        }

        private bool Commit(ChallengeSave next)
        {
            if (!_store.Save(next))
            {
                Notice = _store.Notice;
                return false;
            }
            _save = next;
            Notice = null;
            return true;
        }
    }
}
