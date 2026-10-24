using Tool_Hazard.Biohazard.SCD.Opcodes;

namespace Tool_Hazard.Biohazard.SCD
{
    public static class ScdCompiler_2
    {
        public static void Write(string path, List<ScdInstruction> instructions) =>
            File.WriteAllBytes(path, ToBytes(instructions));

        public static byte[] ToBytes(IEnumerable<ScdInstruction> instructions)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            foreach (var inst in instructions)
            {
                var start = ms.Position;
                if (inst.RawBytes is not null)
                {
                    bw.Write(inst.RawBytes);
                    continue;
                }

                bw.Write(inst.Opcode);
                foreach (var field in inst.Definition.Bytes ?? new Dictionary<string, OpcodeField>())
                {
                    if (field.Key == "Opcode")
                        continue;

                    if (!inst.Parameters.TryGetValue(field.Key, out var value))
                        throw new InvalidDataException(
                            $"Missing parameter '{field.Key}' for opcode {inst.Opcode:X2}.");

                    OpcodeBinaryWriter.Write(bw, field.Value.Type!, value);
                }

                var written = checked((int)(ms.Position - start));
                if (inst.Definition.BinaryLength > 0 && written != inst.Definition.BinaryLength)
                {
                    throw new InvalidDataException(
                        $"Opcode {inst.Opcode:X2} wrote {written} bytes; expected {inst.Definition.BinaryLength}.");
                }
            }

            return ms.ToArray();
        }
    }
}
