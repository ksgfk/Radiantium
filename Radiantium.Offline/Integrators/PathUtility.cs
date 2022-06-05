namespace Radiantium.Offline.Integrators
{
    public static class PathUtility
    {
        public static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
        {
            double f = nf * fPdf, g = ng * gPdf;
            return (float)((f * f) / (f * f + g * g));
        }
    }
}
