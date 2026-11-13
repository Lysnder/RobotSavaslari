using NUnit.Framework;
using RobotSavaslari.Driving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Driving.Tests
{
    /// <summary>
    /// A2 eşleme kuralları: altı eylemin değişebilmesi, çakışmanın görünür
    /// biçimde reddedilmesi, ok takımının korunması, kaydın yeniden
    /// yüklenmesi, bozuk kaydın varsayılana dönmesi ve varsayılana dönüşün
    /// yalnızca sürüş anahtarlarına dokunması.
    /// </summary>
    public class DriveBindingsTests
    {
        private const string VolumeKey = "Settings.MasterVolume";

        private static readonly DriveAction[] AllActions =
        {
            DriveAction.Forward,
            DriveAction.Backward,
            DriveAction.TurnLeft,
            DriveAction.TurnRight,
            DriveAction.Boost,
            DriveAction.Handbrake,
        };

        [SetUp]
        public void SetUp()
        {
            ClearStoredBindings();
            DriveBindings.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            ClearStoredBindings();
            PlayerPrefs.DeleteKey(VolumeKey);
            DriveBindings.ResetToDefaults();
        }

        private static void ClearStoredBindings()
        {
            foreach (DriveAction action in AllActions)
            {
                PlayerPrefs.DeleteKey("Settings.Driving." + action);
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void VarsayilanlarWasdVeEkTuslardir()
        {
            Assert.AreEqual(Key.W, DriveBindings.Forward);
            Assert.AreEqual(Key.S, DriveBindings.Backward);
            Assert.AreEqual(Key.A, DriveBindings.TurnLeft);
            Assert.AreEqual(Key.D, DriveBindings.TurnRight);
            Assert.AreEqual(Key.LeftShift, DriveBindings.Boost);
            Assert.AreEqual(Key.LeftCtrl, DriveBindings.Handbrake);
        }

        [Test]
        public void AltiEylemDeEslenebilir()
        {
            Key[] replacements = { Key.I, Key.K, Key.J, Key.L, Key.U, Key.O };

            for (int index = 0; index < AllActions.Length; index++)
            {
                DriveAction action = AllActions[index];
                Assert.IsTrue(DriveBindings.TryAssign(action, replacements[index], out string rejection),
                    $"{action} eslenemedi: {rejection}");
                Assert.AreEqual(replacements[index], DriveBindings.Get(action));
            }
        }

        [Test]
        public void CakismaReddedilirVeSebepBildirilir()
        {
            // D zaten sağa dönüş için kullanılıyor.
            Assert.IsFalse(DriveBindings.TryAssign(DriveAction.Forward, Key.D, out string rejection));
            Assert.IsNotNull(rejection);
            StringAssert.Contains("saga donus", rejection);
            Assert.AreEqual(Key.W, DriveBindings.Forward, "Reddedilen atama eslemeyi degistirdi.");
        }

        [TestCase(Key.Escape)]
        [TestCase(Key.Enter)]
        [TestCase(Key.R)]
        [TestCase(Key.Space)]
        public void SahiplenilmeyenTuslarReddedilir(Key key)
        {
            Assert.IsTrue(DriveBindings.IsReserved(key), $"{key} sahiplenilmemeli.");
            Assert.IsFalse(DriveBindings.TryAssign(DriveAction.Boost, key, out string rejection));
            Assert.IsNotNull(rejection);
            Assert.AreEqual(Key.LeftShift, DriveBindings.Boost);
        }

        [TestCase(Key.UpArrow)]
        [TestCase(Key.DownArrow)]
        [TestCase(Key.LeftArrow)]
        [TestCase(Key.RightArrow)]
        public void OkTuslariBirincilEslemeOlamaz(Key key)
        {
            // Oklar yönlerin sabit yardımcısı; birincil eşleme olarak atanamaz,
            // böylece eşleme değişse de ok davranışı korunur.
            Assert.IsFalse(DriveBindings.TryAssign(DriveAction.Forward, key, out string rejection));
            Assert.IsNotNull(rejection);
            Assert.AreEqual(Key.W, DriveBindings.Forward);
        }

        [Test]
        public void KayitYenidenAcilistaYuklenir()
        {
            Assert.IsTrue(DriveBindings.TryAssign(DriveAction.Forward, Key.I, out _));
            Assert.IsTrue(DriveBindings.TryAssign(DriveAction.Boost, Key.U, out _));
            DriveBindings.Save();

            // Yeniden açılışı taklit et: bellekteki değerleri sıfırla, sonra yükle.
            DriveBindings.ResetToDefaults();
            Assert.AreEqual(Key.W, DriveBindings.Forward);

            DriveBindings.Load();

            Assert.AreEqual(Key.I, DriveBindings.Forward, "Kayitli esleme yeniden yuklenmedi.");
            Assert.AreEqual(Key.U, DriveBindings.Boost);
        }

        [Test]
        public void BozukKayitVarsayilanaDoner()
        {
            PlayerPrefs.SetString("Settings.Driving." + DriveAction.Forward, "BuBirTusDegil");
            PlayerPrefs.SetString("Settings.Driving." + DriveAction.Boost, string.Empty);
            PlayerPrefs.Save();

            DriveBindings.Load();

            Assert.AreEqual(Key.W, DriveBindings.Forward, "Bozuk kayit varsayilana donmedi.");
            Assert.AreEqual(Key.LeftShift, DriveBindings.Boost);
        }

        [Test]
        public void SahiplenilmeyenTusKayitliysaVarsayilanaDoner()
        {
            PlayerPrefs.SetString("Settings.Driving." + DriveAction.Boost, Key.Space.ToString());
            PlayerPrefs.Save();

            DriveBindings.Load();

            Assert.AreEqual(Key.LeftShift, DriveBindings.Boost,
                "Sahiplenilmeyen tus kayittan geri geldi.");
        }

        [Test]
        public void AyniTusIkiEylemdeKayitliysaTumuVarsayilanaDoner()
        {
            PlayerPrefs.SetString("Settings.Driving." + DriveAction.Forward, Key.I.ToString());
            PlayerPrefs.SetString("Settings.Driving." + DriveAction.Backward, Key.I.ToString());
            PlayerPrefs.Save();

            DriveBindings.Load();

            foreach (DriveAction action in AllActions)
            {
                Assert.AreEqual(DriveBindings.Default(action), DriveBindings.Get(action),
                    $"{action} varsayilana donmedi.");
            }
        }

        [Test]
        public void VarsayilanaDonusYalnizcaSurusAnahtarlariniEtkiler()
        {
            PlayerPrefs.SetFloat(VolumeKey, 0.42f);
            PlayerPrefs.Save();

            Assert.IsTrue(DriveBindings.TryAssign(DriveAction.Forward, Key.I, out _));
            DriveBindings.ResetToDefaults();
            DriveBindings.Save();

            Assert.AreEqual(Key.W, DriveBindings.Forward);
            Assert.AreEqual(0.42f, PlayerPrefs.GetFloat(VolumeKey, -1f), 0.0001f,
                "Surus sifirlamasi baska bir ayar anahtarina dokundu.");
        }

        [Test]
        public void AyniTusuAyniEylemeYenidenAtamakCakismaSayilmaz()
        {
            Assert.IsTrue(DriveBindings.TryAssign(DriveAction.Forward, Key.W, out string rejection), rejection);
            Assert.AreEqual(Key.W, DriveBindings.Forward);
        }
    }
}
