using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Sürüş tuş eşlemelerini saklar ve uygular. Yalnızca sürüşe aittir:
    /// kendi <c>Settings.Driving.*</c> anahtarlarını kullanır, ses/görüntü
    /// ayarlarının anahtarlarına dokunmaz ve genel bir ayar yöneticisi
    /// kurmaz.
    ///
    /// Yön tuşlarının ok takımı sabit yardımcıdır: eşleme değişse bile oklar
    /// kendi yönlerinde çalışmaya devam eder, bu yüzden bir ok tuşu birincil
    /// eşleme olarak atanamaz.
    /// </summary>
    public static class DriveBindings
    {
        public const Key DefaultForward = Key.W;
        public const Key DefaultBackward = Key.S;
        public const Key DefaultTurnLeft = Key.A;
        public const Key DefaultTurnRight = Key.D;
        public const Key DefaultBoost = Key.LeftShift;
        public const Key DefaultHandbrake = Key.LeftCtrl;

        private const string KeyPrefix = "Settings.Driving.";

        /// <summary>Eşlemelerden biri değiştiğinde haber verir.</summary>
        public static event Action Changed;

        private static readonly Key[] Bindings =
        {
            DefaultForward,
            DefaultBackward,
            DefaultTurnLeft,
            DefaultTurnRight,
            DefaultBoost,
            DefaultHandbrake,
        };

        /// <summary>
        /// Sürüşün sahiplenmediği tuşlar: menü, yeniden başlatma, silah ve
        /// yönlerin sabit ok yardımcıları.
        /// </summary>
        private static readonly Key[] ReservedKeys =
        {
            Key.Escape,
            Key.Enter,
            Key.NumpadEnter,
            Key.R,
            Key.Space,
            Key.UpArrow,
            Key.DownArrow,
            Key.LeftArrow,
            Key.RightArrow,
        };

        public static Key Forward => Bindings[(int)DriveAction.Forward];
        public static Key Backward => Bindings[(int)DriveAction.Backward];
        public static Key TurnLeft => Bindings[(int)DriveAction.TurnLeft];
        public static Key TurnRight => Bindings[(int)DriveAction.TurnRight];
        public static Key Boost => Bindings[(int)DriveAction.Boost];
        public static Key Handbrake => Bindings[(int)DriveAction.Handbrake];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => Load();

        /// <summary>Verilen eylemin tuşu.</summary>
        public static Key Get(DriveAction action) => Bindings[(int)action];

        /// <summary>Bu tuş sürüşe atanabilir mi?</summary>
        public static bool IsReserved(Key key) => Array.IndexOf(ReservedKeys, key) >= 0;

        /// <summary>
        /// Tuşu başka hangi eylem kullanıyor? Yoksa null döner.
        /// </summary>
        public static DriveAction? FindConflict(DriveAction action, Key key)
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                if (index != (int)action && Bindings[index] == key)
                {
                    return (DriveAction)index;
                }
            }

            return null;
        }

        /// <summary>
        /// Eşlemeyi değiştirmeyi dener. Çakışma ya da sahiplenilmeyen tuş
        /// durumunda değişiklik yapılmaz ve sebep bildirilir.
        /// </summary>
        public static bool TryAssign(DriveAction action, Key key, out string rejection)
        {
            if (key == Key.None)
            {
                rejection = "Gecersiz tus.";
                return false;
            }

            if (IsReserved(key))
            {
                rejection = $"{key} surus disinda kullaniliyor.";
                return false;
            }

            DriveAction? conflict = FindConflict(action, key);
            if (conflict.HasValue)
            {
                rejection = $"{key} zaten {Describe(conflict.Value)} icin kullaniliyor.";
                return false;
            }

            rejection = null;

            if (Bindings[(int)action] == key)
            {
                return true;
            }

            Bindings[(int)action] = key;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Eylemin okunabilir adı; çakışma mesajları bunu kullanır.</summary>
        public static string Describe(DriveAction action)
        {
            switch (action)
            {
                case DriveAction.Forward: return "ileri";
                case DriveAction.Backward: return "geri";
                case DriveAction.TurnLeft: return "sola donus";
                case DriveAction.TurnRight: return "saga donus";
                case DriveAction.Boost: return "boost";
                case DriveAction.Handbrake: return "el freni";
                default: return action.ToString();
            }
        }

        public static void Load()
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                DriveAction action = (DriveAction)index;
                string stored = PlayerPrefs.GetString(KeyPrefix + action, string.Empty);

                // Bozuk ya da artık geçersiz bir kayıt varsayılanı bozmasın.
                Bindings[index] = Enum.TryParse(stored, out Key parsed) && parsed != Key.None && !IsReserved(parsed)
                    ? parsed
                    : Default(action);
            }

            ResolveDuplicates();
            Changed?.Invoke();
        }

        public static void Save()
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                PlayerPrefs.SetString(KeyPrefix + (DriveAction)index, Bindings[index].ToString());
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Yalnızca sürüş eşlemelerini varsayılana döndürür; başka ayar
        /// anahtarına dokunmaz.
        /// </summary>
        public static void ResetToDefaults()
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                Bindings[index] = Default((DriveAction)index);
            }

            Changed?.Invoke();
        }

        /// <summary>Eşlemeleri sürücüye uygular.</summary>
        public static void Apply(PlayerKeyboardDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            driver.ApplyBindings(Forward, Backward, TurnLeft, TurnRight, Boost, Handbrake);
        }

        public static Key Default(DriveAction action)
        {
            switch (action)
            {
                case DriveAction.Forward: return DefaultForward;
                case DriveAction.Backward: return DefaultBackward;
                case DriveAction.TurnLeft: return DefaultTurnLeft;
                case DriveAction.TurnRight: return DefaultTurnRight;
                case DriveAction.Boost: return DefaultBoost;
                case DriveAction.Handbrake: return DefaultHandbrake;
                default: return Key.None;
            }
        }

        /// <summary>
        /// Elle düzenlenmiş bir kayıt aynı tuşu iki eyleme vermiş olabilir.
        /// Böyle bir kayıt kısmen kurtarılmaya çalışılmaz: tek tek düzeltmek
        /// yeni çakışmalar üretebileceği için tamamı varsayılana döner.
        /// </summary>
        private static void ResolveDuplicates()
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                for (int other = 0; other < index; other++)
                {
                    if (Bindings[index] != Bindings[other])
                    {
                        continue;
                    }

                    for (int reset = 0; reset < Bindings.Length; reset++)
                    {
                        Bindings[reset] = Default((DriveAction)reset);
                    }

                    return;
                }
            }
        }
    }
}
