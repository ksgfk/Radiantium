using System.Numerics;

namespace Radiantium.Offline
{
    //***************************
    //* System.Random Extension *
    //***************************
    public static class Probability
    {
        public static float NextF(this Random rand)
        {
            return (float)rand.NextDouble();
        }

        public static Vector2 Next2F(this Random rand)
        {
            return new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());
        }
    }
}
