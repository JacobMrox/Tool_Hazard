using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct MsgList
    {
        public BioVersion Version { get; }
        public MsgLanguage Language { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public MsgList(BioVersion version, MsgLanguage language, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Language = language;
            Data = data;
        }

        public int Count
        {
            get
            {
                var firstOffset = Data.GetSafeSpan<ushort>(0, 1)[0];
                var numOffsets = firstOffset / 2;
                return numOffsets;
            }
        }

        public Msg this[int index]
        {
            get
            {
                var count = Count;
                var offset = Data.GetSafeSpan<ushort>(0, count)[index];
                var nextOffset = index == count - 1 ?
                    Data.Length :
                    Data.GetSafeSpan<ushort>(0, count)[index + 1];
                return new Msg(Version, Language, Data.Slice(offset, nextOffset - offset));
            }
        }

        public Builder ToBuilder()
        {
            var builder = new Builder();
            var count = Count;
            for (var i = 0; i < count; i++)
            {
                builder.Messages.Add(this[i]);
            }
            return builder;
        }

        public class Builder
        {
            public List<Msg> Messages { get; } = new List<Msg>();

            public MsgList ToMsgList()
            {
                if (Messages.Count == 0)
                {
                    return new MsgList();
                }

                var version = Messages[0].Version;
                var language = Messages[0].Language;
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                var baseOffset = Messages.Count * 2;
                for (var i = 0; i < Messages.Count; i++)
                {
                    var msg = Messages[i];
                    if (msg.Version != version)
                        throw new InvalidOperationException("Not all messages share the same version.");
                    if (msg.Language != language)
                        throw new InvalidOperationException("Not all messages share the same language.");

                    bw.Write((ushort)baseOffset);
                    baseOffset += msg.Data.Length;
                }
                for (var i = 0; i < Messages.Count; i++)
                {
                    bw.Write(Messages[i].Data);
                }
                var bytes = ms.ToArray();
                return new MsgList(version, language, bytes);
            }
        }
    }
}
