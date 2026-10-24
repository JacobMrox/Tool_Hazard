using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Tool_Hazard.UI
{
    internal static class FluentTheme
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwaSystemBackdropType = 38;
        private const int DwmWindowCornerRound = 2;
        private const int DwmSystemBackdropMainWindow = 2;

        private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
        private static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
        private static readonly Color DarkBorder = Color.FromArgb(70, 70, 74);
        private static readonly Color DarkText = Color.FromArgb(243, 243, 243);
        private static readonly Color LightBackground = Color.FromArgb(243, 243, 243);
        private static readonly Color LightSurface = Color.White;
        private static readonly Color LightText = Color.FromArgb(32, 32, 32);

        private static readonly HashSet<Form> ThemedForms = new();

        internal static bool IsDarkMode { get; private set; }

        internal static void Initialize()
        {
            IsDarkMode = AppsUseDarkMode();
            Application.Idle += (_, _) => ApplyToOpenForms();
        }

        internal static void Apply(Form form)
        {
            if (form.IsDisposed || ThemedForms.Contains(form))
                return;

            ThemedForms.Add(form);
            form.HandleCreated += (_, _) => ApplyWindowChrome(form);
            form.FormClosed += (_, _) => ThemedForms.Remove(form);
            ApplyWindowChrome(form);
            ApplyControlTree(form);
        }

        private static void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms)
                Apply(form);
        }

        private static bool AppsUseDarkMode()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyWindowChrome(Form form)
        {
            if (!OperatingSystem.IsWindows() || !form.IsHandleCreated)
                return;

            int dark = IsDarkMode ? 1 : 0;
            int corner = DwmWindowCornerRound;
            int backdrop = DwmSystemBackdropMainWindow;
            _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(form.Handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));
            _ = DwmSetWindowAttribute(form.Handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }

        private static void ApplyControlTree(Control control)
        {
            Color background = IsDarkMode ? DarkBackground : LightBackground;
            Color surface = IsDarkMode ? DarkSurface : LightSurface;
            Color text = IsDarkMode ? DarkText : LightText;

            if (control is not PictureBox)
            {
                control.ForeColor = text;
                if (control.BackgroundImage is null && control.BackColor != Color.Transparent)
                    control.BackColor = control is TextBoxBase or ListBox or ListView or TreeView or DataGridView
                        ? surface
                        : background;
            }

            switch (control)
            {
                case Form form:
                    form.Font = new Font("Segoe UI", form.Font.Size, form.Font.Style);
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = IsDarkMode ? DarkBorder : Color.FromArgb(200, 200, 200);
                    button.FlatAppearance.MouseOverBackColor = IsDarkMode
                        ? Color.FromArgb(62, 62, 66)
                        : Color.FromArgb(232, 232, 232);
                    break;
                case MenuStrip menu:
                    menu.Renderer = new FluentToolStripRenderer(IsDarkMode);
                    menu.BackColor = surface;
                    menu.ForeColor = text;
                    break;
                case StatusStrip status:
                    status.Renderer = new FluentToolStripRenderer(IsDarkMode);
                    status.BackColor = surface;
                    status.ForeColor = text;
                    break;
                case ToolStrip strip:
                    strip.Renderer = new FluentToolStripRenderer(IsDarkMode);
                    strip.BackColor = surface;
                    strip.ForeColor = text;
                    break;
                case DataGridView grid:
                    grid.EnableHeadersVisualStyles = false;
                    grid.BackgroundColor = background;
                    grid.GridColor = IsDarkMode ? DarkBorder : Color.Gainsboro;
                    grid.DefaultCellStyle.BackColor = surface;
                    grid.DefaultCellStyle.ForeColor = text;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 212);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = background;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
                    break;
            }

            if (control.IsHandleCreated && control is TextBoxBase or ListBox or ListView or TreeView)
                SetWindowTheme(control.Handle, IsDarkMode ? "DarkMode_Explorer" : "Explorer", null);

            foreach (Control child in control.Controls)
                ApplyControlTree(child);
        }

        private sealed class FluentToolStripRenderer : ToolStripProfessionalRenderer
        {
            internal FluentToolStripRenderer(bool dark)
                : base(new FluentColorTable(dark))
            {
                RoundedEdges = true;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = IsDarkMode ? DarkText : LightText;
                base.OnRenderItemText(e);
            }
        }

        private sealed class FluentColorTable : ProfessionalColorTable
        {
            private readonly bool _dark;
            internal FluentColorTable(bool dark) => _dark = dark;
            private Color Surface => _dark ? DarkSurface : LightSurface;
            private Color Hover => _dark ? Color.FromArgb(62, 62, 66) : Color.FromArgb(232, 232, 232);
            private Color Border => _dark ? DarkBorder : Color.FromArgb(200, 200, 200);

            public override Color ToolStripDropDownBackground => Surface;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Border;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemPressedGradientBegin => Hover;
            public override Color MenuItemPressedGradientEnd => Hover;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color ToolStripBorder => Border;
            public override Color ToolStripGradientBegin => Surface;
            public override Color ToolStripGradientMiddle => Surface;
            public override Color ToolStripGradientEnd => Surface;
            public override Color StatusStripGradientBegin => Surface;
            public override Color StatusStripGradientEnd => Surface;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
    }
}
