namespace IntelOrca.Biohazard.Script.Compilation
{
    public interface IScdGenerator
    {
        ErrorList Errors { get; }
        IRdtEditOperation[] Operations { get; }

        int Generate(IFileIncluder includer, string path);
    }
}
