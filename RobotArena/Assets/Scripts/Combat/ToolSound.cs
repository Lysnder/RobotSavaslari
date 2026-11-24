using UnityEngine;

namespace RobotSavaslari.Combat
{
    internal static class ToolSound
    {
        internal static AudioClip Create(WeaponStyle style)
        {
            const int rate = 16000;
            float duration = style == WeaponStyle.Saw ? .72f : style == WeaponStyle.Drum ? .44f : .30f;
            var samples = new float[(int)(rate * duration)];
            float phase = 0f;
            var noise = new System.Random((int)style);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                float hz, tone;
                switch (style)
                {
                    case WeaponStyle.Spinner: hz = 125 + 60 * Mathf.Sin(t * 14); break;
                    case WeaponStyle.Fork: hz = 470 * Mathf.Exp(-2.7f * t); break;
                    case WeaponStyle.Drum: hz = Mathf.Lerp(54, 196, t); break;
                    case WeaponStyle.Hammer: hz = 98 + 240 * Mathf.Exp(-14 * t); break;
                    case WeaponStyle.Flipper: hz = Mathf.Lerp(710, 74, Mathf.Sqrt(t)); break;
                    case WeaponStyle.Clamp: hz = 265 + 42 * Mathf.Sin(t * 34); break;
                    default: hz = 385 + 32 * Mathf.Sin(t * 19); break;
                }
                phase += Mathf.PI * 2 * hz / rate;
                tone = Mathf.Sin(phase) + .36f * Mathf.Sin(phase * 3);
                if (style == WeaponStyle.Saw || style == WeaponStyle.Flipper)
                    tone += (float)(noise.NextDouble() - .5) * .65f;
                samples[i] = .42f * tone * Mathf.Min(t * 24, 1f) * Mathf.Clamp01((1 - t) * 6f);
            }
            var clip = AudioClip.Create(style + " servo", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
