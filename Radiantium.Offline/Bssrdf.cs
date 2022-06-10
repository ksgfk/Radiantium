using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Offline.Coordinate;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline
{
    public struct SampleBssrdfResult
    {
        public Vector3 P;
        public Vector3 W;
        public Coordinate Coord;
        public Color3F S;
        public float Pdf;

        public SampleBssrdfResult(Vector3 p, Vector3 w, Coordinate coord, Color3F s, float pdf)
        {
            P = p;
            W = w;
            Coord = coord;
            S = s;
            Pdf = pdf;
        }
    }

    public interface IBssrdf
    {
        Color3F S(Vector3 po, Vector3 wo, Coordinate co, Vector3 pi, Vector3 wi, Coordinate ci);

        SampleBssrdfResult SampleS(Vector3 po, Vector3 wo, Coordinate co, Material mo, Scene scene, Random rand);
    }

    public interface ISeparableBssrdf : IBssrdf
    {
        Color3F Sr(float distance);

        float SampleSr(int channel, float u);

        float PdfSr(int channel, float r);

        public Color3F Sw(float eta, Vector3 w)
        {
            return Bssrdf.SeparableSw(eta, w);
        }

        public Color3F Sp(Vector3 po, Vector3 pi)
        {
            return Sr(Distance(po, pi));
        }

        public float PdfSp(Vector3 po, Coordinate co, Vector3 pi, Coordinate ci)
        {
            Vector3 d = po - pi;
            Vector3 dLocal = co.ToLocal(d);
            Vector3 nLocal = co.ToLocal(ci.Z);
            Vector3 rProj = new Vector3(
                Sqrt(Sqr(dLocal.Y) + Sqr(dLocal.Z)),
                Sqrt(Sqr(dLocal.Z) + Sqr(dLocal.X)),
                Sqrt(Sqr(dLocal.X) + Sqr(dLocal.Y)));
            Vector3 axisProb = new Vector3(0.25f, 0.25f, 0.5f);
            float pdf = 0.0f;
            const float chProb = 1.0f / 3.0f;
            for (int axis = 0; axis < 3; axis++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    pdf += PdfSr(channel, IndexerUnsafe(ref rProj, axis)) *
                        Abs(IndexerUnsafe(ref nLocal, axis)) *
                        chProb *
                        IndexerUnsafe(ref axisProb, axis);
                }
            }
            return pdf;
        }

        public SampleBssrdfResult SampleSp(Vector3 po, Coordinate co, Material mo, Scene scene, Random rand)
        {
            Coordinate coord;
            float u1 = rand.NextFloat();
            if (u1 < 0.5f)
            {
                coord = co; u1 *= 2;
            }
            else if (u1 < 0.75f)
            {
                coord = new Coordinate(co.Y, co.Z, co.X); u1 = (u1 - 0.5f) * 4;
            }
            else
            {
                coord = new Coordinate(co.Z, co.X, co.Y); u1 = (u1 - 0.75f) * 4;
            }
            int channel = Math.Clamp((int)(u1 * 3), 0, 2);
            u1 = u1 * 3 - channel;
            float r = SampleSr(channel, rand.NextFloat());
            if (r < 0) { return new SampleBssrdfResult(); }
            float phi = 2 * PI * rand.NextFloat();
            float rMax = SampleSr(channel, 0.999f);
            if (r >= rMax) { return new SampleBssrdfResult(); }
            float l = 2 * Sqrt(Sqr(rMax) - Sqr(r));
            Ray3F ray = new Ray3F();
            var (sinPhi, cosPhi) = SinCos(phi);
            ray.O = po + r * (coord.X * cosPhi) + coord.Y * sinPhi - l * coord.Z * 0.5f;
            Vector3 target = ray.O + l * coord.Z;
            ray.D = Normalize(target - ray.O);
            ray.MinT = 0.0001f;
            ray.MaxT = l;
            //I don't know if the CLR has escape analysis
            //So we cache the List per thread to avoid GC
            List<Intersection> hitList = Bssrdf.GetIntersectChainCache();
            while (true)
            {
                if (ray.MinT >= ray.MaxT) { break; }
                if (!scene.Intersect(ray, out Intersection inct)) { break; }
                if (inct.HasSurface && inct.Surface == mo)
                {
                    hitList.Add(inct);
                }
            }
            if (hitList.Count == 0) { return new SampleBssrdfResult(); }
            int selected = Math.Clamp((int)(u1 * hitList.Count), 0, hitList.Count - 1);
            Vector3 pi = hitList[selected].P;
            Vector3 wi = -ray.D;
            Coordinate ci = hitList[selected].Shading;
            Color3F s = Sp(po, hitList[selected].P);
            float pdf = PdfSp(po, co, pi, ci);
            return new SampleBssrdfResult(pi, wi, ci, s, pdf);
        }
    }

    public static class Bssrdf
    {
        private static readonly ThreadLocal<List<Intersection>> _threadInctChainCache;

        static Bssrdf()
        {
            _threadInctChainCache = new ThreadLocal<List<Intersection>>(() => new List<Intersection>());
        }

        public static List<Intersection> GetIntersectChainCache() { return _threadInctChainCache.Value!; }

        public static float FresnelMoment1(float eta)
        {
            float eta2 = eta * eta;
            float eta3 = eta2 * eta;
            float eta4 = eta3 * eta;
            float eta5 = eta4 * eta;
            if (eta < 1)
            {
                return 0.45966f -
                    1.73965f * eta +
                    3.37668f * eta2 -
                    3.904945f * eta3 +
                   2.49277f * eta4 -
                   0.68441f * eta5;
            }
            else
            {
                return -4.61686f +
                    11.1136f * eta -
                    10.4646f * eta2 +
                    5.11455f * eta3 -
                    1.27198f * eta4 +
                    0.12746f * eta5;
            }
        }

        public static Color3F Sp<T>(this T bssrdf, Vector3 po, Vector3 pi) where T : ISeparableBssrdf
        {
            return bssrdf.Sp(po, pi);
        }

        public static Color3F SeparableSw(float eta, Vector3 w)
        {
            float c = 1 - 2 * FresnelMoment1(1 / eta);
            return new Color3F((1 - Fresnel.DielectricFunc(CosTheta(w), 1, eta)) / (c * PI));
        }

        public static SampleBssrdfResult SeparableSampleS<T>(Vector3 po, Coordinate co, Material mo, Scene scene, Random rand, T bssrdf) where T : ISeparableBssrdf
        {
            SampleBssrdfResult result = bssrdf.SampleSp(po, co, mo, scene, rand);
            if (result.S != Color3F.Black)
            {
                result.W = result.Coord.Z;
            }
            return result;
        }
    }

    public class SeparableBssrdfAdapter : Material
    {
        public Texture2D Eta { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public SeparableBssrdfAdapter(Texture2D eta)
        {
            Eta = eta ?? throw new ArgumentNullException(nameof(eta));
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            float eta = Eta.Sample(inct.UV).R;
            Color3F f = Bssrdf.SeparableSw(eta, wi);
            f *= Sqr(eta);
            return f;
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return SameHemisphere(wo, wi) ? AbsCosTheta(wi) / PI : 0.0F;
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            Vector3 wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0)
            {
                wi.Z *= -1;
            }
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Pdf(wo, wi, inct);
            Color3F fr = Fr(wo, wi, inct);
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }
}
