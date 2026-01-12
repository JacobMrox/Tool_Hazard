using System.Diagnostics;

namespace IntelOrca.Biohazard.Script.Compilation
{
    [DebuggerDisplay("{Path}({Line},{Column}): error {ErrorCodeString}: {Message}")]
    public readonly struct Error
    {
        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
        public ErrorKind Kind { get; }
        public int Code { get; }
        public string Message { get; }

        public string ErrorCodeString => $"SCD{Code:0000}";

        public Error(string path, int line, int column, ErrorKind kind, int code, string message)
        {
            Path = path;
            Line = line;
            Column = column;
            Kind = kind;
            Code = code;
            Message = message;
        }

        public override string ToString()
        {
            return $"{Path}({Line + 1},{Column + 1}): {Kind.ToString().ToLower()} {ErrorCodeString}: {Message}";
        }
    }
}
