using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.Extensions
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
