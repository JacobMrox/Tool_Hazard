using IntelOrca.Biohazard.Model;

namespace Tool_Hazard.IntelOrca.Biohazard
{
    public sealed class RdtModel
    {
        public Md1 Mesh { get; }
        public TimFile Texture { get; }

        public RdtModel(Md1 mesh, TimFile texture)
        {
            Mesh = mesh;
            Texture = texture;
        }
    }
}
