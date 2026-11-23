using UnityEngine;

namespace RobotSavaslari.UI
{
    internal sealed class ArenaMenuStyle
    {
        private static ArenaMenuStyle instance;
        public static ArenaMenuStyle Current => instance ??= new ArenaMenuStyle();

        private readonly Texture2D garage = Resources.Load<Texture2D>("Menu/RobotGarage");

        public static readonly Color Canvas = new Color32(16, 18, 18, 224);
        public static readonly Color Surface = new Color32(26, 29, 29, 248);
        public static readonly Color Border = new Color32(74, 76, 70, 255);
        public static readonly Color Accent = new Color32(255, 190, 78, 255);
        private static readonly Color Ink = new Color32(20, 23, 28, 255);
        private static readonly Color Paper = new Color32(245, 247, 249, 255);
        private static readonly Color Muted = new Color32(188, 196, 190, 255);
        private static readonly Color Slate = new Color32(43, 48, 47, 255);

        public readonly GUIStyle Logo = Label(43, Paper, FontStyle.Bold, TextAnchor.UpperLeft);
        public readonly GUIStyle Title = Label(30, Paper, FontStyle.Bold);
        public readonly GUIStyle Body = Label(17, Paper);
        public readonly GUIStyle Hint = Label(14, Muted, FontStyle.Normal, TextAnchor.MiddleLeft, true);
        public readonly GUIStyle Heading = Label(14, Accent, FontStyle.Bold);
        public readonly GUIStyle Value = Label(17, Paper, FontStyle.Bold, TextAnchor.MiddleCenter);
        public readonly GUIStyle Button = Action(17, Slate, Paper);
        public readonly GUIStyle Chip = Action(15, Slate, Paper);
        public readonly GUIStyle Selected = Action(15, new Color32(102, 77, 39, 255), Paper, true);
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

        public static void DrawGarage(float width, float height, Color overlay = default)
        {
            if (Event.current.type != EventType.Repaint) return;
            var viewport = new Rect(0, 0, width, height);
            Texture2D art = Current.garage;
            Color previous = GUI.color;
            GUI.color = Color.white;
            if (art != null)
                GUI.DrawTexture(viewport, art, ScaleMode.ScaleAndCrop, false);
            else
                Fill(viewport, new Color32(18, 21, 20, 255));
            GUI.color = previous;
            if (overlay.a > 0f) Fill(viewport, overlay);
        }

        public static void HazardStripe(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.BeginGroup(rect);
            Matrix4x4 previous = GUI.matrix;
            try
            {
                for (int x = -4; x < rect.width; x += 16)
                {
                    GUI.matrix = previous;
                    GUIUtility.RotateAroundPivot(32f, new Vector2(x + 4f, rect.height * .5f));
                    Fill(new Rect(x, -5f, 7f, rect.height + 10f), Accent);
                }
            }
            finally
            {
                GUI.matrix = previous;
                GUI.EndGroup();
            }
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
