using System.Collections.Generic;

namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Tek bir saldirinin hangi hedeflere hasar verdigini tutar.
    /// Ayni hedefin birden cok collider'i veya suren temas ikinci hasar yazamaz;
    /// her yeni saldiri kayitlari temizleyerek baslar.
    /// </summary>
    public sealed class AttackHitLog
    {
        private readonly HashSet<object> _targets = new HashSet<object>();

        public int Count => _targets.Count;

        /// <summary>Hedef bu saldirida ilk kez goruluyorsa true doner ve kaydeder.</summary>
        public bool TryRegister(object target)
        {
            return target != null && _targets.Add(target);
        }

        public bool Contains(object target)
        {
            return target != null && _targets.Contains(target);
        }

        public void Clear()
        {
            _targets.Clear();
        }
    }
}
