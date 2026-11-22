using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Temporary result-screen takeover shared by replay and inspection: hides the menu/HUD, hides the
    /// live robots and their debris, and points the camera at presentation proxies. <see cref="Exit"/>
    /// restores every value it changed to exactly what it was. The match controller itself is never
    /// disabled or called.
    /// </summary>
    public sealed class MatchPresentationScope
    {
        private readonly List<KeyValuePair<Renderer, bool>> _hiddenRenderers = new List<KeyValuePair<Renderer, bool>>();
        private readonly List<Renderer> _scratch = new List<Renderer>();
        private MainMenuScreen _menu;
        private bool _menuWasEnabled;
        private CombatCameraController _camera;

        public bool IsActive { get; private set; }

        /// <param name="alsoHide">More live renderers to hide for the presentation (the archive hides the live arena layout).</param>
        public void Enter(MatchController match, CombatCameraController camera, Transform primary, Transform secondary,
            IEnumerable<Renderer> alsoHide = null)
        {
            if (IsActive) Exit();
            _menu = match != null ? match.GetComponent<MainMenuScreen>() : null;
            if (_menu != null)
            {
                _menuWasEnabled = _menu.enabled;
                _menu.enabled = false;
            }

            _scratch.Clear();
            foreach (RobotPrototypeController robot in new[] { match != null ? match.Player : null, match != null ? match.Opponent : null })
            {
                if (robot == null) continue;
                _scratch.AddRange(robot.GetComponentsInChildren<Renderer>(true));
                RobotVisualProxyFactory.CollectDebrisRenderers(robot.transform, _scratch);
            }
            if (alsoHide != null) _scratch.AddRange(alsoHide);
            RobotVisualProxyFactory.Hide(_scratch, _hiddenRenderers);
            _scratch.Clear();

            _camera = camera;
            if (_camera != null) _camera.SetPresentationTargets(primary, secondary);
            IsActive = true;
        }

        public void Exit()
        {
            if (!IsActive) return;
            RobotVisualProxyFactory.Restore(_hiddenRenderers);
            if (_menu != null) _menu.enabled = _menuWasEnabled;
            if (_camera != null) _camera.ClearPresentationTargets();
            _menu = null;
            _camera = null;
            IsActive = false;
        }
    }

    /// <summary>
    /// Turns behaviours off for as long as a mode owns the screen and gives each its exact previous
    /// enabled state back on <see cref="Restore"/>, in reverse order. Destroyed behaviours are skipped.
    /// </summary>
    public sealed class BehaviourEnableScope
    {
        private readonly List<KeyValuePair<Behaviour, bool>> _previous = new List<KeyValuePair<Behaviour, bool>>();

        public bool IsActive => _previous.Count > 0;
        public int Count => _previous.Count;

        /// <summary>Disables a behaviour once; a behaviour already held keeps its first remembered state.</summary>
        public void Disable(Behaviour behaviour)
        {
            if (behaviour == null) return;
            foreach (KeyValuePair<Behaviour, bool> item in _previous)
                if (item.Key == behaviour) return;
            _previous.Add(new KeyValuePair<Behaviour, bool>(behaviour, behaviour.enabled));
            behaviour.enabled = false;
        }

        public bool WasEnabled(Behaviour behaviour)
        {
            foreach (KeyValuePair<Behaviour, bool> item in _previous)
                if (item.Key == behaviour) return item.Value;
            return behaviour != null && behaviour.enabled;
        }

        public void Restore()
        {
            for (int i = _previous.Count - 1; i >= 0; i--)
                if (_previous[i].Key != null) _previous[i].Key.enabled = _previous[i].Value;
            _previous.Clear();
        }
    }
}
