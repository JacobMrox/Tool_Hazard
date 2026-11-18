using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Tool_Hazard.Properties;

namespace Tool_Hazard.Localization
{
    /// <summary>Describes a language that was discovered from an embedded translation file.</summary>
    internal sealed record LanguageInfo(string Code, string DisplayName);

    /// <summary>
    /// Central localization service for the whole application.
    ///
    /// Translations live in embedded UTF-8 JSON files named <c>Localization/&lt;culture&gt;.json</c>.
    /// Each file maps an English source string to its translated string, for example:
    /// <code>{ "File": "Datei", "Exit": "Beenden" }</code>
    ///
    /// Future proofing:
    ///  - Adding a language only requires dropping a new JSON file into Localization/
    ///    (files are discovered automatically through the project wildcard - no code changes).
    ///  - Adding/improving a translation only requires adding a key to a JSON file
    ///    (the key is the English text shown by the designer).
    ///  - Missing keys gracefully fall back to English.
    ///  - The selected language is persisted and applied to every open form immediately.
    /// </summary>
    internal static class LocalizationManager
    {
        private const string DefaultLanguage = "en";
        private const string ResourcePrefix = "Tool_Hazard.Localization.";
        private const string ResourceSuffix = ".json";

        private static readonly Dictionary<string, Dictionary<string, string>> _translations =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<LanguageInfo> _languages = new();

        // The original (English) text of every localized control/item, so switching
        // languages never compounds translations and untranslated keys fall back cleanly.
        private static readonly ConditionalWeakTable<object, OriginalText> _originals = new();

        // Form roots we have already localized. Reapplying only touches these + any new forms.
        private static readonly HashSet<object> _trackedRoots = new(ReferenceEqualityComparer.Instance);

        private static bool _initialized;
        private static bool _idleHooked;
        private static string _current = DefaultLanguage;

        /// <summary>Raised after the active language (and all open forms) have changed.</summary>
        public static event Action? LanguageChanged;

        /// <summary>Languages available for selection, English first.</summary>
        public static IReadOnlyList<LanguageInfo> AvailableLanguages => _languages;

        /// <summary>The currently active culture code (for example "de" or "ar").</summary>
        public static string CurrentLanguage => _current;

        private sealed class OriginalText
        {
            public OriginalText(string text) => Text = text;
            public string Text { get; }
        }

        /// <summary>
        /// Loads translation files and selects the persisted language. Safe to call more than once.
        /// Call once during application startup, before any forms are created.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;

            LoadLanguages();

            string saved = Settings.Default.Language;
            _current = FindLanguage(saved) ?? DefaultLanguage;
            ApplyCulture(_current);

            // Newly opened forms are localized automatically (mirrors FluentTheme's approach).
            if (!_idleHooked)
            {
                _idleHooked = true;
                Application.Idle += (_, _) => LocalizeNewForms();
            }
        }

        /// <summary>Switches the active language, persists it and refreshes every open form.</summary>
        public static void SetLanguage(string code)
        {
            code = FindLanguage(code) ?? DefaultLanguage;
            if (string.Equals(_current, code, StringComparison.OrdinalIgnoreCase))
                return;

            _current = code;
            Settings.Default.Language = code;
            Settings.Default.Save();
            ApplyCulture(code);
            ReapplyAll();
            LanguageChanged?.Invoke();
        }

        /// <summary>Translates one English source string using the active language.</summary>
        public static string Translate(string english)
        {
            if (string.IsNullOrEmpty(english) || _current == DefaultLanguage)
                return english;

            if (_translations.TryGetValue(_current, out Dictionary<string, string>? map) &&
                map.TryGetValue(english, out string? value) &&
                !string.IsNullOrEmpty(value))
                return value;

            return english;
        }

        /// <summary>
        /// Localizes a control tree (a form or any control). The first call captures the
        /// designer/default English text as the translation source. Later calls reapply
        /// the active language using those stored originals.
        /// </summary>
        public static void Apply(Control root)
        {
            if (root is null || root.IsDisposed)
                return;

            if (!_trackedRoots.Contains(root))
            {
                _trackedRoots.Add(root);
                if (root is Form form)
                    form.FormClosed += (_, _) => _trackedRoots.Remove(root);
                else
                    root.Disposed += (_, _) => _trackedRoots.Remove(root);
            }

            ApplyTree(root);
        }

        private static void LoadLanguages()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                    !name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string code = name.Substring(
                    ResourcePrefix.Length,
                    name.Length - ResourcePrefix.Length - ResourceSuffix.Length);

                // Only flat files (Localization/de.json), never nested resource names.
                if (code.Length == 0 || code.Contains('.'))
                    continue;

                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    using Stream stream = assembly.GetManifestResourceStream(name)!;
                    using JsonDocument doc = JsonDocument.Parse(stream);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.String)
                                map[property.Name] = property.Value.GetString() ?? string.Empty;
                        }
                    }
                }
                catch
                {
                    // A malformed translation file must never prevent the app from starting.
                }

                _translations[code] = map;
                _languages.Add(new LanguageInfo(code, GetNativeName(code)));
            }

            // Keep English at the top, then sort the rest by their native display name.
            _languages.Sort((a, b) =>
            {
                if (a.Code == DefaultLanguage) return -1;
                if (b.Code == DefaultLanguage) return 1;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private static string GetNativeName(string code)
        {
            if (code == DefaultLanguage)
                return "English";
            try
            {
                return CultureInfo.GetCultureInfo(code).NativeName;
            }
            catch
            {
                return code;
            }
        }

        private static string? FindLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;
            foreach (LanguageInfo language in _languages)
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                    return language.Code;
            return null;
        }

        private static void ApplyCulture(string code)
        {
            try
            {
                // Only UI culture changes; CurrentCulture stays invariant so parsing of
                // game data, numbers and dates is never affected by the selected language.
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(code);
            }
            catch
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            }
        }

        private static void LocalizeNewForms()
        {
            foreach (Form form in Application.OpenForms)
                if (!_trackedRoots.Contains(form))
                    Apply(form);
        }

        private static void ReapplyAll()
        {
            foreach (object root in _trackedRoots.ToArray())
                if (root is Control control && !control.IsDisposed)
                    ApplyTree(control);

            // Covers any form opened between a language change and the next idle tick.
            foreach (Form form in Application.OpenForms)
                if (!form.IsDisposed)
                    Apply(form);
        }

        private static void ApplyTree(object node)
        {
            switch (node)
            {
                case Control control:
                    if (!IsDataEntryControl(control))
                        ApplyText(control, control.Text, text => control.Text = text);

                    foreach (Control child in control.Controls)
                        ApplyTree(child);

                    if (control is ToolStrip strip)
                        foreach (ToolStripItem item in strip.Items)
                            ApplyTree(item);
                    break;

                case ToolStripItem item:
                    ApplyText(item, item.Text, text => item.Text = text);

                    if (item is ToolStripDropDownItem dropDown)
                        foreach (ToolStripItem child in dropDown.DropDownItems)
                            ApplyTree(child);
                    break;
            }
        }

        // Data-entry controls display user/file data rather than UI labels.
        private static bool IsDataEntryControl(Control control) =>
            control is TextBoxBase or ComboBox or ListBox or ListView or TreeView
                or DataGridView or NumericUpDown or DateTimePicker;

        private static void ApplyText(object target, string? current, Action<string> set)
        {
            if (string.IsNullOrEmpty(current))
                return;

            if (!_originals.TryGetValue(target, out OriginalText? original))
            {
                original = new OriginalText(current);
                _originals.Add(target, original);
            }

            string translated = Translate(original.Text);
            if (!string.Equals(current, translated, StringComparison.Ordinal))
                set(translated);
        }
    }
}
