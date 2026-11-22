using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Sends layout and mouse events through a native GUIView to the component's real OnGUI (the archive panel's
    /// copy of Tests/Gameplay/PlayMode/ImmediateGui, which lives in another test assembly).
    /// Requires Editor graphics: batch mode is supported without -nographics.
    /// Input points use game pixels, independent of Windows display DPI.
    /// </summary>
    internal static class ArchiveGui
    {
        public static void Draw(MonoBehaviour target)
        {
            using (var window = new GuiWindowScope(target)) window.Send(new Event { type = EventType.Repaint });
        }

        public static void Click(MonoBehaviour target, Vector2 point)
        {
            // One native state must own the complete gesture, including its hot control.
            using (var window = new GuiWindowScope(target))
            {
                window.Send(new Event { type = EventType.Repaint });
                window.Send(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
                window.Send(new Event { type = EventType.MouseUp, mousePosition = point, button = 0, clickCount = 1 });
            }
        }

        public static void ClickScaled(MonoBehaviour target, Rect rect) => Click(target, rect.center * ArenaScale);
        public static float ArenaScale => Mathf.Max(0.01f, Mathf.Min(Screen.height / 720f, Screen.width / 600f));
        public static float ScaledWidth => Screen.width / ArenaScale;
        public static float ScaledHeight => Screen.height / ArenaScale;

        private sealed class GuiWindowScope : IDisposable
        {
#if UNITY_EDITOR
            private readonly TestGuiWindow window;
            public GuiWindowScope(MonoBehaviour target)
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                    "IMGUI interaction tests need a native GUIView; run Unity without -nographics.");
                window = ScriptableObject.CreateInstance<TestGuiWindow>();
                window.Target = target;
                window.Dpi = EditorGUIUtility.pixelsPerPoint;
                window.position = new Rect(0, 0, Screen.width / window.Dpi, Screen.height / window.Dpi);
                window.ShowPopup();
            }
            public void Send(Event evt)
            {
                evt.mousePosition /= window.Dpi;
                window.SendEvent(new Event { type = EventType.Layout, mousePosition = evt.mousePosition });
                window.SendEvent(evt);
            }
            public void Dispose() => window.Close();
#else
            public GuiWindowScope(MonoBehaviour target) => throw new PlatformNotSupportedException("IMGUI interaction tests run in the Editor.");
            public void Send(Event evt) => throw new PlatformNotSupportedException();
            public void Dispose() { }
#endif
        }

#if UNITY_EDITOR
        private sealed class TestGuiWindow : EditorWindow
        {
            private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Static;
            private static readonly PropertyInfo PixelsPerPoint = typeof(GUIUtility).GetProperty("pixelsPerPoint", Internal);
            private static readonly Type ClipType = typeof(GUIUtility).Assembly.GetType("UnityEngine.GUIClip");
            private static readonly MethodInfo PushClip = ClipType.GetMethod("Internal_PushParentClip", Internal, null,
                new[] { typeof(Matrix4x4), typeof(Rect) }, null);
            private static readonly MethodInfo PopClip = ClipType.GetMethod("Internal_PopParentClip", Internal);
            public MonoBehaviour Target;
            public float Dpi;

            private void OnGUI()
            {
                if (Target == null) return;
                float previousDpi = (float)PixelsPerPoint.GetValue(null);
                Matrix4x4 previousMatrix = GUI.matrix;
                PixelsPerPoint.SetValue(null, 1f);
                GUI.matrix = Matrix4x4.identity;
                // Game OnGUI uses pixels; Editor GUIView input and clipping use display points.
                PushClip.Invoke(null, new object[] { Matrix4x4.Scale(new Vector3(1f / Dpi, 1f / Dpi, 1)),
                    new Rect(0, 0, position.width, position.height) });
                try
                {
                    MethodInfo onGui = Target.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Assert.That(onGui, Is.Not.Null, Target.GetType().Name + " has OnGUI");
                    onGui.Invoke(Target, null);
                }
                catch (TargetInvocationException error) when (error.InnerException is ExitGUIException) { }
                catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
                finally
                {
                    PopClip.Invoke(null, null);
                    GUI.matrix = previousMatrix;
                    PixelsPerPoint.SetValue(null, previousDpi);
                }
            }
        }
#endif
    }
}
