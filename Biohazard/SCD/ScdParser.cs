using IntelOrca.Biohazard.Script.Opcodes;
using Tool_Hazard.Biohazard.SCD;
using Tool_Hazard.Biohazard.SCD.Opcodes;

public class ScdParser
{
    public static List<ScdInstruction> Parse(Stream stream)
    {
        var list = new List<ScdInstruction>();
        using var br = new BinaryReader(stream);

        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            byte opcode = br.ReadByte();
            var def = OpcodeDatabase.Get(opcode);

            if (def == null)
                //throw new Exception($"Unknown opcode {opcode:X2}");
                MessageBox.Show(
                    $"Warning: Unknown opcode {opcode:X2}",
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

            var inst = new ScdInstruction
            {
                Opcode = opcode,
                Definition = def
            };

            foreach (var kv in def.Bytes)
            {
                if (kv.Key == "Opcode") continue;
                inst.Parameters[kv.Key] =
                    OpcodeBinaryReader.Read(br, kv.Value.Type);
            }

            list.Add(inst);
        }

        return list;
    }
}
