using System;
using UnityEngine;

namespace RobotSavaslari.Combat
{
    public enum WeaponStyle { Standard, Drum, Hammer, Flipper, Clamp, Saw, Spinner, Fork }

    public static class WorkshopWeapons
    {
        public const int Count = 7;
        private static readonly string[] Ids = { "spinner", "fork", "drum", "hammer", "flipper", "clamp", "saw" };
        private static readonly string[] Parts = { "Spinner", "Lifter", "Drum", "Hammer", "Flipper", "Clamp", "Saw" };
        private static readonly string[] Labels = { "Döner", "Kama", "Dikey Tambur", "Üstten Çekiç", "Fırlatıcı", "Kıskaç", "Testere" };
        private static readonly string[] Trades = {
            "Geniş yayda yüksek hasar. Rakibi ileri itmez.",
            "Önden güçlü itiş. Menzili kısadır.",
            "Devir aldıktan sonra dar bir alanda güçlü darbe ve kaldırış.",
            "Üst zırha ağır darbe. Kurulması ve ıskadan dönüşü yavaştır.",
            "Yakındaki rakibi tek kaldırışla yerinden eder. Hasarı düşük, dolumu uzundur.",
            "Hizalanmış rakibi kısa süre tutar. Az hasar verir.",
            "Temasta art arda küçük kesikler. Temas kopunca kesim durur."
        };

        public static int Index(string id) => Array.IndexOf(Ids, id);
        public static int PartIndex(string part) => Array.IndexOf(Parts, part);
        public static string Id(int index) => Ids[index];
        public static string Id(WeaponStyle style) => style switch {
            WeaponStyle.Spinner => "spinner", WeaponStyle.Fork => "fork",
            WeaponStyle.Drum => "drum", WeaponStyle.Hammer => "hammer", WeaponStyle.Flipper => "flipper",
            WeaponStyle.Clamp => "clamp", WeaponStyle.Saw => "saw", _ => null
        };
        public static string Part(string id) => Parts[Index(id)];
        public static string Label(string id) => Labels[Index(id)];
        public static string Trade(string id) => Trades[Index(id)];
        public static string Pair(string id)
        {
            int i = Index(id);
            if (i < 2) return i == 1 ? Ids[0] : Ids[1];
            return Ids[i + 1 < Count ? i + 1 : 2];
        }
    }
}
