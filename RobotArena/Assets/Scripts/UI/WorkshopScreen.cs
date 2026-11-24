using UnityEngine;

namespace RobotSavaslari.UI
{
    internal sealed class WorkshopScreen
    {
        private readonly WorkshopSelection selection;
        private readonly WorkshopKit kit;
        private readonly GameObject studio;
        private readonly Transform turntable;
        private readonly RenderTexture texture;
        private GameObject model;
        private float angle = -28f;
        private WorkshopLoadout draft;
        private WorkshopStats stats;
        private readonly WorkshopStats basis;
        internal WorkshopLoadout Draft => draft;

        internal WorkshopScreen(WorkshopSelection selection, WorkshopKit kit)
        {
            this.selection = selection;
            this.kit = kit;
            selection.TryGetCurrentStats(out basis);
            studio = new GameObject("Workshop preview studio");
            studio.transform.position = new Vector3(8192, 1024, 8192);
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(studio.transform, false);
            var camera = new GameObject("Workshop camera").AddComponent<Camera>();
            camera.transform.SetParent(studio.transform, false);
            camera.transform.localPosition = new Vector3(2.75f, 2.05f, 3.6f);
            camera.transform.LookAt(studio.transform.position + Vector3.up * .3f);
            camera.orthographic = true;
            camera.orthographicSize = 1.4f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 12f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .15f, .16f);
            texture = new RenderTexture(512, 448, 24) { name = "Workshop preview" };
            camera.targetTexture = texture;
            var lamp = new GameObject("Workshop softbox").AddComponent<Light>();
            lamp.transform.SetParent(studio.transform, false);
            lamp.transform.localPosition = new Vector3(-1.4f, 2.3f, 1.5f);
            lamp.type = LightType.Point;
            lamp.range = 6;
            lamp.intensity = 4;
            lamp.cullingMask = camera.cullingMask;
            SetDraft(selection.LastModular.IsValid ? selection.LastModular : WorkshopLoadout.Default);
        }

        internal bool SetDraft(WorkshopLoadout next)
        {
            if (!next.IsValid || !selection.TryGetStats(next, out var nextStats)) return false;
            var replacement = kit.Build(next, turntable);
            if (!replacement) return false;
            foreach (var item in replacement.GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = 31;
            if (model) { model.SetActive(false); Object.Destroy(model); }
            model = replacement;
            stats = nextStats;
            draft = next;
            Turn(0);
            return true;
        }

        internal void Dispose()
        {
            studio.SetActive(false);
            studio.GetComponentInChildren<Camera>(true).targetTexture = null;
            Object.Destroy(studio);
            texture.Release();
            Object.Destroy(texture);
        }

        private void Turn(float delta)
        {
            angle = Mathf.Repeat(angle + delta, 360f);
            turntable.localRotation = Quaternion.Euler(0, angle, 0);
        }

        internal void Draw(MainMenuScreen menu, float width, float height)
        {
            var theme = ArenaMenuStyle.Current;
            ArenaMenuStyle.DrawGarage(width, height);
            var board = new Rect((width - Mathf.Min(1192, width - 40)) / 2f, (height - 636) / 2f, Mathf.Min(1192, width - 40), 636);
            ArenaMenuStyle.Panel(board);
            ArenaMenuStyle.Fill(new Rect(board.x, board.y, board.width, 3), ArenaMenuStyle.Accent);
            float x = board.x + 24, y = board.y + 24;
            GUI.Label(new Rect(x, y, 320, 44), "Robot atölyesi", theme.Title);
            GUI.Label(new Rect(x, y + 46, 350, 26), "Standart şasi  /  Takım 1", theme.Hint);
            float top = y + 90, left = Mathf.Min(248, board.width * .24f);
            float right = Mathf.Min(282, board.width * .30f);
            var view = new Rect(x + left + 16, top, board.width - 80 - left - right, 350);
            OptionPair(x, top, left, "LASTİK", "Yumuşak", "Sert", "Daha kısa fren", "Daha yüksek son hız", draft.tireId == "soft", 0);
            OptionPair(x, top + 126, left, "ZIRH", "Hafif", "Ağır", "Daha hızlı ivme", "Daha fazla dayanım", draft.armorId == "light", 1);
            OptionPair(x, top + 252, left, "SİLAH", "Döner", "Kama", "Yüksek hasar", "Kısa menzilli itiş", draft.weaponId == "spinner", 2);
            ArenaMenuStyle.Fill(view, new Color(.12f, .15f, .16f));
            GUI.DrawTexture(view, texture, ScaleMode.ScaleToFit, false);
            var evt = Event.current;
            if (evt.type == EventType.MouseDrag && evt.button == 0 && view.Contains(evt.mousePosition))
            {
                Turn(-evt.delta.x * .8f);
                evt.Use();
            }
            if (GUI.Button(new Rect(view.x, view.yMax + 8, view.width * .5f - 4, 36), "‹  Sol", theme.Chip)) Turn(-30);
            if (GUI.Button(new Rect(view.center.x + 4, view.yMax + 8, view.width * .5f - 4, 36), "Sağ  ›", theme.Chip)) Turn(30);

            float sx = view.xMax + 16;
            GUI.Label(new Rect(sx, top, right, 26), "TOPLAM PROFİL", theme.Heading);
            GUI.Label(new Rect(sx, top + 26, right, 22), "Takılı robota göre fark", theme.Hint);
            if (model)
            {
                float sy = top + 54;
                Stat(sx, ref sy, right, "Kütle", stats.mass, basis.mass, " kg");
                Stat(sx, ref sy, right, "Dayanım", stats.health, basis.health, " HP");
                Stat(sx, ref sy, right, "Son hız", stats.topSpeed, basis.topSpeed, " m/s");
                Stat(sx, ref sy, right, "İvme", stats.acceleration, basis.acceleration, " m/s²");
                Stat(sx, ref sy, right, "Fren", stats.braking, basis.braking, " m/s²");
                Stat(sx, ref sy, right, "Dönüş", stats.turnSpeed, basis.turnSpeed, " °/s");
                Stat(sx, ref sy, right, "Hasar", stats.damage, basis.damage, " HP");
                Stat(sx, ref sy, right, "Erişim", stats.range, basis.range, " m");
                Stat(sx, ref sy, right, "Bekleme", stats.cooldown, basis.cooldown, " sn");
                Stat(sx, ref sy, right, "İtiş", stats.pushSpeed, basis.pushSpeed, " m/s");
            }
            if (!string.IsNullOrEmpty(selection.Notice))
                GUI.Label(new Rect(x, board.yMax - 125, board.width - 48, 44), selection.Notice, theme.Hint);
            ArenaMenuStyle.Fill(new Rect(x, board.yMax - 79, board.width - 48, 1), ArenaMenuStyle.Border);
            if (GUI.Button(new Rect(x, board.yMax - 62, 112, 42), "Geri", theme.Button)) { menu.CloseWorkshop(); return; }
            if (GUI.Button(new Rect(x + 120, board.yMax - 62, 180, 42), "Varsayılana dön", theme.Chip)) SetDraft(WorkshopLoadout.Default);
            if (GUI.Button(new Rect(board.xMax - 204, board.yMax - 62, 180, 42), "Uygula", theme.Primary)) menu.ApplyWorkshop();
        }

        private void OptionPair(float x, float y, float width, string title, string first, string second,
            string benefitA, string benefitB, bool firstActive, int slot)
        {
            var theme = ArenaMenuStyle.Current;
            GUI.Label(new Rect(x, y, width, 24), title, theme.Heading);
            float w = (width - 6) / 2;
            bool a = GUI.Button(new Rect(x, y + 28, w, 38), first, firstActive ? theme.Selected : theme.Chip);
            bool b = GUI.Button(new Rect(x + w + 6, y + 28, w, 38), second, !firstActive ? theme.Selected : theme.Chip);
            GUI.Label(new Rect(x, y + 66, w, 45), benefitA, theme.Hint);
            GUI.Label(new Rect(x + w + 6, y + 66, w, 45), benefitB, theme.Hint);
            if (!(a || b) || a == firstActive) return;
            var next = draft;
            if (slot == 0) next.tireId = a ? "soft" : "hard";
            else if (slot == 1) next.armorId = a ? "light" : "heavy";
            else next.weaponId = a ? "spinner" : "fork";
            SetDraft(next);
        }

        private static void Stat(float x, ref float y, float width, string label, float value, float basis, string unit)
        {
            var theme = ArenaMenuStyle.Current;
            float delta = value - basis;
            string difference = Mathf.Abs(delta) < .005f ? "" : $" ({delta:+0.##;-0.##})";
            GUI.Label(new Rect(x, y, width * .38f, 28), label, theme.Hint);
            GUI.Label(new Rect(x + width * .38f, y, width * .62f, 28), $"{value:0.##}{unit}{difference}", theme.Body);
            y += 31;
        }
    }
}
