using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Atalay.Tests
{
    /// <summary>
    /// A2 ölçümleri: iki ayar setinin fren mesafesi, dönüş yarıçapı ve yön
    /// değiştirme gecikmesi; ayrıca aynı sürüşün farklı fizik adımlarında aynı
    /// sonucu verip vermediği. Sayılar test çıktısına yazılır, teslim notundaki
    /// gözlemler bu koşudan gelir.
    /// </summary>
    public class DriveFeelMeasurementTests
    {
        /// <summary>Bir ayar setinin motor değerleri.</summary>
        public readonly struct SettingSet
        {
            public readonly string Name;
            public readonly float MaxForwardSpeed;
            public readonly float MaxReverseSpeed;
            public readonly float Acceleration;
            public readonly float BrakeDeceleration;
            public readonly float MaxTurnSpeed;
            public readonly float TurnAcceleration;
            public readonly float TurnBrakeDeceleration;

            public SettingSet(string name, float maxForward, float maxReverse, float acceleration,
                float brake, float maxTurn, float turnAcceleration, float turnBrake)
            {
                Name = name;
                MaxForwardSpeed = maxForward;
                MaxReverseSpeed = maxReverse;
                Acceleration = acceleration;
                BrakeDeceleration = brake;
                MaxTurnSpeed = maxTurn;
                TurnAcceleration = turnAcceleration;
                TurnBrakeDeceleration = turnBrake;
            }
        }

        // AtalayCourseSceneSetup içindeki varyant değerleriyle aynı olmalı.
        private static readonly SettingSet Stable =
            new SettingSet("A - Sabit", 5f, 2.5f, 20f, 30f, 140f, 520f, 900f);

        private static readonly SettingSet Agile =
            new SettingSet("B - Cevik", 7.5f, 4f, 26f, 20f, 230f, 900f, 1200f);

        private GameObject ground;
        private GameObject robot;
        private RobotMotor motor;
        private float originalFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            originalFixedDeltaTime = Time.fixedDeltaTime;

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(400f, 1f, 400f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.fixedDeltaTime = originalFixedDeltaTime;

            if (robot != null)
            {
                Object.Destroy(robot);
            }

            if (ground != null)
            {
                Object.Destroy(ground);
            }

            motor = null;
        }

        [UnityTest]
        public IEnumerator AyarSetleriAyarlananUstHizaUlasir()
        {
            yield return TraceSpeed(Stable);
            yield return TraceSpeed(Agile);
        }

        /// <summary>
        /// Tam gazla hızlanırken hızı yarım saniyede bir yazar ve sonunda
        /// ulaşılan hızın ayarlanan üst hıza yakın olmasını bekler. Ayar
        /// karşılaştırmalarının anlamlı olması buna bağlı.
        /// </summary>
        private IEnumerator TraceSpeed(SettingSet settings)
        {
            SpawnRobot(settings);

            motor.SetMoveCommand(1f, 0f);
            for (int step = 0; step < 6; step++)
            {
                yield return Simulate(0.5f);
                TestContext.WriteLine($"Hiz izi {settings.Name}  {(step + 1) * 0.5f:0.0} s: {motor.ForwardSpeed:0.00} m/s");
            }

            float reached = motor.ForwardSpeed;
            Object.Destroy(robot);
            robot = null;
            yield return null;

            Assert.Greater(reached, settings.MaxForwardSpeed * 0.8f,
                $"{settings.Name}: ayarlanan ust hizin %80'ine ulasilamadi " +
                $"({reached:0.00} / {settings.MaxForwardSpeed:0.0} m/s).");
        }

        /// <summary>
        /// Ortak motor her fizik adımında gövdenin gerçek hızını geri okur, bu
        /// yüzden zemin sürtünmesi ayarlanan ivmeden düşülür ve kullanılabilir
        /// ivme için bir taban oluşur. Tabanın altındaki ayarlar robotu üst
        /// hızına çıkaramaz; ölçülen değer teslim notunda Emin'e bildirildi.
        /// </summary>
        [UnityTest]
        public IEnumerator DusukIvmeliAyarSurtunmeTabaninaTakilir()
        {
            SettingSet low = new SettingSet("Dusuk ivme", 5f, 2.5f, 12f, 30f, 140f, 520f, 900f);
            SpawnRobot(low);

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(1f);
            float firstSample = motor.ForwardSpeed;

            yield return Simulate(1f);
            float secondSample = motor.ForwardSpeed;

            float netAcceleration = secondSample - firstSample;
            float impliedFriction = low.Acceleration - netAcceleration;

            TestContext.WriteLine($"Ayarlanan ivme: {low.Acceleration:0.0} m/s^2");
            TestContext.WriteLine($"Olculen net ivme: {netAcceleration:0.00} m/s^2");
            TestContext.WriteLine($"Ortulen surtunme tabani: {impliedFriction:0.0} m/s^2");

            Assert.Less(secondSample, low.MaxForwardSpeed * 0.5f,
                "Dusuk ivmeli ayar beklenmedik sekilde ust hiza yaklasti; surtunme tabani olcumu guncellenmeli.");
            Assert.Greater(impliedFriction, 5f,
                "Surtunme tabani beklenenden dusuk; ayar setlerinin alt siniri yeniden olculmeli.");
        }

        [UnityTest]
        public IEnumerator FrenMesafesiAyarSetineGoreDegisir()
        {
            float stableDistance = 0f;
            float agileDistance = 0f;

            yield return MeasureStoppingDistance(Stable, value => stableDistance = value);
            yield return MeasureStoppingDistance(Agile, value => agileDistance = value);

            TestContext.WriteLine($"Fren mesafesi  {Stable.Name}: {stableDistance:0.00} m");
            TestContext.WriteLine($"Fren mesafesi  {Agile.Name}: {agileDistance:0.00} m");

            Assert.Greater(stableDistance, 0f);
            Assert.Less(stableDistance, agileDistance,
                "Sert frenleyen ayar setinin daha kisa mesafede durmasi bekleniyordu.");
        }

        [UnityTest]
        public IEnumerator DonusYaricapiAyarSetineGoreDegisir()
        {
            float stableDiameter = 0f;
            float agileDiameter = 0f;

            yield return MeasureTurnDiameter(Stable, value => stableDiameter = value);
            yield return MeasureTurnDiameter(Agile, value => agileDiameter = value);

            TestContext.WriteLine($"180 derece capi  {Stable.Name}: {stableDiameter:0.00} m");
            TestContext.WriteLine($"180 derece capi  {Agile.Name}: {agileDiameter:0.00} m");

            Assert.Greater(stableDiameter, 0f);
            Assert.Greater(agileDiameter, 0f);
        }

        [UnityTest]
        public IEnumerator TersKomutGecikmedenAlgilanir()
        {
            float stableLatency = 0f;
            float agileLatency = 0f;

            yield return MeasureReverseLatency(Stable, value => stableLatency = value);
            yield return MeasureReverseLatency(Agile, value => agileLatency = value);

            TestContext.WriteLine($"Yon degisimi  {Stable.Name}: {stableLatency:0.000} s");
            TestContext.WriteLine($"Yon degisimi  {Agile.Name}: {agileLatency:0.000} s");

            Assert.Less(stableLatency, 0.5f, "Ileriden geriye gecis yarim saniyeden uzun surdu.");
            Assert.Less(agileLatency, 0.8f, "Ileriden geriye gecis beklenenden uzun surdu.");
        }

        [UnityTest]
        public IEnumerator AyniSurusFarkliFizikAdimindaAyniSonucuVerir()
        {
            float[] steps = { 1f / 100f, 1f / 50f, 1f / 30f };
            float[] distances = new float[steps.Length];

            for (int index = 0; index < steps.Length; index++)
            {
                Time.fixedDeltaTime = steps[index];

                SpawnRobot(Stable);
                Vector3 start = robot.transform.position;

                motor.SetMoveCommand(1f, 0f);
                yield return Simulate(1.5f);

                distances[index] = Vector3.Distance(start, robot.transform.position);
                TestContext.WriteLine($"Fizik adimi {steps[index]:0.0000} s -> {distances[index]:0.000} m");

                Object.Destroy(robot);
                robot = null;
                yield return null;
            }

            float reference = distances[1];
            for (int index = 0; index < distances.Length; index++)
            {
                float deviation = Mathf.Abs(distances[index] - reference) / reference;
                Assert.Less(deviation, 0.08f,
                    $"Fizik adimi {steps[index]:0.0000} s'de mesafe %{deviation * 100f:0.0} saptı; " +
                    "surus kare hizina bagli.");
            }
        }

        private IEnumerator MeasureStoppingDistance(SettingSet settings, System.Action<float> report)
        {
            SpawnRobot(settings);

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2.5f);

            TestContext.WriteLine($"Fren oncesi hiz  {settings.Name}: {motor.ForwardSpeed:0.00} m/s " +
                                  $"(ayarlanan ust hiz {settings.MaxForwardSpeed:0.0})");

            Vector3 brakeStart = robot.transform.position;
            motor.Stop();

            float elapsed = 0f;
            while (Mathf.Abs(motor.ForwardSpeed) > 0.05f && elapsed < 5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            report(Vector3.Distance(brakeStart, robot.transform.position));

            Object.Destroy(robot);
            robot = null;
            yield return null;
        }

        private IEnumerator MeasureTurnDiameter(SettingSet settings, System.Action<float> report)
        {
            SpawnRobot(settings);

            motor.SetMoveCommand(1f, 1f);
            yield return Simulate(1f);

            Vector3 start = robot.transform.position;
            float previousYaw = robot.transform.eulerAngles.y;
            float turned = 0f;
            float elapsed = 0f;

            while (turned < 180f && elapsed < 8f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float yaw = robot.transform.eulerAngles.y;
                turned += Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
                previousYaw = yaw;
            }

            // 180 derece sonra alinan yer degistirme, donus dairesinin capidir.
            report(Vector3.Distance(start, robot.transform.position));

            Object.Destroy(robot);
            robot = null;
            yield return null;
        }

        private IEnumerator MeasureReverseLatency(SettingSet settings, System.Action<float> report)
        {
            SpawnRobot(settings);

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2.5f);

            motor.SetMoveCommand(-1f, 0f);

            float elapsed = 0f;
            while (motor.ForwardSpeed > 0f && elapsed < 3f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            report(elapsed);

            Object.Destroy(robot);
            robot = null;
            yield return null;
        }

        private void SpawnRobot(SettingSet settings)
        {
            robot = new GameObject($"Robot ({settings.Name})");
            robot.transform.position = new Vector3(0f, 0.05f, 0f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;
            body.centerOfMass = new Vector3(0f, 0.2f, 0f);

            motor = robot.AddComponent<RobotMotor>();
            ApplySettings(motor, settings);
        }

        /// <summary>
        /// Ayar setini motora uygular. Değerler Inspector alanlarında durduğu
        /// için test bunlara yansımayla erişir; alan adı değişirse test anlaşılır
        /// bir mesajla düşer.
        /// </summary>
        private static void ApplySettings(RobotMotor target, SettingSet settings)
        {
            SetPrivateFloat(target, "maxForwardSpeed", settings.MaxForwardSpeed);
            SetPrivateFloat(target, "maxReverseSpeed", settings.MaxReverseSpeed);
            SetPrivateFloat(target, "acceleration", settings.Acceleration);
            SetPrivateFloat(target, "brakeDeceleration", settings.BrakeDeceleration);
            SetPrivateFloat(target, "maxTurnSpeed", settings.MaxTurnSpeed);
            SetPrivateFloat(target, "turnAcceleration", settings.TurnAcceleration);
            SetPrivateFloat(target, "turnBrakeDeceleration", settings.TurnBrakeDeceleration);
        }

        private static void SetPrivateFloat(object target, string fieldName, float value)
        {
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                $"{target.GetType().Name}.{fieldName} alani bulunamadi; ayar seti testi guncellenmeli.");

            field.SetValue(target, value);
        }

        private static IEnumerator Simulate(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }
    }
}
