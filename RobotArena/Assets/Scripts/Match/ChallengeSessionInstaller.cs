using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Match
{
    internal static class ChallengeSessionInstaller
    {
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
            foreach (var menu in root.GetComponentsInChildren<UI.MainMenuScreen>(true))
                if (!menu.TryGetComponent<ChallengeSessionController>(out _))
                    menu.gameObject.AddComponent<ChallengeSessionController>();
        }
    }
}
