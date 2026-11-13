using RobotSavaslari.Driving;
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
        private PlayerKeyboardDriver driver;
        private DriveBoost boost;
        private DriveHandbrake handbrake;
        private bool installed;
        private int lastMatchCount = -1;
        private MatchController.MatchState lastState = MatchController.MatchState.Result;

        /// <summary>Bağlanan boost bileşeni; tanı ve test için.</summary>
        public DriveBoost Boost => boost;

        /// <summary>Bağlanan el freni bileşeni; tanı ve test için.</summary>
        public DriveHandbrake Handbrake => handbrake;

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
