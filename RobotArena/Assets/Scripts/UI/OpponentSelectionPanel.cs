using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.UI
{
    // Runtime-only connection to the existing MainArena menu. Never creates the combat bot.
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class OpponentSelectionPanel : MonoBehaviour
    {
        public BotPersonality Personality { get; private set; } = BotPersonality.Aggressive;
        public BotDifficulty Difficulty { get; private set; } = BotDifficulty.Hard;
        private MatchController _match;
        private MainMenuScreen _menu;
        private SettingsPanelController _settings;
        private RobotBotController _appliedBot;
        private int _appliedMatch = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachCurrent() => Attach(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Scenes/Gameplay/MainArena.unity") return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var match in root.GetComponentsInChildren<MatchController>(true))
                    if (match.GetComponent<MainMenuScreen>() != null && match.GetComponent<OpponentSelectionPanel>() == null)
                        match.gameObject.AddComponent<OpponentSelectionPanel>();
        }
        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _menu = GetComponent<MainMenuScreen>();
            _settings = GetComponent<SettingsPanelController>();
        }
        public bool CanSelect => isActiveAndEnabled && _match != null && _match.IsInitialized &&
            _match.State == MatchController.MatchState.Ready && _menu != null && _menu.isActiveAndEnabled &&
            _settings != null && !_settings.IsOpen && Time.timeScale > 0;

        public bool Select(BotPersonality personality, BotDifficulty difficulty)
        {
            if (!CanSelect) return false;
            Personality = personality; Difficulty = difficulty; _appliedMatch = -1;
            return true;
        }
        private void Update() => ApplyToCombatBot();
        private void FixedUpdate() => ApplyToCombatBot();
        public void ApplyToCombatBot()
        {
            if (_match == null || _match.State != MatchController.MatchState.Playing || _match.Opponent == null) return;
            var bot = _match.Opponent.GetComponent<RobotBotController>();
            if (bot == null || (_appliedBot == bot && _appliedMatch == _match.MatchCount)) return;
            bot.ConfigureDecisions(Personality, Difficulty);
            _appliedBot = bot; _appliedMatch = _match.MatchCount;
        }
    }
}
