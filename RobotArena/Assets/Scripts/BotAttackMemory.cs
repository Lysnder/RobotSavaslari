using UnityEngine;

namespace RobotSavaslari
{
    // Six distinct charge onsets, twelve seconds of simulated (unpaused) time.
    // A charge ends only after 0.3 seconds without an incoming observation.
    public sealed class BotAttackMemory
    {
        public const int Capacity = 6;
        public const float Lifetime = 12f;
        private readonly int[] _sectors = new int[Capacity];
        private readonly float[] _times = new float[Capacity];
        private int _head;
        private float _clock, _quietTime;
        private bool _inCharge;
        public int Count { get; private set; }
        public int TotalCharges { get; private set; }
        public float Repetition { get; private set; }
        public bool AdaptDefense => Count >= 3 && Repetition >= 2f / 3f;

        public void Observe(bool incoming, float localAngle, float dt)
        {
            if (dt <= 0) return;
            _clock += dt;
            while (Count > 0 && _clock - _times[(_head - Count + Capacity) % Capacity] > Lifetime) Count--;
            if (!incoming)
            {
                _quietTime += dt;
                if (_quietTime >= 0.3f) _inCharge = false;
            }
            else
            {
                _quietTime = 0;
                if (!_inCharge)
                {
                    _sectors[_head] = Sector(localAngle);
                    _times[_head] = _clock;
                    _head = (_head + 1) % Capacity;
                    Count = Mathf.Min(Capacity, Count + 1);
                    TotalCharges++;
                    _inCharge = true;
                }
            }
            int matching = 0;
            for (int i = 0; i < Count; i++)
                if (_sectors[(_head - 1 - i + Capacity) % Capacity] == Sector(localAngle)) matching++;
            Repetition = Count > 0 ? (float)matching / Count : 0;
        }

        private static int Sector(float angle) => Mathf.RoundToInt(Mathf.DeltaAngle(0, angle) / 60f);
        public void Clear()
        {
            _head = Count = TotalCharges = 0;
            _clock = _quietTime = Repetition = 0;
            _inCharge = false;
        }
    }
}
