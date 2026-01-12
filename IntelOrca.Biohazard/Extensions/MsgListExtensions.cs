using System;
using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.Extensions
{
    public static class MsgListExtensions
    {
        public static MsgList WithMessage(this MsgList list, int index, Msg value)
        {
            if (value.Version != list.Version)
                throw new ArgumentException("Version mismatch.", nameof(value));
            if (value.Language != list.Language)
                throw new ArgumentException("Language mismatch.", nameof(value));

            var builder = list.ToBuilder();
            while (builder.Messages.Count <= index)
            {
                builder.Messages.Add(new Msg(list.Version, list.Language, ""));
            }
            builder.Messages[index] = value;
            return builder.ToMsgList();
        }
    }
}
