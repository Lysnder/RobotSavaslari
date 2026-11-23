using UnityEngine;

namespace RobotSavaslari.UI
{
    internal sealed class ArenaMenuStyle
    {
        private static ArenaMenuStyle instance;
        public static ArenaMenuStyle Current => instance ??= new ArenaMenuStyle();

        public static readonly Color Canvas = new Color32(13, 19, 25, 224);
        public static readonly Color Surface = new Color32(21, 29, 38, 250);
        public static readonly Color Border = new Color32(54, 67, 81, 255);
        public static readonly Color Accent = new Color32(255, 183, 85, 255);
        private static readonly Color Ink = new Color32(20, 23, 28, 255);
        private static readonly Color Paper = new Color32(245, 247, 249, 255);
        private static readonly Color Muted = new Color32(181, 195, 209, 255);
        private static readonly Color Slate = new Color32(31, 43, 55, 255);

        public readonly GUIStyle Logo = Label(47, Paper, FontStyle.Bold, TextAnchor.UpperLeft);
        public readonly GUIStyle Title = Label(30, Paper, FontStyle.Bold);
        public readonly GUIStyle Body = Label(17, Paper);
        public readonly GUIStyle Hint = Label(14, Muted, FontStyle.Normal, TextAnchor.MiddleLeft, true);
        public readonly GUIStyle Heading = Label(14, Accent, FontStyle.Bold);
        public readonly GUIStyle Value = Label(17, Paper, FontStyle.Bold, TextAnchor.MiddleCenter);
        public readonly GUIStyle Button = Action(17, Slate, Paper);
        public readonly GUIStyle Chip = Action(15, Slate, Paper);
        public readonly GUIStyle Selected = Action(15, new Color32(108, 74, 44, 255), Paper, true);
        public readonly GUIStyle Primary = Action(20, Accent, Ink, true);

        public static float Scale(float width, float height) =>
            Mathf.Max(.01f, Mathf.Min(height / 720f, width / 840f));

        public static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prior = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prior;
        }

        public static void Panel(Rect rect)
        {
            Fill(rect, Border);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), Surface);
        }

        private static GUIStyle Label(int size, Color color, FontStyle weight = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool wrap = false)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = weight, alignment = alignment, wordWrap = wrap,
                padding = new RectOffset(), normal = { textColor = color }
            };
        }

        private static GUIStyle Action(int size, Color background, Color text, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(8, 8, 2, 2), border = new RectOffset()
            };
            Paint(style.normal, background, text);
            Paint(style.hover, Color.Lerp(background, Color.white, .15f), text);
            Paint(style.active, Color.Lerp(background, Color.black, .22f), text);
            Paint(style.focused, Color.Lerp(background, Accent, .32f), text);
            return style;
        }

        private static void Paint(GUIStyleState state, Color background, Color text)
        {
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point
            };
            pixel.SetPixel(0, 0, background);
            pixel.Apply(false, true);
            state.background = pixel;
            state.textColor = text;
        }
    }
}
