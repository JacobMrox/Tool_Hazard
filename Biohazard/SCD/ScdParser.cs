using Tool_Hazard.Biohazard.SCD;
using Tool_Hazard.Biohazard.SCD.Opcodes;
using Tool_Hazard.IntelOrca.Biohazard;

public static class ScdParser
{
    public static List<ScdInstruction> Parse(Stream stream) =>
        Parse(stream, OpcodeDatabase.LoadedVersion);

    public static List<ScdInstruction> Parse(Stream stream, BioVersion version)
    {
        var list = new List<ScdInstruction>();
        using var br = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            var offset = checked((int)br.BaseStream.Position);
            var remaining = checked((int)(br.BaseStream.Length - br.BaseStream.Position));
            var opcode = br.ReadByte();
            var length = OpcodeDatabase.GetInstructionSize(version, opcode, br);
            br.BaseStream.Position = offset;

            if (length <= 0 || length > remaining)
            {
                // Retail rooms can contain alignment or unreachable bytes after an
                // event terminator. Preserve them exactly instead of guessing at
                // instruction boundaries and desynchronising the rest of the file.
                var trailing = br.ReadBytes(remaining);
                list.Add(CreateRawInstruction(offset, trailing));
                break;
            }

            var bytes = br.ReadBytes(length);
            var definition = OpcodeDatabase.Get(opcode);
            if (definition is null || definition.BinaryLength != length)
                definition = OpcodeDatabase.CreateRawDefinition(opcode, length, definition);

            var instruction = new ScdInstruction
            {
                Offset = offset,
                Opcode = opcode,
                Definition = definition
            };

            if (definition.IsRawLayout)
            {
                instruction.RawBytes = bytes;
            }
            else
            {
                using var instructionStream = new MemoryStream(bytes, writable: false);
                using var instructionReader = new BinaryReader(instructionStream);
                instructionReader.ReadByte();
                foreach (var field in definition.Bytes ?? new Dictionary<string, OpcodeField>())
                {
                    if (field.Key == "Opcode")
                        continue;
                    instruction.Parameters[field.Key] =
                        OpcodeBinaryReader.Read(instructionReader, field.Value.Type!);
                }
            }

            list.Add(instruction);
        }

        return list;
    }

    private static ScdInstruction CreateRawInstruction(int offset, byte[] bytes)
    {
        var opcode = bytes.Length == 0 ? (byte)0 : bytes[0];
        return new ScdInstruction
        {
            Offset = offset,
            Opcode = opcode,
            Definition = OpcodeDatabase.CreateRawDefinition(opcode, bytes.Length),
            RawBytes = bytes
        };
    }
}
