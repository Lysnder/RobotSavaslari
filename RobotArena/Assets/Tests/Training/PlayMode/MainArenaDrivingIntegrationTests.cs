using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Training.Tests
{
    /// <summary>
    /// Gerçek MainArena sahnesinde kurulum doğrulaması.
    ///
    /// Diğer sürüş testleri bileşenleri kendi kurduğu küçük sahnelerde ölçer;
    /// burada ölçülen şey başka: teslim edilen özelliklerin oyuncunun gerçekten
    /// oynadığı sahnede, sahne dosyasına dokunulmadan bağlanıp bağlanmadığı.
    /// Sahne YAML'ı bu kartların teslim alanı dışında olduğu için bağlantı
    /// çalışma anında kuruluyor ve bu yüzden sahneyle birlikte sınanması
    /// gerekiyor.
    ///
    /// Not: kurulumu başlatan <c>RuntimeInitializeOnLoadMethod</c> yalnızca
    /// oyun açılışındaki ilk sahne yüklemesinde çalışır. Test sahnesi önce
    /// yüklendiği için o kanca burada tetiklenmez; testler kurucuyu elle
    /// oluşturup <c>Install()</c> çağırır. Sınanan şey kancanın kendisi değil,
    /// kancanın yaptığı iş.
    /// </summary>
    public class MainArenaDrivingIntegrationTests
    {
        private const string ArenaSceneName = "MainArena";

        private GameObject host;
        private DrivingFeatureInstaller installer;
        private MatchController match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ArenaSceneName);

            // Sahnenin Awake/Start zinciri ve maç kurulumu tamamlansın.
            yield return null;
            yield return null;

            match = Object.FindAnyObjectByType<MatchController>();
            Assert.That(match, Is.Not.Null, "MainArena'da MatchController yok");

            host = new GameObject("Driving Features (test)");
            installer = host.AddComponent<DrivingFeatureInstaller>();

            // Kurucunun Update'i kendi kendine kurar; bir kare bekleyip
            // sonucu alıyoruz.
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.Destroy(host);
            }

            DriveBindings.ResetToDefaults();
        }

        [UnityTest]
        public IEnumerator SahnedekiOyuncuyaSurusBilesenleriBaglanir()
        {
            Assert.That(installer.Installed, Is.True, "Kurulum sahnede tamamlanmadı");

            Assert.That(match.Player, Is.Not.Null, "Maçın oyuncusu yok");
            GameObject robot = match.Player.gameObject;

            Assert.That(robot.GetComponent<PlayerKeyboardDriver>(), Is.Not.Null,
                "Oyuncu robotunda klavye sürücüsü yok");
            Assert.That(installer.Boost, Is.Not.Null, "Boost bağlanmadı");
            Assert.That(installer.Handbrake, Is.Not.Null, "El freni bağlanmadı");
            Assert.That(installer.BindingsScreen, Is.Not.Null, "Eşleme ekranı bağlanmadı");
            Assert.That(installer.TimeTrial, Is.Not.Null, "Eğitim oturumu bağlanmadı");

            // Bileşenler oyuncu robotunun üstünde durmalı, başka bir nesnede değil.
            Assert.That(installer.Boost.gameObject, Is.SameAs(robot));
            Assert.That(installer.Handbrake.gameObject, Is.SameAs(robot));

            // Rota işaretleri robotla birlikte hareket etmesin diye eğitim
            // oturumu robotun üstünde değil kurucunun nesnesinde durur.
            Assert.That(installer.TimeTrial.gameObject, Is.SameAs(host));

            // Sürücü sonradan eklenen bileşenleri görüyor olmalı.
            PlayerKeyboardDriver driver = robot.GetComponent<PlayerKeyboardDriver>();
            Assert.That(driver.Boost, Is.SameAs(installer.Boost), "Sürücü boost'u görmüyor");
            Assert.That(driver.Handbrake, Is.SameAs(installer.Handbrake), "Sürücü el frenini görmüyor");

            yield break;
        }

        [UnityTest]
        public IEnumerator IkinciKurulumBilesenleriCogaltmaz()
        {
            Assert.That(installer.Install(), Is.True);
            Assert.That(installer.Install(), Is.True);
            yield return null;

            GameObject robot = match.Player.gameObject;

            Assert.That(robot.GetComponents<DriveBoost>(), Has.Length.EqualTo(1),
                "Boost ikinci kurulumda çoğaldı");
            Assert.That(robot.GetComponents<DriveHandbrake>(), Has.Length.EqualTo(1),
                "El freni ikinci kurulumda çoğaldı");
            Assert.That(robot.GetComponents<DriveBindingsScreen>(), Has.Length.EqualTo(1),
                "Eşleme ekranı ikinci kurulumda çoğaldı");
            Assert.That(host.GetComponents<TimeTrialSession>(), Has.Length.EqualTo(1),
                "Eğitim oturumu ikinci kurulumda çoğaldı");
            Assert.That(host.GetComponents<GhostRunner>(), Has.Length.EqualTo(1),
                "Hayalet sürücüsü ikinci kurulumda çoğaldı");
        }

        [UnityTest]
        public IEnumerator KayitliEslemeSahnedekiSurucuyeUygulanir()
        {
            Assert.That(DriveBindings.TryAssign(DriveAction.Forward, Key.I, out string rejection), Is.True,
                "Eşleme reddedildi: " + rejection);

            // Kurucu her karede değil kurulumda uygular; yeniden kuruyoruz.
            Assert.That(installer.Install(), Is.True);
            yield return null;

            PlayerKeyboardDriver driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            Assert.That(driver.GetBinding(DriveAction.Forward), Is.EqualTo(Key.I),
                "Kayıtlı eşleme sahnedeki sürücüye ulaşmadı");
            Assert.That(driver.GetBinding(DriveAction.Boost), Is.EqualTo(DriveBindings.DefaultBoost),
                "Değiştirilmeyen eşleme bozuldu");
        }

        [UnityTest]
        public IEnumerator EgitimGirisCikisiMacSayaclariniDegistirmez()
        {
            TimeTrialSession trial = installer.TimeTrial;
            int matchCountBefore = match.MatchCount;
            int resultCountBefore = match.ResultCount;
            MatchController.MatchState stateBefore = match.State;

            trial.CanEnter = true;
            Assert.That(trial.Enter(), Is.True, "Eğitime girilemedi");
            Assert.That(trial.State, Is.Not.EqualTo(TimeTrialState.Inactive));

            // Eğitim sırasında maç bileşeni kapalı olmalı.
            Assert.That(match.enabled, Is.False, "Eğitimde maç bileşeni açık kaldı");

            yield return null;
            yield return new WaitForFixedUpdate();

            trial.Exit();
            yield return null;

            Assert.That(trial.State, Is.EqualTo(TimeTrialState.Inactive));
            Assert.That(match.enabled, Is.True, "Çıkışta maç bileşeni geri açılmadı");
            Assert.That(match.MatchCount, Is.EqualTo(matchCountBefore),
                "Eğitim maç sayacını artırdı");
            Assert.That(match.ResultCount, Is.EqualTo(resultCountBefore),
                "Eğitim sonuç sayacını artırdı");
            Assert.That(match.State, Is.EqualTo(stateBefore),
                "Eğitim maç durumunu değiştirdi");
        }

        [UnityTest]
        public IEnumerator EgitimdenSonraIkinciEgitimeGirilebilir()
        {
            TimeTrialSession trial = installer.TimeTrial;

            trial.CanEnter = true;
            Assert.That(trial.Enter(), Is.True, "Birinci eğitime girilemedi");
            yield return null;
            trial.Exit();
            yield return null;

            int matchCountBetween = match.MatchCount;

            trial.CanEnter = true;
            Assert.That(trial.Enter(), Is.True, "İkinci eğitime girilemedi");
            yield return null;
            trial.Exit();
            yield return null;

            Assert.That(match.MatchCount, Is.EqualTo(matchCountBetween),
                "İkinci eğitim maç sayacını artırdı");

            PlayerKeyboardDriver driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            Assert.That(driver, Is.Not.Null, "İkinci eğitim sonrası sürücü kayboldu");
        }

        [UnityTest]
        public IEnumerator MacDurumuDegisinceBoostVeElFreniBirakilir()
        {
            installer.Boost.SetBoostRequested(true);
            installer.Handbrake.SetHandbrakeRequested(true);
            Assert.That(installer.Boost.IsRequested, Is.True);
            Assert.That(installer.Handbrake.IsRequested, Is.True);

            Assert.That(match.RequestCountdown(), Is.True, "Geri sayım başlatılamadı");

            // Kurucunun Update'i durum değişimini görsün.
            yield return null;

            Assert.That(installer.Boost.IsRequested, Is.False,
                "Maç durumu değişince boost isteği bırakılmadı");
            Assert.That(installer.Handbrake.IsRequested, Is.False,
                "Maç durumu değişince el freni isteği bırakılmadı");
            Assert.That(installer.Boost.Energy, Is.EqualTo(installer.Boost.Capacity).Within(0.001f),
                "Maç durumu değişince enerji tazelenmedi");

            RobotMotor motor = match.Player.GetComponent<RobotMotor>();
            Assert.That(motor.SpeedModifier, Is.EqualTo(1f).Within(0.001f),
                "Geçici hız çarpanı bırakılmadı");
            Assert.That(motor.TurnModifier, Is.EqualTo(1f).Within(0.001f),
                "Geçici dönüş çarpanı bırakılmadı");
        }
    }
}
