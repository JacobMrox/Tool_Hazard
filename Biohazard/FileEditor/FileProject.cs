// Biohazard/FileEditor/FileProject.cs
// Based on https://github.com/Gemini-Loboto3/RE1-Mod-SDK/tree/master/File%20Editor
// This is a port of the editor XML project format from the original C++ tool,
// which uses a simple structure with a root <Strings> element and child <Text> elements for each page.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tool_Hazard.Biohazard.FileEditor
{
    /// <summary>
    /// Loads/saves the editor XML project format:
    ///
    /// <Strings>
    ///   <Text>... with \\n stored ...</Text>
    /// </Strings>
    /// </summary>
    public sealed class FileProject
    {
        public List<string> Pages { get; } = new();

        public static FileProject New()
        {
            var p = new FileProject();
            p.Pages.Add("{center}New FILE");
            return p;
        }

        public static FileProject Load(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("Project XML not found", xmlPath);

            var doc = XDocument.Load(xmlPath, LoadOptions.None);
            var root = doc.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "Strings", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid project XML: root must be <Strings>.");

            var p = new FileProject();
            foreach (var t in root.Elements().Where(x => string.Equals(x.Name.LocalName, "Text", StringComparison.OrdinalIgnoreCase)))
            {
                var raw = t.Value ?? string.Empty;
                p.Pages.Add(raw.Replace("\\n", "\n"));
            }

            if (p.Pages.Count == 0)
                throw new InvalidDataException("Project contains no <Text> entries.");

            return p;
        }

        public void Save(string xmlPath)
        {
            if (Pages.Count == 0)
                throw new InvalidOperationException("Cannot save a project with 0 pages.");

            var root = new XElement("Strings",
                Pages.Select(p => new XElement("Text", (p ?? string.Empty).Replace("\n", "\\n")))
            );

            // Emit UTF-8 with BOM like the C++ tool (xml.SetBOM(true)).
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

            using var fs = new FileStream(xmlPath, FileMode.Create, FileAccess.Write, FileShare.None);
            // UTF8 BOM:
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            using var sw = new StreamWriter(fs, utf8Bom);
            doc.Save(sw);
        }

        public void MoveUp(int index)
        {
            if (index <= 0 || index >= Pages.Count) return;
            (Pages[index - 1], Pages[index]) = (Pages[index], Pages[index - 1]);
        }

        public void MoveDown(int index)
        {
            if (index < 0 || index >= Pages.Count - 1) return;
            (Pages[index], Pages[index + 1]) = (Pages[index + 1], Pages[index]);
        }
    }
}
