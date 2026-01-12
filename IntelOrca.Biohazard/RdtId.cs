using System;
using System.Globalization;

namespace IntelOrca.Biohazard
{
    public readonly struct RdtId : IEquatable<RdtId>, IComparable<RdtId>
    {
        public int Stage { get; }
        public int Room { get; }
        public int? Variant { get; }

        public RdtId(int stage, int room) : this(stage, room, null)
        {
        }

        public RdtId(int stage, int room, int? variant) : this()
        {
            Stage = stage;
            Room = room;
            Variant = variant;
        }

        public override bool Equals(object? obj) => obj is RdtId id && Equals(id);
        public bool Equals(RdtId other) =>
            Stage == other.Stage &&
            Room == other.Room &&
            Variant == other.Variant;

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Stage;
            hash = hash * 23 + Room;
            hash = hash * 23 + Variant ?? 0;
            return hash;
        }

        public int CompareTo(RdtId other)
        {
            if (Stage < other.Stage)
                return -1;
            if (Stage > other.Stage)
                return 1;
            if (Room != other.Room)
                return Room - other.Room;
            return (Variant ?? 0) - (other.Variant ?? 0);
        }

        public static bool operator ==(RdtId left, RdtId right) => left.Equals(right);
        public static bool operator !=(RdtId left, RdtId right) => !(left == right);

        public static RdtId FromInteger(int value)
        {
            return new RdtId((value >> 8) - 1, value & 0xFF);
        }

        public static RdtId Parse(string s)
        {
            if (!TryParse(s, out var id))
                throw new FormatException("Failed to parse RDT ID.");
            return id;
        }

        public static bool TryParse(string s, out RdtId id)
        {
            id = default(RdtId);
            if (s.Length < 2 || s.Length > 4)
                return false;

            var stage = ParseHex(s[0]);
            if (stage == null)
                return false;

            if (!int.TryParse(s.Substring(1, 2), NumberStyles.HexNumber, null, out var room))
                return false;

            if (s.Length == 4)
            {
                var variant = ParseHex(s[3]);
                id = new RdtId(stage.Value - 1, room, variant);
            }
            else
            {
                id = new RdtId(stage.Value - 1, room);
            }
            return true;
        }

        private static int? ParseHex(char c)
        {
            c = char.ToUpper(c);
            if (c >= '0' && c <= '9')
                return c - '0';
            else if (c >= 'A' && c <= 'G')
                return 10 + (c - 'A');
            else
                return null;
        }

        public override string ToString()
        {
            var var = Variant == null ? "" : Variant.Value.ToString("X");
            if (Stage == 15)
                return $"G{Room:X2}{var}";
            return $"{Stage + 1:X}{Room:X2}{var}";
        }
    }
}
