using RobotSavaslari.Driving;
using RobotSavaslari.Training;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Match
{
    /// <summary>
    /// Sürüş özelliklerini çalışma anında MainArena'daki oyuncu robotuna bağlar
    /// ve maç sıfırlamalarıyla aynı hizada tutar.
    ///
    /// Sahne dosyasına dokunmadan kurulur: oyuncuya teslim edilen özellik ana
    /// sahnede çalışmak zorunda, ama MainArena YAML'ı bu kartların teslim alanı
    /// dışında. Bağlantı idempotenttir; iki kez kurulmaz ve bileşenler zaten
    /// varsa yeniden eklenmez.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DrivingFeatureInstaller : MonoBehaviour
    {
        private const string MainArenaSceneName = "MainArena";

        private MatchController match;
        private SettingsPanelController settings;
        private PlayerKeyboardDriver driver;
        private DriveBoost boost;
        private DriveHandbrake handbrake;
        private DriveBindingsScreen bindingsScreen;
        private MainMenuScreen menu;
        private TimeTrialSession timeTrial;
        private bool installed;
        private int lastMatchCount = -1;
        private MatchController.MatchState lastState = MatchController.MatchState.Result;

        /// <summary>Bağlanan boost bileşeni; tanı ve test için.</summary>
        public DriveBoost Boost => boost;

        /// <summary>Bağlanan el freni bileşeni; tanı ve test için.</summary>
        public DriveHandbrake Handbrake => handbrake;

        /// <summary>Bağlanan eşleme ekranı; tanı ve test için.</summary>
        public DriveBindingsScreen BindingsScreen => bindingsScreen;

        /// <summary>Bağlanan sürüş eğitimi; tanı ve test için.</summary>
        public TimeTrialSession TimeTrial => timeTrial;

        /// <summary>Eğitim sürüyor mu? Sürerken eşleme ekranına girilmez.</summary>
        private bool IsTrialActive => timeTrial != null && timeTrial.State != TimeTrialState.Inactive;

        /// <summary>Bağlantı kuruldu mu?</summary>
        public bool Installed => installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (SceneManager.GetActiveScene().name != MainArenaSceneName) return;
            if (FindAnyObjectByType<DrivingFeatureInstaller>() != null) return;
            new GameObject("Driving Features").AddComponent<DrivingFeatureInstaller>();
        }

        /// <summary>
        /// Oyuncu robotunu bulup eksik sürüş bileşenlerini ekler. Aynı çağrı
        /// defalarca yapılabilir; var olanı tekrar eklemez.
        /// </summary>
        public bool Install()
        {
            PlayerKeyboardDriver playerDriver = ResolvePlayerDriver();
            if (playerDriver == null)
            {
                return false;
            }

            driver = playerDriver;

            boost = driver.GetComponent<DriveBoost>();
            if (boost == null)
            {
                boost = driver.gameObject.AddComponent<DriveBoost>();
            }

            handbrake = driver.GetComponent<DriveHandbrake>();
            if (handbrake == null)
            {
                handbrake = driver.gameObject.AddComponent<DriveHandbrake>();
            }

            // Sürücü kendi Awake'inde bu bileşenleri arar; sonradan
            // eklendiklerinde referanslar burada verilir.
            driver.Boost = boost;
            driver.Handbrake = handbrake;

            DriveBoostGauge gauge = driver.GetComponent<DriveBoostGauge>();
            if (gauge == null)
            {
                gauge = driver.gameObject.AddComponent<DriveBoostGauge>();
            }

            gauge.Boost = boost;

            bindingsScreen = driver.GetComponent<DriveBindingsScreen>();
            if (bindingsScreen == null)
            {
                bindingsScreen = driver.gameObject.AddComponent<DriveBindingsScreen>();
            }

            bindingsScreen.Driver = driver;

            // Kayıtlı eşlemeler sürücüye uygulanır; kayıt yoksa varsayılanlar.
            DriveBindings.Apply(driver);

            // Eğitim oturumu bu nesnede durur: robotun üstünde değil, çünkü
            // rota işaretleri robotla birlikte hareket etmemeli.
            if (timeTrial == null)
            {
                timeTrial = GetComponent<TimeTrialSession>();
            }

            if (timeTrial == null)
            {
                timeTrial = gameObject.AddComponent<TimeTrialSession>();
            }

            TimeTrialHud trialHud = GetComponent<TimeTrialHud>();
            if (trialHud == null)
            {
                trialHud = gameObject.AddComponent<TimeTrialHud>();
            }

            trialHud.Session = timeTrial;

            GhostRunner ghostRunner = GetComponent<GhostRunner>();
            if (ghostRunner == null)
            {
                ghostRunner = gameObject.AddComponent<GhostRunner>();
            }

            ghostRunner.Session = timeTrial;
            trialHud.Ghost = ghostRunner;

            if (menu == null)
            {
                menu = FindAnyObjectByType<MainMenuScreen>();
            }

            if (settings == null)
            {
                settings = FindAnyObjectByType<SettingsPanelController>();
            }

            timeTrial.Bind(match, menu, settings, driver);

            installed = true;
            return true;
        }

        private PlayerKeyboardDriver ResolvePlayerDriver()
        {
            if (match == null)
            {
                match = FindAnyObjectByType<MatchController>();
            }

            if (match != null && match.Player != null)
            {
                PlayerKeyboardDriver matchDriver = match.Player.GetComponent<PlayerKeyboardDriver>();
                if (matchDriver != null)
                {
                    return matchDriver;
                }
            }

            // Maç henüz kurulmadıysa sahnedeki etkin sürücüye bağlan.
            foreach (PlayerKeyboardDriver candidate in
                     FindObjectsByType<PlayerKeyboardDriver>(FindObjectsSortMode.None))
            {
                if (candidate.isActiveAndEnabled)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void Update()
        {
            if (!installed || driver == null)
            {
                Install();
            }

            if (match == null || boost == null)
            {
                return;
            }

            if (settings == null)
            {
                settings = FindAnyObjectByType<SettingsPanelController>();
            }

            // Eşleme ekranına ve eğitime yalnızca kurulmuş, hazır bir maçta ve
            // ayarlar kapalıyken girilir; maç başlayınca ikisi de kapanır.
            bool readyAccess = match.IsInitialized
                               && match.State == MatchController.MatchState.Ready
                               && (settings == null || !settings.IsOpen);

            if (bindingsScreen != null)
            {
                bindingsScreen.AccessAllowed = readyAccess && !IsTrialActive;
            }

            if (timeTrial != null)
            {
                timeTrial.CanEnter = readyAccess && (bindingsScreen == null || !bindingsScreen.IsOpen);
            }

            bool newMatch = match.MatchCount != lastMatchCount;
            bool returnedToStart = match.State != lastState &&
                                   (match.State == MatchController.MatchState.Countdown ||
                                    match.State == MatchController.MatchState.Ready);

            if (newMatch || returnedToStart)
            {
                // Yeni maç ya da hazırlık: enerji, tüketim, aktif komut ve
                // geçici dönüş durumu temizlenir.
                boost.ResetState();

                if (handbrake != null)
                {
                    handbrake.ResetState();
                }
            }

            lastMatchCount = match.MatchCount;
            lastState = match.State;
        }
    }
}
