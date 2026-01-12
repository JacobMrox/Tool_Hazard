using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Extensions
{
    public static class EmrExtensions
    {
        public static Emr Scale(this Emr emr, double y) => Scale(emr, 1, y, 1);

        public static Emr Scale(this Emr emr, double x, double y, double z)
        {
            if (x == 1 && y == 1 && z == 1)
                return emr;

            var emrBuilder = emr.ToBuilder();
            for (var i = 0; i < emrBuilder.KeyFrames.Count; i++)
            {
                var keyFrame = emrBuilder.KeyFrames[i];
                var offset = keyFrame.Offset;
                offset.x = (short)(offset.x * x);
                offset.y = (short)(offset.y * y);
                offset.z = (short)(offset.z * z);
                keyFrame.Offset = offset;
            }
            return emrBuilder.ToEmr();
        }
    }
}
