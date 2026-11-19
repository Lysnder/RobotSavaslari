using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Arena
{
    public sealed class ArenaHazardCatalog : ScriptableObject
    {
        public ArenaLayout[] layouts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= Register;
            SceneManager.sceneLoaded += Register;
        }

        private static void Register(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Scenes/Gameplay/MainArena.unity") return;
            var catalog = Resources.Load<ArenaHazardCatalog>("ArenaHazards/ArenaHazardCatalog");
            if (catalog == null || catalog.layouts == null)
            {
                Debug.LogWarning("Arena hazard catalog is missing.");
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                var arena = root.GetComponentInChildren<ArenaEnvironment>(true);
                if (arena == null) continue;
                foreach (var layout in catalog.layouts) arena.RegisterLayout(layout);
            }
        }
    }
}
