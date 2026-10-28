using Tool_Hazard.IntelOrca.Biohazard.Room;

namespace Tool_Hazard.IntelOrca.Biohazard.Extensions
{
    public static class RbjExtensions
    {
        public static Rbj WithAnimation(this Rbj rbj, int index, RbjAnimation value)
        {
            var builder = rbj.ToBuilder();
            while (builder.Animations.Count <= index)
            {
                builder.Animations.Add(new RbjAnimation());
            }
            builder.Animations[index] = value;
            return builder.ToRbj();
        }
    }
}
