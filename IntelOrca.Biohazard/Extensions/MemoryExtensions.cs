using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Extensions
{
    public static class MemoryExtensions
    {
        public static List<T> ToList<T>(this ReadOnlyMemory<T> data)
        {
            var span = data.Span;
            var list = new List<T>(span.Length);
            for (var i = 0; i < span.Length; i++)
            {
                list.Add(span[i]);
            }
            return list;
        }

        public static void WriteToFile(this ReadOnlyMemory<byte> data, string path) => WriteToFile(data.Span, path);
        public static void WriteToFile(this ReadOnlySpan<byte> span, string path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            var buffer = new byte[4096];
            for (var i = 0; i < span.Length; i += buffer.Length)
            {
                var left = span.Length - i;
                var view = span.Slice(i, Math.Min(left, buffer.Length));
                view.CopyTo(buffer);
                fs.Write(buffer, 0, view.Length);
            }
        }

        public unsafe static ReadOnlySpan<T> TruncateStartBy<T>(this ReadOnlySpan<T> span, int count) where T : struct
        {
            if (count < 0)
                return span.Slice(span.Length + count, -count);
            return span.Slice(count, span.Length - count);
        }

        public unsafe static ReadOnlySpan<T> TruncateBy<T>(this ReadOnlySpan<T> span, int count) where T : struct
        {
            return span.Slice(0, span.Length - count);
        }

        public unsafe static ReadOnlySpan<T> GetSafeSpan<T>(this ReadOnlyMemory<byte> memory, int offset, int count) where T : struct
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            var requiredLength = count * sizeof(T);
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            ReadOnlySpan<byte> span;
            if (memory.Length <= offset)
            {
                span = new byte[requiredLength];
            }
            else if (memory.Length - offset < requiredLength)
            {
                var copyLength = memory.Length - offset;
                var fix = new byte[requiredLength];
                for (var i = 0; i < copyLength; i++)
                {
                    fix[i] = memory.Span[offset + i];
                }
                span = fix;
            }
            else
            {
                span = memory.Span.Slice(offset, requiredLength);
            }
            return MemoryMarshal.Cast<byte, T>(span).Slice(0, count);
        }
    }
}
