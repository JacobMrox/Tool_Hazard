namespace IntelOrca.Biohazard.Script.Opcodes
{
    public interface IAot
    {
        byte Id { get; set; }
        byte SCE { get; set; }
        byte SAT { get; set; }
        byte Floor { get; set; }
        byte Super { get; set; }
    }
}
