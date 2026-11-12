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

        public void Enter(MatchController match, CombatCameraController camera, Transform primary, Transform secondary)
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
}
