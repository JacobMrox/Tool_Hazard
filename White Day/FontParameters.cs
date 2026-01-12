using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace White_Day_Mod_Tool
{
    public class FontParameters
    {
        public List<FontCharacter> Characters { get; private set; } = new List<FontCharacter>();

        public void Load(string path)
        {
            Characters.Clear();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                int numChars = br.ReadInt32();
                for (int i = 0; i < numChars; i++)
                {
                    int charCode = br.ReadInt32();
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    int width = br.ReadInt32();
                    int height = br.ReadInt32();

                    Characters.Add(new FontCharacter
                    {
                        Character = charCode,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    });
                }
            }
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Characters.Count);
                foreach (var ch in Characters)
                {
                    bw.Write(ch.Character);
                    bw.Write(ch.X);
                    bw.Write(ch.Y);
                    bw.Write(ch.Width);
                    bw.Write(ch.Height);
                }
            }
        }
    }
}
