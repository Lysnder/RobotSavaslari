using System;
using UnityEngine;

namespace RobotSavaslari.Training
{
    /// <summary>Kaydın tek bir anı: konum ve yön.</summary>
    [Serializable]
    public struct GhostLapSample
    {
        public float time;
        public Vector3 position;
        public float yaw;
    }

    /// <summary>
    /// Tek bir turun kaydı. Küçük ve kendine yeten bir veri yapısıdır; dövüş
    /// tarafındaki replay sınıflarına bağlanmaz.
    ///
    /// <see cref="version"/> ve <see cref="courseId"/> taşır: kayıt biçimi
    /// değişirse ya da rota başkalaşırsa eski kayıt aynı turmuş gibi
    /// kullanılmaz.
    /// </summary>
    [Serializable]
    public class GhostLapRecording
    {
        /// <summary>Kayıt biçiminin sürümü.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string courseId = string.Empty;
        public float lapTime;
        public GhostLapSample[] samples = Array.Empty<GhostLapSample>();

        /// <summary>Kayıt bu parkurda, bu sürümle kullanılabilir mi?</summary>
        public bool IsUsableFor(string expectedCourseId)
        {
            if (version != CurrentVersion)
            {
                return false;
            }

            if (string.IsNullOrEmpty(courseId) || courseId != expectedCourseId)
            {
                return false;
            }

            if (lapTime <= 0f || samples == null || samples.Length < 2)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verilen anın konumunu ve yönünü örnekler arasından hesaplar.
        /// Kayıt bittiyse son örnekte kalır.
        /// </summary>
        public void Evaluate(float time, out Vector3 position, out float yaw)
        {
            if (samples == null || samples.Length == 0)
            {
                position = Vector3.zero;
                yaw = 0f;
                return;
            }

            if (time <= samples[0].time)
            {
                position = samples[0].position;
                yaw = samples[0].yaw;
                return;
            }

            GhostLapSample last = samples[samples.Length - 1];
            if (time >= last.time)
            {
                position = last.position;
                yaw = last.yaw;
                return;
            }

            for (int index = 1; index < samples.Length; index++)
            {
                GhostLapSample next = samples[index];
                if (next.time < time)
                {
                    continue;
                }

                GhostLapSample previous = samples[index - 1];
                float span = next.time - previous.time;
                float t = span > 0f ? Mathf.Clamp01((time - previous.time) / span) : 0f;

                position = Vector3.Lerp(previous.position, next.position, t);
                yaw = Mathf.LerpAngle(previous.yaw, next.yaw, t);
                return;
            }

            position = last.position;
            yaw = last.yaw;
        }
    }
}
