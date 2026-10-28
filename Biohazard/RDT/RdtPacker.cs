// Tool_Hazard/Biohazard/RDT/RdtPacker.cs
#nullable enable
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Room;
using System.Runtime.InteropServices;
using Tool_Hazard.IntelOrca.Biohazard;
using Tool_Hazard.IntelOrca.Biohazard.Room;

namespace Tool_Hazard.Biohazard.RDT
{
    public sealed class RdtPacker
    {
        private readonly BioVersion _version;
        private readonly string _folderPath; // folder that contains header.ini and subfolders
        private readonly string _targetPath;
        private readonly bool _showDialogs;

        public string BasePath => _folderPath;
        public string TargetPath => _targetPath;

        public RdtPacker(BioVersion version, string folderPath, bool showDialogs = true)
        {
            _version = version;
            _folderPath = folderPath;
            _targetPath = Path.Combine(folderPath, "packed.RDT");
            _showDialogs = showDialogs;
        }

        public bool Pack()
        {
            try
            {
                var sourcePath = Path.Combine(BasePath, "_source.RDT");
                if (_version != BioVersion.Biohazard1 && File.Exists(sourcePath))
                {
                    PackRdt2FromSource(sourcePath);
                    if (_showDialogs)
                        MessageBox.Show($"{_version} RDT repacked successfully!\n\n{TargetPath}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }

                if (_version == BioVersion.Biohazard1)
                {
                    var b = new Rdt1.Builder();

                    // Header (INI editable; critical counts recomputed below)
                    var header = ReadHeaderIni_Rdt1();
                    b.Header = header;

                    // Required blocks
                    b.LIT = ReadFile("light.lit");
                    b.RID = ReadFile("camera.rid");
                    b.RVD = ReadFile("zone.rvd");
                    b.PRI = ReadFile("sprite.pri");
                    b.SCA = ReadFile("collision.sca");
                    b.BLK = ReadFile("block.blk");
                    b.FLR = ReadFile("floor.flr");

                    var scaTerm = ReadFile("collision.sca.term");
                    if (scaTerm.Length != 0) b.SCATerminator = BitConverter.ToInt32(scaTerm, 0);

                    // Scripts (containers)
                    b.InitSCD = new ScdProcedureContainer(ReadFile("Script/init.scd"));
                    b.MainSCD = new ScdProcedureContainer(ReadFile("Script/main.scd"));
                    b.EventSCD = new ScdEventList(ReadFile("Script/event.scd"));

                    // Anim
                    var emr = ReadFile("Animation/anim.emr");
                    var edd = ReadFile("Animation/anim.edd");
                    b.EMR = emr.Length == 0 ? null : new Emr(_version, emr);
                    b.EDD = edd.Length == 0 ? null : new Edd1(_version, edd);

                    // Msg
                    b.MSG = ReadFile("Message/main.msg");

                    // Item icons
                    var icons = ReadFile("Item/icons.bin");
                    b.EmbeddedItemIcons = new EmbeddedItemIcons(icons);

                    // Effects
                    b.EmbeddedEffects = ReadEffs();

                    // Camera textures
                    b.CameraTextures = ReadTimSequence("Camera/cam{0:00}.tim");

                    // Embedded object assets + table
                    b.EmbeddedObjectTmd = ReadTmdSequence("Object/obj_tmd{0:00}.tmd");
                    b.EmbeddedObjectTim = ReadTimSequence("Object/obj_tim{0:00}.tim");
                    b.EmbeddedObjectModelTable = ReadModelTextureIndexTable("Tables/object_table.bin");

                    // Embedded item assets + table
                    b.EmbeddedItemTmd = ReadTmdSequence("Item/item_tmd{0:00}.tmd");
                    b.EmbeddedItemTim = ReadTimSequence("Item/item_tim{0:00}.tim");
                    b.EmbeddedItemModelTable = ReadModelTextureIndexTable("Tables/item_table.bin");

                    // Sound
                    b.EDT = ReadFile("Sound/snd0.edt");
                    b.VH = ReadFile("Sound/snd0.vh");
                    b.VB = ReadFile("Sound/snd0.vb");

                    // ---- Recompute MUST-MATCH header counts ----
                    b.Header = FixCounts_Rdt1(b.Header, b);

                    var rdt = b.ToRdt();
                    File.WriteAllBytes(TargetPath, rdt.Data.ToArray());
                }
                else
                {
                    var b = new Rdt2.Builder(_version);

                    // Header from INI; counts fixed below
                    var header = ReadHeaderIni_Rdt2();
                    b.Header = header;

                    // Blocks
                    b.BLK = ReadFile("block.blk");
                    b.RID = new Rid(ReadFile("camera.rid"));
                    b.RVD = ReadFile("zone.rvd");
                    b.LIT = ReadFile("light.lit");
                    b.PRI = ReadFile("sprite.pri");
                    b.SCA = ReadFile("collision.sca");
                    b.FLR = ReadFile("floor.flr");

                    var flt = ReadFile("floor.flt");
                    if (flt.Length != 0) b.FLRTerminator = BitConverter.ToUInt16(flt, 0);

                    // Scripts
                    b.SCDINIT = ReadScds("Script/init{0:00}.scd");
                    if (_version == BioVersion.Biohazard2)
                        b.SCDMAIN = ReadScds("Script/main{0:00}.scd");
                    else
                        b.SCDMAIN = new ScdProcedureList(); // RE3 not used

                    // Messages
                    b.MSGJA = ReadMsgs(MsgLanguage.Japanese, "Message/ja{0:00}.msg");
                    b.MSGEN = ReadMsgs(MsgLanguage.English, "Message/en{0:00}.msg");

                    // Scroll TIM
                    var scroll = ReadFile("scroll.tim");
                    b.TIMSCROLL = scroll.Length == 0 ? new Tim() : new Tim(scroll);

                    // Objects
                    ReadObjects(b);

                    // Effects
                    b.EspTable = new EspTable(_version, ReadFile("Effect/effect.tbl"));
                    b.EmbeddedEffects = ReadEffs();

                    // RE2 optional RBJ
                    var rbj = ReadFile("Animation/anim.rbj");
                    b.RBJ = rbj.Length == 0 ? new Rbj(_version, Array.Empty<byte>()) : new Rbj(_version, rbj);

                    // RE3 ETD (if provided)
                    if (_version == BioVersion.Biohazard3)
                    {
                        var etd = ReadFile("Effect/effect.etd");
                        b.ETD = etd.Length == 0 ? new Etd() : new Etd(etd);
                    }

                    // Sound
                    b.EDT = ReadFile("Sound/snd0.edt");
                    b.VH = ReadFile("Sound/snd0.vh");
                    b.VB = ReadFile("Sound/snd0.vb");

                    var vbx = ReadFile("Sound/snd0.vbx");
                    if (vbx.Length != 0) b.VBOFFSET = BitConverter.ToInt32(vbx, 0);

                    // ---- Recompute MUST-MATCH header counts ----
                    b.Header = FixCounts_Rdt2(b.Header, b);

                    var rdt = b.ToRdt();
                    File.WriteAllBytes(TargetPath, rdt.Data.ToArray());
                }

                if (_showDialogs)
                    MessageBox.Show($"{_version} RDT repacked successfully!\n\n{TargetPath}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                if (!_showDialogs)
                    throw;
                MessageBox.Show($"Pack error:\n\n{ex}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void PackRdt2FromSource(string sourcePath)
        {
            var source = new Rdt2(_version, sourcePath);
            var b = source.ToBuilder();

            // Header fields remain editable, but begin with the exact source values.
            if (File.Exists(Path.Combine(BasePath, "header.ini")))
                b.Header = ReadHeaderIni_Rdt2();

            ReplaceIfPresent("block.blk", value => b.BLK = value);
            ReplaceIfPresent("camera.rid", value => b.RID = new Rid(value));
            ReplaceIfPresent("zone.rvd", value => b.RVD = value);
            ReplaceIfPresent("light.lit", value => b.LIT = value);
            ReplaceIfPresent("sprite.pri", value => b.PRI = value);
            ReplaceIfPresent("collision.sca", value => b.SCA = value);
            ReplaceIfPresent("floor.flr", value => b.FLR = value);

            var fltPath = Path.Combine(BasePath, "floor.flt");
            if (File.Exists(fltPath))
            {
                var data = File.ReadAllBytes(fltPath);
                if (data.Length != sizeof(ushort))
                    throw new InvalidDataException("floor.flt must contain exactly two bytes.");
                b.FLRTerminator = BitConverter.ToUInt16(data, 0);
            }

            if (HasSequence("Script/init{0:00}.scd"))
                b.SCDINIT = ReadScds("Script/init{0:00}.scd");
            if (_version == BioVersion.Biohazard2 && HasSequence("Script/main{0:00}.scd"))
                b.SCDMAIN = ReadScds("Script/main{0:00}.scd");

            if (HasSequence("Message/ja{0:00}.msg"))
                b.MSGJA = ReadMsgsPreservingPrefix(b.MSGJA, MsgLanguage.Japanese, "Message/ja{0:00}.msg");
            if (HasSequence("Message/en{0:00}.msg"))
                b.MSGEN = ReadMsgsPreservingPrefix(b.MSGEN, MsgLanguage.English, "Message/en{0:00}.msg");

            ReplaceIfPresent("scroll.tim", value => b.TIMSCROLL = new Tim(value));
            UpdateObjectsPreservingTable(b);
            UpdateEffectsPreservingSource(b);

            if (_version == BioVersion.Biohazard2)
                ReplaceIfPresent("Animation/anim.rbj", value => b.RBJ = new Rbj(_version, value));
            if (_version == BioVersion.Biohazard3)
                ReplaceIfPresent("Effect/effect.etd", value => b.ETD = new Etd(value));

            ReplaceIfPresent("Sound/snd0.edt", value => b.EDT = value);
            ReplaceIfPresent("Sound/snd0.vh", value => b.VH = value);
            ReplaceIfPresent("Sound/snd0.vb", value => b.VB = value);

            var vbxPath = Path.Combine(BasePath, "Sound", "snd0.vbx");
            if (File.Exists(vbxPath))
            {
                var data = File.ReadAllBytes(vbxPath);
                if (data.Length != sizeof(int))
                    throw new InvalidDataException("snd0.vbx must contain exactly four bytes.");
                b.VBOFFSET = BitConverter.ToInt32(data, 0);
            }

            b.Header = FixCounts_Rdt2(b.Header, b);
            var output = CanonicalizeAndValidateRepackedRdt(b.ToRdt().Data.ToArray());
            File.WriteAllBytes(TargetPath, output);
        }

        private void ReplaceIfPresent(string relativePath, Action<byte[]> replace)
        {
            var path = Path.Combine(BasePath, relativePath);
            if (File.Exists(path))
                replace(File.ReadAllBytes(path));
        }

        private bool HasSequence(string format) =>
            File.Exists(Path.Combine(BasePath, string.Format(format, 0)));

        private byte[] CanonicalizeAndValidateRepackedRdt(byte[] data)
        {
            // Construction parses every section and catches out-of-range offsets. A
            // first rebuild also normalizes tables whose physical asset order changed
            // after an edit (for example, replacing one embedded model with a smaller
            // one). The normalized result itself must be byte-stable before it is saved.
            var parsed = new Rdt2(_version, data);
            var canonical = parsed.ToBuilder().ToRdt().Data.ToArray();
            var validated = new Rdt2(_version, canonical).ToBuilder().ToRdt().Data.Span;
            if (!validated.SequenceEqual(canonical))
            {
                var commonLength = Math.Min(validated.Length, canonical.Length);
                var firstDifference = -1;
                for (var i = 0; i < commonLength; i++)
                {
                    if (validated[i] == canonical[i])
                        continue;
                    firstDifference = i;
                    break;
                }
                throw new InvalidDataException(
                    $"Repacked RDT failed the byte-stability validation pass " +
                    $"after normalization (bytes {canonical.Length}->{validated.Length}, " +
                    $"first difference 0x{firstDifference:X}).");
            }
            return canonical;
        }

        // ---------------- Count Fixups ----------------

        private static Rdt1.Rdt1Header FixCounts_Rdt1(Rdt1.Rdt1Header h, Rdt1.Builder b)
        {
            // Cameras: RID bytes / sizeof(Rdt1Camera)
            // IntelOrca Rdt1 camera struct size is known; compute safely via Marshal.
            int camSize = Marshal.SizeOf<Rdt1.Rdt1Camera>();
            int camCount = camSize == 0 ? 0 : (b.RID.Length / camSize);
            h.nCut = (byte)Math.Clamp(camCount, 0, 255);

            h.nOmodel = (byte)Math.Clamp(b.EmbeddedObjectModelTable.Count, 0, 255);
            h.nItem = (byte)Math.Clamp(b.EmbeddedItemModelTable.Count, 0, 255);

            // nSprite is not reliably computable; keep INI value.
            // nDoor/nRoom_at keep INI.
            return h;
        }

        private static Rdt2.Rdt2Header FixCounts_Rdt2(Rdt2.Rdt2Header h, Rdt2.Builder b)
        {
            // cameras from RID
            h.nCut = (byte)Math.Clamp(b.RID.Count, 0, 255);

            // objects must match or builder throws
            h.nOmodel = (byte)Math.Clamp(b.EmbeddedObjectModelTable.Count, 0, 255);

            // nSprite is preserved from INI; changing it blindly can crash some rooms.
            return h;
        }

        // ---------------- INI read ----------------

        private Rdt1.Rdt1Header ReadHeaderIni_Rdt1()
        {
            var ini = IniFile.Load(Path.Combine(BasePath, "header.ini"));
            return new Rdt1.Rdt1Header
            {
                nSprite = ini.GetByte("Header", "nSprite", 0),
                nCut = ini.GetByte("Header", "nCut", 0),
                nOmodel = ini.GetByte("Header", "nOmodel", 0),
                nItem = ini.GetByte("Header", "nItem", 0),
                nDoor = ini.GetByte("Header", "nDoor", 0),
                nRoom_at = ini.GetByte("Header", "nRoom_at", 0),
            };
        }

        private Rdt2.Rdt2Header ReadHeaderIni_Rdt2()
        {
            var ini = IniFile.Load(Path.Combine(BasePath, "header.ini"));
            return new Rdt2.Rdt2Header
            {
                nSprite = ini.GetByte("Header", "nSprite", 0),
                nCut = ini.GetByte("Header", "nCut", 0),
                nOmodel = ini.GetByte("Header", "nOmodel", 0),
                nItem = ini.GetByte("Header", "nItem", 0),
                nDoor = ini.GetByte("Header", "nDoor", 0),
                nRoom_at = ini.GetByte("Header", "nRoom_at", 0),
                Reverb_lv = ini.GetByte("Header", "Reverb_lv", 0),
                unknown7 = ini.GetByte("Header", "unknown7", 0),
            };
        }

        // ---------------- Effects ----------------

        private EmbeddedEffectList ReadEffs()
        {
            var list = new List<EmbeddedEffect>();
            for (byte id = 0; id < 255; id++)
            {
                var eff = ReadFile($"Effect/esp{id:X2}.eff");
                var tim = ReadFile($"Effect/esp{id:X2}.tim");
                if (eff.Length == 0 && tim.Length == 0)
                    continue;

                list.Add(new EmbeddedEffect(id,
                    eff.Length == 0 ? new Eff() : new Eff(eff),
                    tim.Length == 0 ? new Tim() : new Tim(tim)));
            }
            return new EmbeddedEffectList(_version, list.ToArray());
        }

        // ---------------- Objects (RE2/RE3) ----------------

        private void UpdateObjectsPreservingTable(Rdt2.Builder b)
        {
            for (var i = 0; i < b.EmbeddedObjectModelTable.Count; i++)
            {
                var entry = b.EmbeddedObjectModelTable[i];
                var modelIndex = entry.Model;
                var textureIndex = entry.Texture;

                var md1Path = Path.Combine(BasePath, $"Object/object{i:00}.md1");
                if (File.Exists(md1Path))
                {
                    var data = File.ReadAllBytes(md1Path);
                    var unchanged = modelIndex >= 0 &&
                        b.EmbeddedObjectMd1[modelIndex].Data.Span.SequenceEqual(data);
                    if (!unchanged)
                    {
                        if (modelIndex >= 0 && b.EmbeddedObjectModelTable.Count(x => x.Model == modelIndex) == 1)
                        {
                            b.EmbeddedObjectMd1[modelIndex] = new Md1(data);
                        }
                        else
                        {
                            modelIndex = b.EmbeddedObjectMd1.Count;
                            b.EmbeddedObjectMd1.Add(new Md1(data));
                        }
                    }
                }

                var timPath = Path.Combine(BasePath, $"Object/object{i:00}.tim");
                if (File.Exists(timPath))
                {
                    var data = File.ReadAllBytes(timPath);
                    var unchanged = textureIndex >= 0 &&
                        b.EmbeddedObjectTim[textureIndex].Data.Span.SequenceEqual(data);
                    if (!unchanged)
                    {
                        if (textureIndex >= 0 && b.EmbeddedObjectModelTable.Count(x => x.Texture == textureIndex) == 1)
                        {
                            b.EmbeddedObjectTim[textureIndex] = new Tim(data);
                        }
                        else
                        {
                            textureIndex = b.EmbeddedObjectTim.Count;
                            b.EmbeddedObjectTim.Add(new Tim(data));
                        }
                    }
                }

                b.EmbeddedObjectModelTable[i] = new ModelTextureIndex(modelIndex, textureIndex);
            }

            // Consecutive files after the source table are intentional additions.
            for (var i = b.EmbeddedObjectModelTable.Count; i < byte.MaxValue; i++)
            {
                var md1 = ReadFile($"Object/object{i:00}.md1");
                var tim = ReadFile($"Object/object{i:00}.tim");
                if (md1.Length == 0 && tim.Length == 0)
                    break;

                var modelIndex = -1;
                var textureIndex = -1;
                if (md1.Length != 0)
                {
                    modelIndex = b.EmbeddedObjectMd1.Count;
                    b.EmbeddedObjectMd1.Add(new Md1(md1));
                }
                if (tim.Length != 0)
                {
                    textureIndex = b.EmbeddedObjectTim.Count;
                    b.EmbeddedObjectTim.Add(new Tim(tim));
                }
                b.EmbeddedObjectModelTable.Add(new ModelTextureIndex(modelIndex, textureIndex));
            }
        }

        private void UpdateEffectsPreservingSource(Rdt2.Builder b)
        {
            var tablePath = Path.Combine(BasePath, "Effect", "effect.tbl");
            var tableChanged = false;
            byte[]? tableData = null;
            if (File.Exists(tablePath))
            {
                tableData = File.ReadAllBytes(tablePath);
                tableChanged = !b.EspTable.Data.Span.SequenceEqual(tableData);
            }

            var effects = b.EmbeddedEffects.Effects.ToArray().ToList();
            var effectsChanged = false;
            for (var id = 0; id < byte.MaxValue; id++)
            {
                var effPath = Path.Combine(BasePath, "Effect", $"esp{id:X2}.eff");
                var timPath = Path.Combine(BasePath, "Effect", $"esp{id:X2}.tim");
                if (!File.Exists(effPath) && !File.Exists(timPath))
                    continue;

                var index = effects.FindIndex(x => x.Id == id);
                var oldEff = index >= 0 ? effects[index].Eff : new Eff();
                var oldTim = index >= 0 ? effects[index].Tim : new Tim();
                var newEff = File.Exists(effPath) ? new Eff(File.ReadAllBytes(effPath)) : oldEff;
                var newTim = File.Exists(timPath) ? new Tim(File.ReadAllBytes(timPath)) : oldTim;

                if (oldEff.Data.Span.SequenceEqual(newEff.Data.Span) &&
                    oldTim.Data.Span.SequenceEqual(newTim.Data.Span))
                    continue;

                effectsChanged = true;
                var replacement = new EmbeddedEffect((byte)id, newEff, newTim);
                if (index >= 0)
                    effects[index] = replacement;
                else
                    effects.Add(replacement);
            }

            if (tableChanged && effectsChanged)
                throw new InvalidDataException("Both effect.tbl and individual ESP files changed. Edit one representation at a time.");

            if (tableChanged)
            {
                b.EspTable = new EspTable(_version, tableData!);
            }
            else if (effectsChanged)
            {
                b.EmbeddedEffects = new EmbeddedEffectList(_version, effects.ToArray());
                b.EspTable = new EspTable();
            }
        }

        private void ReadObjects(Rdt2.Builder b)
        {
            // Keep your “object00.md1/tim” convention, but compute table entries strictly.
            // No hashing/dedupe: keeps indices stable, avoids surprises.
            b.EmbeddedObjectModelTable.Clear();
            b.EmbeddedObjectMd1.Clear();
            b.EmbeddedObjectTim.Clear();

            for (int i = 0; i < 255; i++)
            {
                var md1Data = ReadFile($"Object/object{i:00}.md1");
                var timData = ReadFile($"Object/object{i:00}.tim");
                if (md1Data.Length == 0 && timData.Length == 0)
                    break;

                int md1Index = -1;
                int timIndex = -1;

                if (md1Data.Length != 0)
                {
                    md1Index = b.EmbeddedObjectMd1.Count;
                    b.EmbeddedObjectMd1.Add(new Md1(md1Data));
                }
                if (timData.Length != 0)
                {
                    timIndex = b.EmbeddedObjectTim.Count;
                    b.EmbeddedObjectTim.Add(new Tim(timData));
                }

                b.EmbeddedObjectModelTable.Add(new ModelTextureIndex(md1Index, timIndex));
            }
        }

        // ---------------- Scripts + Msgs ----------------

        private ScdProcedureList ReadScds(string fmt)
        {
            var builder = new ScdProcedureList.Builder(_version);
            for (int i = 0; i < 512; i++)
            {
                var path = Path.Combine(BasePath, string.Format(fmt, i));
                if (!File.Exists(path)) break;
                var data = File.ReadAllBytes(path);
                builder.Procedures.Add(new ScdProcedure(_version, data));
            }
            return builder.ToProcedureList();
        }

        private MsgList ReadMsgs(MsgLanguage lng, string fmt)
        {
            var builder = new MsgList.Builder();
            for (int i = 0; i < 512; i++)
            {
                var path = Path.Combine(BasePath, string.Format(fmt, i));
                if (!File.Exists(path)) break;
                var data = File.ReadAllBytes(path);
                builder.Messages.Add(new Msg(_version, lng, data));
            }
            return builder.ToMsgList();
        }

        private MsgList ReadMsgsPreservingPrefix(MsgList source, MsgLanguage lng, string fmt)
        {
            var messages = new List<byte[]>();
            for (var i = 0; i < 512; i++)
            {
                var path = Path.Combine(BasePath, string.Format(fmt, i));
                if (!File.Exists(path))
                    break;
                messages.Add(File.ReadAllBytes(path));
            }

            if (messages.Count != source.Count || source.Data.Length < sizeof(ushort))
                return ReadMsgs(lng, fmt);

            var firstOffset = BitConverter.ToUInt16(source.Data.Span.Slice(0, sizeof(ushort)));
            var normalTableLength = messages.Count * sizeof(ushort);
            if (firstOffset < normalTableLength || firstOffset > source.Data.Length)
                return ReadMsgs(lng, fmt);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            var offset = (int)firstOffset;
            foreach (var message in messages)
            {
                bw.Write((ushort)offset);
                offset += message.Length;
            }

            // A few retail PC rooms contain a deliberate byte between the offset
            // table and the first message. Preserve that prefix exactly.
            bw.Write(source.Data.Slice(normalTableLength, firstOffset - normalTableLength).Span);
            foreach (var message in messages)
                bw.Write(message);

            return new MsgList(_version, lng, ms.ToArray());
        }

        // ---------------- RE1 helpers ----------------

        private List<Tim> ReadTimSequence(string fmt)
        {
            var list = new List<Tim>();
            for (int i = 0; i < 512; i++)
            {
                var data = ReadFile(string.Format(fmt, i));
                if (data.Length == 0) break;
                list.Add(new Tim(data));
            }
            return list;
        }

        private List<Tmd> ReadTmdSequence(string fmt)
        {
            var list = new List<Tmd>();
            for (int i = 0; i < 512; i++)
            {
                var data = ReadFile(string.Format(fmt, i));
                if (data.Length == 0) break;
                list.Add(new Tmd(data));
            }
            return list;
        }

        private List<ModelTextureIndex> ReadModelTextureIndexTable(string relativePath)
        {
            var data = ReadFile(relativePath);
            if (data.Length == 0)
                return new List<ModelTextureIndex>();

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            int count = br.ReadInt32();
            var list = new List<ModelTextureIndex>(count);
            for (int i = 0; i < count; i++)
            {
                int model = br.ReadInt32();
                int tex = br.ReadInt32();
                list.Add(new ModelTextureIndex(model, tex));
            }
            return list;
        }

        // ---------------- File IO ----------------

        private byte[] ReadFile(string relativePath)
        {
            var full = Path.Combine(BasePath, relativePath);
            return File.Exists(full) ? File.ReadAllBytes(full) : Array.Empty<byte>();
        }

        // ---------------- Minimal INI ----------------

        private sealed class IniFile
        {
            private readonly Dictionary<string, Dictionary<string, string>> _data =
                new(StringComparer.OrdinalIgnoreCase);

            public static IniFile Load(string path)
            {
                var ini = new IniFile();
                if (!File.Exists(path)) return ini;

                string section = "Header";
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    ini.Set(section, key, val);
                }
                return ini;
            }

            private void Set(string section, string key, string value)
            {
                if (!_data.TryGetValue(section, out var sec))
                {
                    sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _data[section] = sec;
                }
                sec[key] = value;
            }

            public byte GetByte(string section, string key, byte defaultValue)
            {
                if (_data.TryGetValue(section, out var sec) &&
                    sec.TryGetValue(key, out var v) &&
                    byte.TryParse(v, out var b))
                    return b;
                return defaultValue;
            }
        }
    }
}
