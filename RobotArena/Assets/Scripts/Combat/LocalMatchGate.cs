using System;
using UnityEngine;

namespace RobotSavaslari.Combat
{
    /// <summary>Dovusun islemesine izin veren mac durumu. Mac akisi bu arayuzu uygulayarak saldirilari acip kapatabilir.</summary>
    public interface IMatchGate
    {
        bool IsOpen { get; }
    }

    /// <summary>
    /// Sahneye yerel mac acik/kapali anahtari. Kapaliyken yeni saldiri kabul edilmez,
    /// suren saldiri da daha fazla hasar yazamaz.
    /// </summary>
    public sealed class LocalMatchGate : MonoBehaviour, IMatchGate
    {
        [SerializeField] private bool open = true;

        public bool IsOpen => open;

        public event Action<bool> Changed;

        public void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            Changed?.Invoke(open);
        }
    }
}
