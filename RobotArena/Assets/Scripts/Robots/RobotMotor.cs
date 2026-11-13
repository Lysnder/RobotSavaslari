using UnityEngine;

namespace RobotSavaslari.Robots
{
    /// <summary>
    /// Robotun fizik tabanlı hareket bileşeni. Komutu dışarıdan alır; klavyeyi,
    /// yapay zekâyı ya da ağ katmanını tanımaz. Komut verildiği gibi kalır
    /// (latch), yani sürücü bileşen her karede komutu yenilemeli; komut
    /// bırakıldığında <see cref="Stop"/> ya da <see cref="RobotMoveCommand.Idle"/>
    /// gönderilir ve robot frenleyerek durur.
    /// </summary>
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Robot Savaşları/Robot Motor")]
    public class RobotMotor : MonoBehaviour
    {
        [Header("İleri / Geri (m/s)")]
        [SerializeField, Min(0f)] private float maxForwardSpeed = 6f;
        [SerializeField, Min(0f)] private float maxReverseSpeed = 3f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float brakeDeceleration = 26f;

        [Header("Dönüş (derece/s)")]
        [SerializeField, Min(0f)] private float maxTurnSpeed = 180f;
        [SerializeField, Min(0f)] private float turnAcceleration = 720f;
        [SerializeField, Min(0f)] private float turnBrakeDeceleration = 1080f;

        [Header("Geçici sürüş değişimi")]
        [Tooltip("Sürücünün isteyebileceği en büyük çarpan; hatalı bir değer robotu uçurmasın diye.")]
        [SerializeField, Min(1f)] private float driveModifierCeiling = 3f;

        private Rigidbody body;
        private float moveCommand;
        private float turnCommand;
        private float forwardSpeed;
        private float turnSpeed;
        private float mobility = 1f;
        private float turning = 1f;
        private float steeringPull;
        private float traction = 1f;
        private float speedModifier = 1f;
        private float accelerationModifier = 1f;
        private float turnModifier = 1f;

        public void SetDriveCondition(float mobilityScale, float turningScale, float pull, float grip)
        {
            mobility = Mathf.Clamp01(mobilityScale);
            turning = Mathf.Clamp01(turningScale);
            steeringPull = Mathf.Clamp(pull, -1f, 1f);
            traction = Mathf.Clamp01(grip);
        }

        /// <summary>
        /// Sürücünün istediği geçici performans değişimi: boost gibi, kısa süreli
        /// ve isteğe bağlı. Hasar koşulunun <see cref="SetDriveCondition"/>
        /// üstüne çarpan olarak biner; 1 etkisiz demektir.
        ///
        /// Hasar oranlarından ayrı tutulur, çünkü o kanal 0–1 aralığındadır ve
        /// her fizik adımında hasar köprüsü tarafından yeniden yazılır; oraya
        /// yazılan bir boost hem sınırlanır hem de bir sonraki adımda silinir.
        /// Değerler <see cref="driveModifierCeiling"/> ile sınırlanır.
        /// </summary>
        /// <param name="speedScale">İleri üst hız çarpanı. Geri hıza uygulanmaz.</param>
        /// <param name="accelerationScale">Hızlanma çarpanı; fren etkilenmez.</param>
        /// <param name="turnScale">Dönüş hızı çarpanı; el freni bunu yükseltir.</param>
        public void SetDriveModifier(float speedScale, float accelerationScale, float turnScale = 1f)
        {
            speedModifier = Mathf.Clamp(speedScale, 0f, driveModifierCeiling);
            accelerationModifier = Mathf.Clamp(accelerationScale, 0f, driveModifierCeiling);
            turnModifier = Mathf.Clamp(turnScale, 0f, driveModifierCeiling);
        }

        /// <summary>Geçici değişimi bırakır: normal sınırlar geri gelir.</summary>
        public void ClearDriveModifier() => SetDriveModifier(1f, 1f, 1f);

        /// <summary>Uygulanan ileri hız çarpanı (1 = değişim yok).</summary>
        public float SpeedModifier => speedModifier;

        /// <summary>Uygulanan hızlanma çarpanı (1 = değişim yok).</summary>
        public float AccelerationModifier => accelerationModifier;

        /// <summary>Uygulanan dönüş çarpanı (1 = değişim yok).</summary>
        public float TurnModifier => turnModifier;

        /// <summary>
        /// Hasar ve geçici değişim uygulandıktan sonra şu an geçerli olan dönüş
        /// hızı sınırı (derece/s). El freni bu sınırı yükseltir ama
        /// <see cref="driveModifierCeiling"/> üstüne çıkamaz.
        /// </summary>
        public float EffectiveMaxTurnSpeed => maxTurnSpeed * turning * turnModifier;

        /// <summary>
        /// Hasar ve geçici değişim uygulandıktan sonra şu an geçerli olan ileri
        /// üst hız. Gösterge ve testler bu sınırı okur.
        /// </summary>
        public float EffectiveMaxForwardSpeed => maxForwardSpeed * mobility * speedModifier;

        public void ResetMotion()
        {
            Stop();
            ClearDriveModifier();
            forwardSpeed = turnSpeed = 0f;
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Şu an uygulanan ileri/geri komutu (-1..1).</summary>
        public float MoveCommand => moveCommand;

        /// <summary>Şu an uygulanan dönüş komutu (-1..1).</summary>
        public float TurnCommand => turnCommand;

        /// <summary>Gövdenin ileri yöndeki gerçek hızı (m/s, geri giderken negatif).</summary>
        public float ForwardSpeed => forwardSpeed;

        /// <summary>Uygulanan dönüş hızı (derece/s, sağa dönerken pozitif).</summary>
        public float TurnSpeed => turnSpeed;

        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;
        public float MaxTurnSpeed => maxTurnSpeed;

        /// <summary>Fizik gövdesi; test ve tanı amaçlı dışarıya açıktır.</summary>
        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            // Robot yalnızca Y ekseninde dönsün: köşeye sürüldüğünde devrilip
            // duvarın üstünden çıkmasını engeller.
            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Hız doğrudan yazıldığı için ince duvarlardan geçmeyi önlemek adına
            // sürekli çarpışma algılama ve ara kare yumuşatma şart.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>
        /// Hareket komutu gönderir. Sürücü bileşenin tek ihtiyacı budur.
        /// </summary>
        /// <param name="forward">İleri/geri: 1 ileri, -1 geri, 0 gaz yok.</param>
        /// <param name="turn">Dönüş: 1 sağa, -1 sola, 0 dönüş yok.</param>
        public void SetMoveCommand(float forward, float turn)
        {
            moveCommand = Mathf.Clamp(forward, -1f, 1f);
            turnCommand = Mathf.Clamp(turn, -1f, 1f);
        }

        /// <summary>Hazır komut yapısıyla aynı işi yapar.</summary>
        public void SetMoveCommand(RobotMoveCommand command) => SetMoveCommand(command.Forward, command.Turn);

        /// <summary>
        /// Komutu bırakır: hareket girdisi kesilir, robot frenleyerek durur.
        /// </summary>
        public void Stop() => SetMoveCommand(0f, 0f);

        private void OnDisable()
        {
            // Bileşen kapatılırsa robot komutla birlikte hızını da bırakmalı.
            Stop();
            ClearDriveModifier();

            if (body != null && !body.isKinematic)
            {
                Vector3 velocity = body.linearVelocity;
                body.linearVelocity = new Vector3(0f, velocity.y, 0f);
                body.angularVelocity = Vector3.zero;
            }

            forwardSpeed = 0f;
            turnSpeed = 0f;
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            if (body.isKinematic) return;
            // A ram briefly releases traction so the physical recoil remains visible.
            if (traction < 1f)
            {
                Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                Vector3 slowed = Vector3.MoveTowards(planar, Vector3.zero, brakeDeceleration * traction * deltaTime);
                body.linearVelocity += slowed - planar;
                body.angularVelocity = Vector3.zero;
                turnSpeed = 0f;
                return;
            }

            Vector3 forwardAxis = transform.forward;
            forwardAxis.y = 0f;
            if (forwardAxis.sqrMagnitude < 1e-6f)
            {
                return;
            }
            forwardAxis.Normalize();

            // Hızı her adımda gerçek gövdeden oku: duvara dayanınca hız
            // kendiliğinden sıfırlanır, robot duvarı "ezmeye" devam etmez.
            forwardSpeed = Vector3.Dot(body.linearVelocity, forwardAxis);

            // Geçici çarpan yalnızca ileri sürüşe uygulanır: boost geri vitesi
            // tasarlanan sınırın üstüne çıkarmaz.
            float targetSpeed = moveCommand >= 0f
                ? moveCommand * maxForwardSpeed * mobility * speedModifier
                : moveCommand * maxReverseSpeed * mobility;

            bool braking = Mathf.Approximately(moveCommand, 0f) || targetSpeed * forwardSpeed < 0f;
            float speedChange = (braking
                ? brakeDeceleration
                : acceleration * mobility * accelerationModifier) * deltaTime;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, speedChange);

            // Yalnızca yatay hız sürülür; düşey bileşen yer çekimine bırakılır.
            Vector3 planarVelocity = forwardAxis * forwardSpeed;
            body.linearVelocity = new Vector3(planarVelocity.x, body.linearVelocity.y, planarVelocity.z);

            // Dönüş hızı ise bileşenin kendi durumunda tutulur: zemin sürtünmesi
            // her adımda dönüşü yiyip bitirmesin, robot komutu verildiği hızda
            // dönsün. Duvar yine gövdeyi fiziken durdurur.
            float targetTurnSpeed = Mathf.Clamp(turnCommand + moveCommand * steeringPull, -1f, 1f)
                                    * maxTurnSpeed * turning * turnModifier;
            bool turnBraking = Mathf.Approximately(turnCommand, 0f) || targetTurnSpeed * turnSpeed < 0f;
            float turnChange = (turnBraking ? turnBrakeDeceleration : turnAcceleration) * deltaTime;
            turnSpeed = Mathf.MoveTowards(turnSpeed, targetTurnSpeed, turnChange);

            body.angularVelocity = new Vector3(0f, turnSpeed * Mathf.Deg2Rad, 0f);
        }
    }
}
