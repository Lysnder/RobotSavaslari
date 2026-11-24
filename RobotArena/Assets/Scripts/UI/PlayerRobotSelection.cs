using RobotSavaslari.Arena;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerRobotSelection : MonoBehaviour
    {
        private MatchController match;
        private MainMenuScreen menu;
        private ArenaEnvironment arena;
        private MatchReplayArchivePlayer archive;
        private ForkLoadout kit;
        private RobotDamageVisuals.Part weapon;
        private Mesh spinnerMesh, forkMesh;
        private Material spinnerMaterial;
        private string playerName, spinnerName, notice;
        private Vector3 spinnerPivot, forkPivot;
        private CombatProfile spinnerProfile;
        private WorkshopSelection workshop;
        public bool ForkSelected { get; private set; }
        public string Notice => workshop != null && !string.IsNullOrEmpty(workshop.Notice) ? workshop.Notice : notice;
        internal CombatProfile ProfileFor(bool fork) => fork ? kit?.profile : spinnerProfile;
        internal string StockName(bool fork) => fork ? playerName + " - Kama" : playerName;

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
                foreach (var controller in root.GetComponentsInChildren<MatchController>(true))
                    if (controller.TryGetComponent<MainMenuScreen>(out _) && !controller.TryGetComponent<PlayerRobotSelection>(out _))
                        controller.gameObject.AddComponent<PlayerRobotSelection>();
        }

        private void Awake()
        {
            match = GetComponent<MatchController>();
            menu = GetComponent<MainMenuScreen>();
            arena = FindAnyObjectByType<ArenaEnvironment>();
            archive = GetComponent<MatchReplayArchivePlayer>();
            var prefab = Resources.Load<GameObject>("Robots/Kama");
            if (prefab != null) kit = prefab.GetComponent<ForkLoadout>();
            workshop = GetComponent<WorkshopSelection>() ?? gameObject.AddComponent<WorkshopSelection>();
        }

        public bool CanSelect => isActiveAndEnabled && match != null && match.IsInitialized && menu != null &&
            menu.IsShowingReadyMenu && (archive == null || !archive.IsArchiveModeOpen);

        public bool SelectFork(bool useFork)
        {
            return workshop != null && workshop.SelectStock(useFork);
        }

        internal bool FitStockWeapon(bool useFork)
        {
            if (!CanSelect) return false;
            if (useFork == ForkSelected) return true;
            if (!CanFitWeapon(useFork))
            {
                notice = "Kama kullanılamıyor. Döner seçili.";
                return false;
            }
            Apply(useFork);
            notice = null;
            return true;
        }

        internal bool CanFitWeapon(bool useFork) => !useFork || (arena != null && arena.Layout != null &&
            ArenaLayoutValidator.Validate(arena.Layout, arena.ClearanceRadius).Count == 0 && PrepareKit());

        internal bool PrepareKit()
        {
            if (kit == null || match.Player == null) return false;
            if (forkMesh != null) return true;
            var damage = match.Player.GetComponent<RobotDamageVisuals>();
            damage.Initialize();
            foreach (var part in damage.Parts)
                if (part.Kind == RobotDamageVisuals.PartKind.Weapon && part.Name == "Spinner") weapon = part;
            if (weapon == null || !match.Player.TryGetComponent(out MeleeAttack melee) || melee.Profile == null) return false;
            var parent = weapon.Transform.parent;
            forkMesh = kit.BuildMesh(match.Player.transform, parent);
            if (forkMesh == null) return false;
            spinnerName = weapon.Name;
            spinnerMesh = weapon.Mesh;
            spinnerMaterial = weapon.Transform.GetComponent<MeshRenderer>().sharedMaterial;
            spinnerPivot = weapon.RestPosition;
            spinnerProfile = melee.Profile;
            playerName = match.Player.name;
            forkPivot = parent.InverseTransformPoint(match.Player.transform.TransformPoint(kit.hinge));
            return true;
        }

        private void Apply(bool useFork)
        {
            // Keep the same damage Part so hits, wear, debris, the animator and replay all follow the selected weapon.
            weapon.Name = useFork ? "Lifter" : spinnerName;
            weapon.Transform.name = weapon.Name;
            weapon.Mesh = useFork ? forkMesh : spinnerMesh;
            weapon.Transform.GetComponent<MeshFilter>().sharedMesh = weapon.Mesh;
            weapon.Wear.GetComponent<MeshFilter>().sharedMesh = weapon.Mesh;
            weapon.Transform.GetComponent<MeshRenderer>().sharedMaterial = useFork ? kit.material : spinnerMaterial;
            weapon.RestPosition = useFork ? forkPivot : spinnerPivot;
            weapon.Transform.localPosition = weapon.RestPosition;
            weapon.Transform.localRotation = weapon.RestRotation;
            match.Player.name = useFork ? playerName + " - Kama" : playerName;
            match.Player.SelectWeaponProfile(useFork ? kit.profile : spinnerProfile);
            match.Player.GetComponent<RobotModelAnimator>().BindParts();
            ForkSelected = useFork;
        }

        private void OnDestroy()
        {
            // DamageVisuals owns the currently fitted runtime mesh; release the spare.
            if (forkMesh != null) Destroy(ForkSelected ? spinnerMesh : forkMesh);
        }

        internal void DrawReadyCard(float width, float height)
        {
            if (!CanSelect) return;
            float available = width - 340f - 384f;
            float panelWidth = Mathf.Min(280f, available - 12f);
            bool compact = panelWidth < 185f;
            var panel = new Rect(340f + (available - panelWidth) * .5f, height - 148f, panelWidth, 108f);
            ArenaMenuStyle.Panel(panel);
            var theme = ArenaMenuStyle.Current;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, 24f), "ROBOT", theme.Heading);
            string label = workshop.IsModular ? "‹  Atölye robotu  ›" : ForkSelected ? "‹  Kama  ›" : "‹  Döner  ›";
            if (GUI.Button(new Rect(panel.x + 7f, panel.y + 32f, panel.width - 14f, 32f), label, theme.Selected))
                SelectFork(!workshop.IsModular && !ForkSelected);
            string hint = ForkSelected ? (compact ? "Yakın itiş" : "Yakın menzil · önden itiş") :
                (compact ? "Geniş yay" : "Döner silah · geniş vuruş yayı");
            GUI.Label(new Rect(panel.x + 7f, panel.y + 65f, panel.width - 14f, 41f),
                Notice ?? (workshop.IsModular ? "Hazır robota geç" : hint), theme.Hint);
        }
    }
}
