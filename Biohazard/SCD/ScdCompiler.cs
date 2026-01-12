using Tool_Hazard.Biohazard.SCD.Opcodes;

namespace Tool_Hazard.Biohazard.SCD
{
    public static class ScdCompiler_2
    {
        public static void Write(string path, List<ScdInstruction> instructions)
        {
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);

            foreach (var inst in instructions)
            {
                bw.Write(inst.Opcode);

                // Write parameters in the same order as JSON "Bytes" fields (skipping "Opcode")
                //foreach (var kv in inst.Definition.Bytes)
                foreach (var kv in inst.Definition.Bytes ?? new Dictionary<string, OpcodeField>())
                {
                    if (kv.Key == "Opcode")
                        continue;

                    if (!inst.Parameters.TryGetValue(kv.Key, out var value))
                        throw new Exception($"Missing parameter '{kv.Key}' for opcode {inst.Opcode:X2}");

                    OpcodeBinaryWriter.Write(bw, kv.Value.Type, value!);
                }
            }
        }
    }
}
