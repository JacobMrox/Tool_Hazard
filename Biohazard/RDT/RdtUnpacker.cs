using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Room;

namespace Tool_Hazard.Biohazard.RDT
{
    public class RdtUnpacker(BioVersion version, string inputPath, string outputPath)
    {
        public string BasePath { get; } = outputPath;
        public string FileName { get; } = Path.GetFileName(inputPath);
        public void UnpackBio1()
        {
            var rdt1 = new Rdt1(inputPath);
            var rdt1b = rdt1.ToBuilder();
            WriteHeaderRdt1(rdt1b.Header);
            //WriteFile("Animation/anim.rbj", rdt1b.RBJ.Data);
            WriteFile("Animation/anim.edd", rdt1b.EDD.Data);
            WriteFile("Animation/anim.emr", rdt1b.EMR.Data);
            WriteEffs(rdt1b.EmbeddedEffects);
            //WriteFile("Effect/effect.etd", rdt1b.ETD.Data);
            WriteFile("Effects/effects.etd", rdt1b.EDT);
            //WriteFile("Effect/effect.tbl", rdt1b.EspTable.Data);
            //WriteMessages(rdt1b.MSGJA, rdt1b.MSGEN);
            WriteFile("Message/main[i].msg", rdt1b.MSG);
            //WriteObjects(rdt1b);
            //WriteScds(rdt1b.SCDINIT, rdt1b.SCDMAIN);
            //WriteScds(rdt1b.InitSCD, rdt1b.MainSCD);
            WriteFile("Sound/snd0.edt", rdt1b.EDT);
            WriteFile("Sound/snd0.vh", rdt1b.VH);
            WriteFile("Sound/snd0.vb", rdt1b.VB);
            //if (rdt1b.VBOFFSET is int vboffset)
            //WriteFile("Sound/snd0.vbx", BitConverter.GetBytes(vboffset));
            WriteFile("block.blk", rdt1b.BLK);
            WriteFile("camera.rid", rdt1b.RID);
            WriteFile("collision.sca", rdt1b.SCA);
            WriteFile("floor.flr", rdt1b.FLR);
            //WriteFile("floor.flt", rdt1b.FLRTerminator == null ? [] : BitConverter.GetBytes(rdt1b.FLRTerminator.Value));
            WriteFile("light.lit", rdt1b.LIT);
            //WriteFile("scroll.tim", rdt1b.TIMSCROLL.Data);
            WriteFile("sprite.pri", rdt1b.PRI);
            WriteFile("zone.rvd", rdt1b.RVD);
            //Print success message
            MessageBox.Show($"{version} RDT unpacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        public void Unpack()
        {
            if (version == BioVersion.Biohazard1)
            {
                MessageBox.Show(
                    "RE1 RDT format is not compatible with the RDT2 unpacker.\n" +
                    "Therefore RE1 RDT Unpacker is experimental.",
                    "Experimental",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                var rdt1 = new Rdt1(inputPath);
                var rdt1b = rdt1.ToBuilder();
                WriteHeaderRdt1(rdt1b.Header);
                //WriteFile("Animation/anim.rbj", rdt1b.RBJ.Data);
                WriteFile("Animation/anim.edd", rdt1b.EDD.Data);
                WriteFile("Animation/anim.emr", rdt1b.EMR.Data);
                WriteEffs(rdt1b.EmbeddedEffects);
                //WriteFile("Effect/effect.etd", rdt1b.ETD.Data);
                WriteFile("Effects/effects.etd", rdt1b.EDT);
                //WriteFile("Effect/effect.tbl", rdt1b.EspTable.Data);
                //WriteMessages(rdt1b.MSGJA, rdt1b.MSGEN);
                //WriteMessage(rdt1b.MSG);
                WriteFile("Message/main.msg", rdt1b.MSG);
                //WriteObjects(rdt1b);
                //WriteScds(rdt1b.SCDINIT, rdt1b.SCDMAIN);
                //WriteScds(rdt1b.InitSCD, rdt1b.MainSCD);
                WriteFile("Sound/snd0.edt", rdt1b.EDT);
                WriteFile("Sound/snd0.vh", rdt1b.VH);
                WriteFile("Sound/snd0.vb", rdt1b.VB);
                //if (rdt1b.VBOFFSET is int vboffset)
                //WriteFile("Sound/snd0.vbx", BitConverter.GetBytes(vboffset));
                WriteFile("block.blk", rdt1b.BLK);
                WriteFile("camera.rid", rdt1b.RID);
                WriteFile("collision.sca", rdt1b.SCA);
                WriteFile("floor.flr", rdt1b.FLR);
                //WriteFile("floor.flt", rdt1b.FLRTerminator == null ? [] : BitConverter.GetBytes(rdt1b.FLRTerminator.Value));
                WriteFile("light.lit", rdt1b.LIT);
                //WriteFile("scroll.tim", rdt1b.TIMSCROLL.Data);
                WriteFile("sprite.pri", rdt1b.PRI);
                WriteFile("zone.rvd", rdt1b.RVD);
                //Print success message
                MessageBox.Show($"{version} RDT unpacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else try
                {
                    var rdt2 = new Rdt2(version, inputPath);
                    var rdt2b = rdt2.ToBuilder();
                    WriteHeader(rdt2b.Header);
                    WriteFile("Animation/anim.rbj", rdt2b.RBJ.Data);
                    WriteEffs(rdt2b.EmbeddedEffects);
                    WriteFile("Effect/effect.etd", rdt2b.ETD.Data);
                    WriteFile("Effect/effect.tbl", rdt2b.EspTable.Data);
                    WriteMessages(rdt2b.MSGJA, rdt2b.MSGEN);
                    WriteObjects(rdt2b);
                    WriteScds(rdt2b.SCDINIT, rdt2b.SCDMAIN);
                    WriteFile("Sound/snd0.edt", rdt2b.EDT);
                    WriteFile("Sound/snd0.vh", rdt2b.VH);
                    WriteFile("Sound/snd0.vb", rdt2b.VB);
                    if (rdt2b.VBOFFSET is int vboffset)
                        WriteFile("Sound/snd0.vbx", BitConverter.GetBytes(vboffset));
                    WriteFile("block.blk", rdt2b.BLK);
                    WriteFile("camera.rid", rdt2b.RID.Data);
                    WriteFile("collision.sca", rdt2b.SCA);
                    WriteFile("floor.flr", rdt2b.FLR);
                    WriteFile("floor.flt", rdt2b.FLRTerminator == null ? [] : BitConverter.GetBytes(rdt2b.FLRTerminator.Value));
                    WriteFile("light.lit", rdt2b.LIT);
                    WriteFile("scroll.tim", rdt2b.TIMSCROLL.Data);
                    WriteFile("sprite.pri", rdt2b.PRI);
                    WriteFile("zone.rvd", rdt2b.RVD);
                    //Print success message
                    MessageBox.Show($"{version} RDT unpacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                //"You look like you've got somethin to say?" - Output any errors through a dialogue box
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
        }

        private void WriteHeader(Rdt2.Rdt2Header header)
        {
            using var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(header);
            var data = ms.ToArray();
            WriteFile(Path.ChangeExtension(FileName, ".hdr"), data);
        }

        private void WriteHeaderRdt1(Rdt1.Rdt1Header header)
        {
            using var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(header);
            var data = ms.ToArray();
            WriteFile(Path.ChangeExtension(FileName, ".hdr"), data);
        }

        private void WriteEffs(EmbeddedEffectList effects)
        {
            for (var i = 0; i < effects.Count; i++)
            {
                var id = effects[i].Id;
                WriteFile($"Effect/esp{id:X2}.eff", effects[i].Eff.Data);
                WriteFile($"Effect/esp{id:X2}.tim", effects[i].Tim.Data);
            }
        }

        //For RE1/BIO1
        private void WriteMessage(MsgList msglist)
        {
            for (var i = 0; i < msglist.Count; i++)
            {
                WriteFile($"Message/sub{i:00}.msg", msglist[i].Data);
            }
        }

        //For multilingual RDTS (RE2/3)
        private void WriteMessages(MsgList ja, MsgList en)
        {
            for (var i = 0; i < ja.Count; i++)
            {
                WriteFile($"Message/main{i:00}.msg", ja[i].Data);
            }
            for (var i = 0; i < en.Count; i++)
            {
                WriteFile($"Message/sub{i:00}.msg", en[i].Data);
            }
        }

        private void WriteObjects(Rdt2.Builder rdt)
        {
            var table = rdt.EmbeddedObjectModelTable;
            for (var i = 0; i < table.Count; i++)
            {
                var modelIndex = table[i].Model;
                var textureIndex = table[i].Texture;
                if (modelIndex != -1)
                {
                    var md1 = rdt.EmbeddedObjectMd1[modelIndex];
                    WriteFile($"Object/object{i:00}.md1", md1.Data);
                }
                if (textureIndex != -1)
                {
                    var tim = rdt.EmbeddedObjectTim[textureIndex];
                    WriteFile($"Object/object{i:00}.tim", tim.Data);
                }
            }
        }

        private void WriteScds(ScdProcedureList init, ScdProcedureList main)
        {
            for (var i = 0; i < init.Count; i++)
            {
                WriteFile($"Script/main{i:00}.scd", init[i].Data.Span);
            }
            for (var i = 0; i < main.Count; i++)
            {
                WriteFile($"Script/sub{i:00}.scd", main[i].Data.Span);
            }
        }

        private void WriteFileExt(string extension, ReadOnlyMemory<byte> data) => WriteFile(extension, data.Span);
        private void WriteFileExt(string extension, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return;

            WriteFile(Path.ChangeExtension(FileName, extension), data);
        }

        private void WriteFile(string relativePath, byte[] data) => WriteFile(relativePath, new ReadOnlySpan<byte>(data));
        private void WriteFile(string relativePath, ReadOnlyMemory<byte> data) => WriteFile(relativePath, data.Span);
        private void WriteFile(string relativePath, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return;

            var destPath = Path.Combine(BasePath, relativePath);
            var dir = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(destPath, data.ToArray());
        }
    }
}
