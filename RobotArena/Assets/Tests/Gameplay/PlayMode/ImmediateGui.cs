using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// Runs a component's OnGUI from a test. Batch-mode test runs have no Game view, so Unity never
    /// sends GUI events; this opens the same IMGUI container scope an IMGUIContainer uses and sends
    /// a layout pass followed by the requested event. Clicks are a mouse down and up at a point in
    /// unscaled screen GUI coordinates (origin top left).
    /// </summary>
    internal static class ImmediateGui
    {
        private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly Type StateType = typeof(GUIUtility).Assembly.GetType("UnityEngine.ObjectGUIState");
        private static readonly MethodInfo BeginContainer = StateType == null ? null
            : typeof(GUIUtility).GetMethod("BeginContainer", Internal, null, new[] { StateType }, null);
        private static readonly MethodInfo EndContainer = typeof(GUIUtility).GetMethod("EndContainer", Internal, null, Type.EmptyTypes, null);
        private static readonly MethodInfo BeginGui = typeof(GUIUtility).GetMethod("BeginGUI", Internal);
        private static readonly MethodInfo EndGui = typeof(GUIUtility).GetMethod("EndGUI", Internal);

        public static void Draw(MonoBehaviour target)
        {
            Send(target, new Event { type = EventType.Repaint });
        }

        public static void Click(MonoBehaviour target, Vector2 point)
        {
            Draw(target);
            Send(target, new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
            Send(target, new Event { type = EventType.MouseUp, mousePosition = point, button = 0, clickCount = 1 });
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
        }

        /// <summary>Clicks a rect given in the component's reference layout, scaled the way the arena UI scales.</summary>
        public static void ClickScaled(MonoBehaviour target, Rect rect)
        {
            float scale = ArenaScale;
            Click(target, rect.center * scale);
        }

        /// <summary>The scale MainMenuScreen, settings and presentation screens apply to their 720p layout.</summary>
        public static float ArenaScale => Mathf.Max(0.01f, Mathf.Min(Screen.height / 720f, Screen.width / 600f));
        public static float ScaledWidth => Screen.width / ArenaScale;
        public static float ScaledHeight => Screen.height / ArenaScale;

        private static void Send(MonoBehaviour target, Event evt)
        {
            Assert.That(StateType != null && BeginContainer != null && EndContainer != null && BeginGui != null && EndGui != null,
                Is.True, "IMGUI internals available in this Unity version");
            MethodInfo onGui = target.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(onGui, Is.Not.Null, target.GetType().Name + " has OnGUI");
            Run(target, onGui, new Event { type = EventType.Layout, mousePosition = evt.mousePosition });
            Run(target, onGui, evt);
        }

        /// <summary>The owner id BeginGUI expects: an int in older Unity versions, an EntityId in newer ones.</summary>
        private static object OwnerId(Object target)
        {
            Type idType = BeginGui.GetParameters()[1].ParameterType;
            if (idType == typeof(int)) return target.GetInstanceID();
            MethodInfo getEntityId = typeof(Object).GetMethod("GetEntityId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (getEntityId != null && getEntityId.ReturnType == idType) return getEntityId.Invoke(target, null);
            return Activator.CreateInstance(idType);
        }

        private static void Run(MonoBehaviour target, MethodInfo onGui, Event evt)
        {
            object state = Activator.CreateInstance(StateType, true);
            Event.current = evt;
            BeginContainer.Invoke(null, new[] { state });
            try
            {
                BeginGui.Invoke(null, new object[] { 0, OwnerId(target), 1 });
                try { onGui.Invoke(target, null); }
                catch (TargetInvocationException error) when (error.InnerException is ExitGUIException) { }
                catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
                finally { EndGui.Invoke(null, new object[] { 1 }); }
            }
            finally
            {
                EndContainer.Invoke(null, null);
                (state as IDisposable)?.Dispose();
            }
        }
    }
}
