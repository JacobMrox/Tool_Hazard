// Tool_Hazard/Biohazard/RDT/RdtPacker.cs
#nullable enable
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Room;
using System.Runtime.InteropServices;

namespace Tool_Hazard.Biohazard.RDT
{
    public sealed class RdtPacker
    {
        private readonly BioVersion _version;
        private readonly string _folderPath; // folder that contains header.ini and subfolders
        private readonly string _targetPath;

        public string BasePath => _folderPath;
        public string TargetPath => _targetPath;

        public RdtPacker(BioVersion version, string folderPath)
        {
            _version = version;
            _folderPath = folderPath;
            _targetPath = Path.Combine(folderPath, "packed.RDT");
        }

        public void Pack()
        {
            try
            {
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

                MessageBox.Show($"{_version} RDT repacked successfully!\n\n{TargetPath}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pack error:\n\n{ex}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            for (int i = 0; i < 100; i++)
            {
                var data = ReadFile(string.Format(fmt, i));
                if (data.Length == 0) break;
                builder.Procedures.Add(new ScdProcedure(_version, data));
            }
            return builder.ToProcedureList();
        }

        private MsgList ReadMsgs(MsgLanguage lng, string fmt)
        {
            var builder = new MsgList.Builder();
            for (int i = 0; i < 100; i++)
            {
                var data = ReadFile(string.Format(fmt, i));
                if (data.Length == 0) break;
                builder.Messages.Add(new Msg(_version, lng, data));
            }
            return builder.ToMsgList();
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
