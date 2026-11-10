using UnityEngine;

namespace RobotSavaslari
{
    [RequireComponent(typeof(RobotBotController))]
    public sealed class BotAttackProbe : MonoBehaviour
    {
        public int Count { get; private set; }
        public float LastRequestTime { get; private set; } = -1;
        private RobotBotController _bot;
        private void OnEnable() { _bot = GetComponent<RobotBotController>(); _bot.AttackRequest += Receive; }
        private void OnDisable() { if (_bot != null) _bot.AttackRequest -= Receive; }
        private void Receive(RobotBotController bot, Transform target) { Count++; LastRequestTime = Time.time; }
        public void ResetCount() { Count = 0; LastRequestTime = -1; }
    }
}
