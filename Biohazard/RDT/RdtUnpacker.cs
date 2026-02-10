// Tool_Hazard/Biohazard/RDT/RdtUnpacker.cs
#nullable enable
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Room;
using System.Text;

namespace Tool_Hazard.Biohazard.RDT
{
    public sealed class RdtUnpacker
    {
        private readonly BioVersion _version;
        private readonly string _inputPath;
        private readonly string _outputPath;

        public string BasePath => _outputPath;
        public string FileName => Path.GetFileName(_inputPath);

        public RdtUnpacker(BioVersion version, string inputPath, string outputPath)
        {
            _version = version;
            _inputPath = inputPath;
            _outputPath = outputPath;
        }

        public void Unpack()
        {
            try
            {
                Directory.CreateDirectory(BasePath);

                if (_version == BioVersion.Biohazard1)
                {
                    var rdt = new Rdt1(_inputPath);
                    var b = rdt.ToBuilder();

                    WriteHeaderIni_Rdt1(b.Header);

                    // Core blocks
                    WriteFile("light.lit", b.LIT);
                    WriteFile("camera.rid", b.RID);
                    WriteFile("zone.rvd", b.RVD);
                    WriteFile("sprite.pri", b.PRI);
                    WriteFile("collision.sca", b.SCA);
                    WriteFile("collision.sca.term", b.SCATerminator is int t ? BitConverter.GetBytes(t) : Array.Empty<byte>());
                    WriteFile("block.blk", b.BLK);
                    WriteFile("floor.flr", b.FLR);

                    // Scripts (containers)
                    WriteFile("Script/init.scd", b.InitSCD.Data.ToArray());
                    WriteFile("Script/main.scd", b.MainSCD.Data.ToArray());
                    WriteFile("Script/event.scd", b.EventSCD.Data.ToArray());

                    // Anim
                    if (b.EMR is not null) WriteFile("Animation/anim.emr", b.EMR.Data);
                    if (b.EDD is not null) WriteFile("Animation/anim.edd", b.EDD.Data);

                    // Message
                    WriteFile("Message/main.msg", b.MSG);

                    // Item icons
                    if (b.EmbeddedItemIcons.Data.Length != 0)
                        WriteFile("Item/icons.bin", b.EmbeddedItemIcons.Data.ToArray());

                    // Effects
                    WriteEffs(b.EmbeddedEffects);

                    // Embedded camera textures (TIMs)
                    for (int i = 0; i < b.CameraTextures.Count; i++)
                        WriteFile($"Camera/cam{i:00}.tim", b.CameraTextures[i].Data);

                    // Embedded object models (TMD/TIM) + table
                    WriteModelTextureIndexTable("Tables/object_table.bin", b.EmbeddedObjectModelTable);
                    for (int i = 0; i < b.EmbeddedObjectTmd.Count; i++)
                        WriteFile($"Object/obj_tmd{i:00}.tmd", b.EmbeddedObjectTmd[i].Data.ToArray());
                    for (int i = 0; i < b.EmbeddedObjectTim.Count; i++)
                        WriteFile($"Object/obj_tim{i:00}.tim", b.EmbeddedObjectTim[i].Data);

                    // Embedded item models (TMD/TIM) + table
                    WriteModelTextureIndexTable("Tables/item_table.bin", b.EmbeddedItemModelTable);
                    for (int i = 0; i < b.EmbeddedItemTmd.Count; i++)
                        WriteFile($"Item/item_tmd{i:00}.tmd", b.EmbeddedItemTmd[i].Data.ToArray());
                    for (int i = 0; i < b.EmbeddedItemTim.Count; i++)
                        WriteFile($"Item/item_tim{i:00}.tim", b.EmbeddedItemTim[i].Data);

                    // Sound
                    WriteFile("Sound/snd0.edt", b.EDT);
                    WriteFile("Sound/snd0.vh", b.VH);
                    WriteFile("Sound/snd0.vb", b.VB);
                }
                else
                {
                    var rdt = new Rdt2(_version, _inputPath);
                    var b = rdt.ToBuilder();

                    WriteHeaderIni_Rdt2(b.Header);

                    // Standard blocks
                    WriteFile("block.blk", b.BLK);
                    WriteFile("camera.rid", b.RID.Data.ToArray());
                    WriteFile("collision.sca", b.SCA);
                    WriteFile("floor.flr", b.FLR);
                    WriteFile("floor.flt", b.FLRTerminator is ushort ft ? BitConverter.GetBytes(ft) : Array.Empty<byte>());
                    WriteFile("light.lit", b.LIT);
                    WriteFile("sprite.pri", b.PRI);
                    WriteFile("zone.rvd", b.RVD);

                    // Scripts (procedure lists)
                    WriteScds("Script/init{0:00}.scd", b.SCDINIT);
                    if (_version == BioVersion.Biohazard2)
                        WriteScds("Script/main{0:00}.scd", b.SCDMAIN);

                    // Messages
                    WriteMsgs(MsgLanguage.Japanese, "Message/ja{0:00}.msg", b.MSGJA);
                    WriteMsgs(MsgLanguage.English, "Message/en{0:00}.msg", b.MSGEN);

                    // Scroll TIM
                    if (!b.TIMSCROLL.Data.IsEmpty) WriteFile("scroll.tim", b.TIMSCROLL.Data.ToArray());

                    // Objects (MD1/TIM) via embedded table
                    WriteObjects(b);

                    // Effects
                    // RE2/RE3: both can be embedded effects; RE3 often preserves EspTable too
                    WriteFile("Effect/effect.tbl", b.EspTable.Data.ToArray());
                    WriteEffs(b.EmbeddedEffects);

                    // RBJ (RE2 only in builder writer; RE3 typically absent)
                    if (_version != BioVersion.Biohazard3 && !b.RBJ.Data.IsEmpty)
                        WriteFile("Animation/anim.rbj", b.RBJ.Data.ToArray());

                    // ETD (RE3)
                    if (_version == BioVersion.Biohazard3 && !b.ETD.Data.IsEmpty)
                        WriteFile("Effect/effect.etd", b.ETD.Data.ToArray());

                    // Sound
                    WriteFile("Sound/snd0.edt", b.EDT);
                    WriteFile("Sound/snd0.vh", b.VH);
                    WriteFile("Sound/snd0.vb", b.VB);
                    if (b.VBOFFSET is int vboff)
                        WriteFile("Sound/snd0.vbx", BitConverter.GetBytes(vboff));
                }

                MessageBox.Show($"{_version} RDT unpacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unpack error:\n\n{ex}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------- INI (Header) ----------------

        private void WriteHeaderIni_Rdt1(Rdt1.Rdt1Header h)
        {
            var ini = new IniFile();
            ini.Set("Header", "nSprite", h.nSprite.ToString());
            ini.Set("Header", "nCut", h.nCut.ToString());
            ini.Set("Header", "nOmodel", h.nOmodel.ToString());
            ini.Set("Header", "nItem", h.nItem.ToString());
            ini.Set("Header", "nDoor", h.nDoor.ToString());
            ini.Set("Header", "nRoom_at", h.nRoom_at.ToString());
            WriteText("header.ini", ini.ToString());
        }

        private void WriteHeaderIni_Rdt2(Rdt2.Rdt2Header h)
        {
            var ini = new IniFile();
            ini.Set("Header", "nSprite", h.nSprite.ToString());
            ini.Set("Header", "nCut", h.nCut.ToString());
            ini.Set("Header", "nOmodel", h.nOmodel.ToString());
            ini.Set("Header", "nItem", h.nItem.ToString());
            ini.Set("Header", "nDoor", h.nDoor.ToString());
            ini.Set("Header", "nRoom_at", h.nRoom_at.ToString());
            ini.Set("Header", "Reverb_lv", h.Reverb_lv.ToString());
            ini.Set("Header", "unknown7", h.unknown7.ToString());
            WriteText("header.ini", ini.ToString());
        }

        // ---------------- Writers ----------------

        private void WriteEffs(EmbeddedEffectList effects)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                var id = effects[i].Id;
                if (!effects[i].Eff.Data.IsEmpty)
                    WriteFile($"Effect/esp{id:X2}.eff", effects[i].Eff.Data.ToArray());
                if (!effects[i].Tim.Data.IsEmpty)
                    WriteFile($"Effect/esp{id:X2}.tim", effects[i].Tim.Data.ToArray());
            }
        }

        private void WriteObjects(Rdt2.Builder b)
        {
            // Export per embedded table index, stable naming object00.md1/tim
            var table = b.EmbeddedObjectModelTable;
            for (int i = 0; i < table.Count; i++)
            {
                var md1Index = table[i].Model;
                var timIndex = table[i].Texture;

                if (md1Index != -1 && md1Index < b.EmbeddedObjectMd1.Count)
                    WriteFile($"Object/object{i:00}.md1", b.EmbeddedObjectMd1[md1Index].Data.ToArray());

                if (timIndex != -1 && timIndex < b.EmbeddedObjectTim.Count)
                    WriteFile($"Object/object{i:00}.tim", b.EmbeddedObjectTim[timIndex].Data.ToArray());
            }
        }

        private void WriteMsgs(MsgLanguage lng, string fmt, MsgList list)
        {
            for (int i = 0; i < list.Count; i++)
                WriteFile(string.Format(fmt, i), list[i].Data);
        }

        private void WriteScds(string fmt, ScdProcedureList list)
        {
            for (int i = 0; i < list.Count; i++)
                WriteFile(string.Format(fmt, i), list[i].Data.Span.ToArray());
        }

        private void WriteModelTextureIndexTable(string relativePath, List<ModelTextureIndex> list)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(list.Count);
            foreach (var mt in list)
            {
                bw.Write(mt.Model);
                bw.Write(mt.Texture);
            }
            WriteFile(relativePath, ms.ToArray());
        }

        // ---------------- File helpers ----------------

        private void WriteText(string relativePath, string text)
        {
            var destPath = Path.Combine(BasePath, relativePath);
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(destPath, text, new UTF8Encoding(false));
        }

        private void WriteFile(string relativePath, byte[] data)
        {
            if (data.Length == 0) return;
            WriteFile(relativePath, (ReadOnlySpan<byte>)data);
        }

        private void WriteFile(string relativePath, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            WriteFile(relativePath, data.Span);
        }

        private void WriteFile(string relativePath, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            var destPath = Path.Combine(BasePath, relativePath);
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(destPath, data.ToArray());
        }

        // ---------------- Minimal INI ----------------

        private sealed class IniFile
        {
            private readonly Dictionary<string, Dictionary<string, string>> _data = new(StringComparer.OrdinalIgnoreCase);

            public void Set(string section, string key, string value)
            {
                if (!_data.TryGetValue(section, out var sec))
                {
                    sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _data[section] = sec;
                }
                sec[key] = value;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var sec in _data)
                {
                    sb.Append('[').Append(sec.Key).AppendLine("]");
                    foreach (var kv in sec.Value)
                        sb.Append(kv.Key).Append('=').AppendLine(kv.Value);
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }
    }
}
