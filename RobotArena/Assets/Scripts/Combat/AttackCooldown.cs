namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Saldiri bekleme suresi. Sure saldiri kullanildigi anda baslar.
    /// Saf C#: zaman disaridan verilir, boylece Unity zamani olmadan test edilebilir.
    /// </summary>
    public sealed class AttackCooldown
    {
        private float _readyAt = float.NegativeInfinity;

        public bool IsReady(float now)
        {
            return now >= _readyAt;
        }

        public float Remaining(float now)
        {
            return IsReady(now) ? 0f : _readyAt - now;
        }

        public void Begin(float now, float duration)
        {
            _readyAt = now + (duration > 0f ? duration : 0f);
        }

        public void Reset()
        {
            _readyAt = float.NegativeInfinity;
        }
    }
}
