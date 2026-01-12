using System;
using System.Linq;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct EmbeddedEffect
    {
        public byte Id { get; }
        public Eff Eff { get; }
        public Tim Tim { get; }

        public EmbeddedEffect(byte id, Eff eff, Tim tim)
        {
            Id = id;
            Eff = eff;
            Tim = tim;
        }
    }

    public readonly struct EmbeddedEffectList
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<EmbeddedEffect> Effects { get; }

        public EmbeddedEffectList(BioVersion version, ReadOnlyMemory<EmbeddedEffect> effects)
        {
            Version = version;
            Effects = effects;
        }

        public int Count => Effects.Length;

        public EmbeddedEffect this[int index] => Effects.Span[index];

        public ReadOnlySpan<byte> ESPID
        {
            get
            {
                var effects = Effects.Span;
                var ids = new byte[Version == BioVersion.Biohazard3 ? 16 : 8];
                for (var i = 0; i < ids.Length; i++)
                {
                    ids[i] = (byte)(effects.Length > i ? effects[i].Id : 0xFF);
                }
                return ids;
            }
        }

        public byte[] Ids
        {
            get
            {
                var result = new byte[Count];
                for (var i = 0; i < Count; i++)
                {
                    result[i] = this[i].Id;
                }
                return result;
            }
        }

        public bool Contains(byte id)
        {
            for (var i = 0; i < Count; i++)
            {
                if (this[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString() => string.Join("-", Ids.Select(x => x.ToString("X2")));
    }
}
