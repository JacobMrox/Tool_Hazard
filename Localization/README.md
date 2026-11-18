# Localization

Tool Hazard uses a small, data-driven localization layer (`Localization/LocalizationManager.cs`).
The user picks a language live from **Settings → Language** in the main window; there is no restart.

## How it works

* Every `Localization/<culture>.json` file is embedded automatically (see the wildcard in
  `Tool Hazard.csproj`) and discovered at startup — no registration code is needed.
* Each file is a flat JSON object mapping the **English source string** to its translation:

  ```json
  {
    "File": "Datei",
    "Exit": "Beenden"
  }
  ```

* The English text set by the WinForms designer is the key. On first display the manager
  remembers that English text, so switching languages is repeatable and any missing key
  falls back to English automatically.
* The choice is saved in `Properties.Settings.Default.Language` and applied to all open
  forms immediately (including forms opened later).

## Adding a language

1. Create `Localization/<culture>.json`, e.g. `Localization/it.json` (use the standard culture
   code: `de`, `fr`, `es`, `it`, `pt`, `nl`, `ru`, `pl`, `uk`, `cs`, `sv`, `el`, `tr`, `ar`,
   `he`, `fa`, ...).
2. Copy the keys from `en.json`/another language and translate the values.
3. Build. The language appears in **Settings → Language** automatically.

## Adding/improving a translation

Just add or edit a key in any `<culture>.json`. The value must be the exact English string
used by the control (including punctuation and `...`). Keys not present in a file simply
stay in English.

## Using localization in code

```csharp
using Tool_Hazard.Localization;

LocalizationManager.Initialize();               // once, at startup (already in Program.cs)
string text = LocalizationManager.Translate("Ready");   // translate a dynamic string
LocalizationManager.SetLanguage("de");          // switch live (persists + refreshes all forms)
LocalizationManager.Apply(this);                // optional: localize a custom control tree
```

Static labels/menu items do not need any code: `LocalizationManager` walks open forms via
`Application.Idle` and translates any control/`ToolStripItem` whose text matches a key.
