using Radiantium.Core;
using System.Numerics;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Lights
{
    public class DiffuseAreaLight : AreaLight
    {
        private readonly float _surfaceArea;

        public Color3F Lemit { get; }
        public Shape Shape { get; }
        public override Color3F Power => Lemit * _surfaceArea * PI;

        public DiffuseAreaLight(Shape shape, Color3F lemit)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Lemit = lemit;
            _surfaceArea = Shape.SurfaceArea;
        }

        public override Color3F L(Intersection inct, Vector3 w)
        {
            return (Dot(inct.N, w) > 0) ? Lemit : new Color3F(0.0f);
        }

        public override float PdfLi(LightEvalParam inct, Vector3 wi)
        {
            Ray3F ray = new Ray3F(inct.P, wi);
            if (!Shape.Intersect(ray, out SurfacePoint point))
            {
                return 0.0f;
            }
            ShapeIntersection hit = Shape.GetIntersection(ray, point);
            float pdf = (inct.P - hit.P).LengthSquared() / (MathExt.AbsDot(hit.N, -wi) * Shape.SurfaceArea);
            if (float.IsInfinity(pdf))
            {
                pdf = 0.0f;
            }
            return pdf;
        }

        public override LightSampleResult SampleLi(LightEvalParam inct, Random rand)
        {
            ShapeSurfacePoint shape = Shape.Sample(rand, out float shapePdf);
            Vector3 wi = shape.P - inct.P;
            Color3F li = (Dot(shape.N, -wi) > 0) ? Lemit : new Color3F(0.0f);
            float pdf;
            if (wi.LengthSquared() == 0)
            {
                pdf = 0.0f;
            }
            else
            {
                wi = Normalize(wi);
                pdf = shapePdf * (inct.P - shape.P).LengthSquared() / MathExt.AbsDot(shape.N, -wi);
                if (float.IsInfinity(pdf))
                {
                    pdf = 0.0f;
                }
            }
            return new LightSampleResult(shape.P, li, wi, pdf);
        }
    }
}
