using System;
using System.Collections.Generic;
using System.IO;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Robots;
using UnityEngine;

namespace RobotSavaslari.UI
{
    public struct WorkshopStats
    {
        public float mass, health, topSpeed, acceleration, braking, turnSpeed, turnAcceleration;
        public float damage, range, cooldown, pushSpeed;
        internal bool InBounds => Within(mass, 8, 16) && Within(health, 75, 130) && Within(topSpeed, 4, 8) &&
            Within(acceleration, 12, 25) && Within(braking, 18, 35) && Within(turnSpeed, 130, 230) &&
            Within(turnAcceleration, 480, 1000) && Within(damage, 1, 50) && Within(range, .5f, 2) &&
            Within(cooldown, .5f, 2) && Within(pushSpeed, 0, 4);
        private static bool Within(float value, float minimum, float maximum) => value >= minimum && value <= maximum;

        internal void SetWeapon(CombatProfile profile)
        {
            damage = profile.damage;
            range = profile.range;
            cooldown = profile.cooldown;
            pushSpeed = profile.pushSpeed;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorkshopSelection : MonoBehaviour
    {
        private MatchController match;
        private PlayerRobotSelection presets;
        private RobotMotor motor;
        private RobotHealth health;
        private RobotDamageVisuals damage;
        private Rigidbody body;
        private BoxCollider hull;
        private Transform stockModel;
        private List<RobotDamageVisuals.Part> stockParts;
        private WorkshopStats baseline;
        private WorkshopModel modularModel;
        private WorkshopKit kit;
        private WorkshopStore store;
        private bool loaded;

        public WorkshopLoadout LastModular { get; private set; } = WorkshopLoadout.Default;
        public bool IsModular => modularModel != null;
        public string Notice { get; private set; }
        public bool CanSelect => presets != null && presets.CanSelect && !match.IsPaused && match.RoundNumber == 1;

        private void Awake()
        {
            match = GetComponent<MatchController>();
            presets = GetComponent<PlayerRobotSelection>();
        }

        private void Start()
        {
            if (!loaded && match.IsInitialized) RestoreSelection();
        }

        public bool UseStore(WorkshopStore source)
        {
            if (source == null || !CanSelect) return false;
            store = source;
            loaded = false;
            return RestoreSelection();
        }

        private bool RestoreSelection()
        {
            if (loaded) return true;
            if (!CanSelect) return false;
            if (store == null) store = new WorkshopStore(Path.Combine(Path.GetDirectoryName(match.HistoryStore.FilePath), WorkshopStore.FileName));
            var saved = store.Load();
            loaded = true;
            LastModular = saved.lastModular;
            bool restored = saved.selected == "modular" ? SetModular(LastModular, false) : SetStock(saved.selected == "fork", false);
            if (!restored)
            {
                string problem = Notice;
                SetStock(false, false);
                Notice = (problem ?? "Kayıtlı kurulum kullanılamıyor.") + " Hazır Döner seçildi.";
                return false;
            }
            Notice = store.Notice;
            return true;
        }

        private bool Capture()
        {
            if (stockParts != null) return true;
            if (match == null || !match.IsInitialized || match.Player == null || !presets.PrepareKit()) return false;
            var robot = match.Player;
            motor = robot.GetComponent<RobotMotor>();
            health = robot.GetComponent<RobotHealth>();
            body = robot.GetComponent<Rigidbody>();
            hull = robot.GetComponent<BoxCollider>();
            damage = robot.GetComponent<RobotDamageVisuals>();
            stockModel = robot.transform.Find("Robot Model");
            kit = WorkshopKit.Load();
            if (!motor || !health || !body || !hull || !damage || !stockModel || damage.Parts.Count == 0) return false;
            baseline = new WorkshopStats { mass = body.mass, health = health.MaxHealth, topSpeed = motor.MaxForwardSpeed,
                acceleration = motor.Acceleration, braking = motor.Braking, turnSpeed = motor.MaxTurnSpeed, turnAcceleration = motor.TurnAcceleration };
            baseline.SetWeapon(presets.ProfileFor(false));
            stockParts = new List<RobotDamageVisuals.Part>(damage.Parts);
            return true;
        }

        public bool TryGetCurrentStats(out WorkshopStats stats)
        {
            stats = default;
            if (!Capture()) return false;
            if (IsModular) return TryGetStats(LastModular, out stats);
            stats = baseline;
            stats.SetWeapon(presets.ProfileFor(presets.ForkSelected));
            return true;
        }

        public bool TryGetStats(WorkshopLoadout loadout, out WorkshopStats stats)
        {
            stats = default;
            if (!loadout.IsValid || !Capture() || !kit || !kit.HasParts) return false;
            bool heavy = loadout.armorId == "heavy";
            bool soft = loadout.tireId == "soft";
            bool fork = loadout.weaponId == "fork";
            var weapon = presets.ProfileFor(fork);
            if (!weapon || !(weapon.arcDegrees >= 1 && weapon.arcDegrees <= 360) ||
                !(weapon.activeDuration >= 0 && weapon.activeDuration <= weapon.cooldown)) return false;
            stats = baseline;
            stats.mass *= heavy ? 1.12f : .92f;
            stats.health = Mathf.Round(stats.health * (heavy ? 1.10f : .92f));
            stats.topSpeed *= soft ? .94f : 1.06f;
            stats.braking *= soft ? 1.12f : .90f;
            stats.acceleration *= heavy ? .90f : 1.12f;
            float steering = (soft ? 1.06f : .94f) * (heavy ? .94f : 1.06f);
            stats.turnSpeed *= steering;
            stats.turnAcceleration *= steering;
            stats.SetWeapon(weapon);
            return stats.InBounds;
        }

        public bool TryApply(WorkshopLoadout loadout)
        {
            if (!CanSelect || (!loaded && !RestoreSelection())) return false;
            return SetModular(loadout, true);
        }

        public bool SelectStock(bool useFork)
        {
            if (!CanSelect || (!loaded && !RestoreSelection())) return false;
            return SetStock(useFork, true);
        }

        private bool SetModular(WorkshopLoadout loadout, bool persist)
        {
            if (!TryGetStats(loadout, out var stats) || !presets.CanFitWeapon(loadout.weaponId == "fork") ||
                !hull.enabled || hull.isTrigger || hull.transform != body.transform)
            {
                Notice = "Kurulum geçersiz. Yuva, silah profili ve şasi zarfını kontrol edin.";
                return false;
            }
            WorkshopModel next;
            try { next = WorkshopModel.Build(kit, loadout, body.transform, hull, stockParts); }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is UnityException)
            {
                Notice = "Atölye robotu kurulamadı. " + e.Message;
                return false;
            }
            if (persist && !Save("modular", loadout))
            {
                next.Dispose();
                return false;
            }

            if (modularModel == null)
            {
                stockModel.gameObject.SetActive(false);
                stockModel.SetParent(transform, false);
            }
            else modularModel.Dispose();
            modularModel = next;
            LastModular = loadout;
            match.Player.name = presets.StockName(false) + " - " + loadout.tireId + "-" + loadout.armorId + "-" + loadout.weaponId;
            damage.InstallParts(next.Parts);
            next.Root.SetActive(true);
            match.Player.SelectWeaponProfile(presets.ProfileFor(loadout.weaponId == "fork"));
            ApplyStats(stats);
            Notice = null;
            return true;
        }

        private bool SetStock(bool fork, bool persist)
        {
            if (!presets.CanFitWeapon(fork)) { Notice = "Kama bu arenada kullanılamıyor."; return false; }
            // A damaged or newer save stays untouched. An explicit ready robot can still play this session.
            if (persist && !store.IsProtected && !Save(fork ? "fork" : "spinner", LastModular)) return false;
            if (IsModular)
            {
                modularModel.Dispose();
                modularModel = null;
                stockModel.SetParent(match.Player.transform, false);
                stockModel.gameObject.SetActive(true);
                damage.InstallParts(stockParts);
                match.Player.name = presets.StockName(presets.ForkSelected);
                match.Player.SelectWeaponProfile(presets.ProfileFor(presets.ForkSelected));
                ApplyStats(baseline);
            }
            if (!presets.FitStockWeapon(fork)) return false;
            Notice = store.Notice;
            return true;
        }

        private void ApplyStats(WorkshopStats stats)
        {
            body.mass = stats.mass;
            match.Player.WeaponPushMultiplier = baseline.mass / stats.mass;
            motor.ConfigureChassis(stats.topSpeed, stats.acceleration, stats.braking, stats.turnSpeed, stats.turnAcceleration);
            health.ConfigureCapacity(stats.health);
            match.Player.GetComponent<RobotModelAnimator>().BindParts();
        }

        private bool Save(string selected, WorkshopLoadout last)
        {
            if (store.Save(new WorkshopSave { version = 1, selected = selected, lastModular = last })) return true;
            Notice = store.Notice ?? "Atölye seçimi kaydedilemedi.";
            return false;
        }

        private void OnDestroy()
        {
            if (modularModel != null) modularModel.Dispose(false);
        }
    }
}
