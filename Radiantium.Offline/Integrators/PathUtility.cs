namespace Radiantium.Offline.Integrators
{
    public static class PathUtility
    {
        public static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
        {
            float f = nf * fPdf, g = ng * gPdf;
            return (f * f) / (f * f + g * g);
        }
    }
}