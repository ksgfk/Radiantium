using Radiantium.Core;
using System.Numerics;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Lights
{
    //最简单的面光源
    public class DiffuseAreaLight : AreaLight
    {
        readonly float _surfaceArea;

        public Color3F LightEmit { get; }
        public Shape Shape { get; }
        public override Color3F Power => LightEmit * _surfaceArea * PI;

        public DiffuseAreaLight(Shape shape, Color3F lemit)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            LightEmit = lemit;
            _surfaceArea = Shape.SurfaceArea;
        }

        public override Color3F L(Intersection inct, Vector3 w)
        {
            return (Dot(inct.N, w) > 0) ? LightEmit : new Color3F(0.0f); //只有正面发光
        }

        public override float PdfLi(LightEvalParam inct, Vector3 wi)
        {
            Ray3F ray = new Ray3F(inct.P, wi);
            if (!Shape.Intersect(ray, out SurfacePoint point))
            {
                return 0.0f;
            }
            ShapeIntersection hit = Shape.GetIntersection(ray, point);
            //将pdf从面积转化为立体角
            float pdf = (inct.P - hit.P).LengthSquared() / (MathExt.AbsDot(hit.N, -wi) * Shape.SurfaceArea);
            if (float.IsInfinity(pdf))
            {
                pdf = 0.0f;
            }
            return pdf;
        }

        public override SampleLightResult SampleLi(LightEvalParam inct, Random rand)
        {
            ShapeSurfacePoint shape = Shape.Sample(rand, out float shapePdf); //在形状上采样一个点, 作为发光点
            Vector3 wi = shape.P - inct.P;
            Color3F li = (Dot(shape.N, -wi) > 0) ? LightEmit : new Color3F(0.0f);
            float pdf;
            if (wi.LengthSquared() == 0) //它们距离实在太近了, 应该是基本不可能出现的情况
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
            return new SampleLightResult(shape.P, li, wi, pdf);
        }

        public override LightEmitResult SampleEmit(Random rand)
        {
            ShapeSurfacePoint samplePoint = Shape.Sample(rand, out float pdfPos);
            Vector3 localW = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2())); //采样一个方向作为发光方向
            float pdfDir = Probability.SquareToCosineHemispherePdf(localW);
            Vector3 w = samplePoint.Shading.ToWorld(localW);
            return new LightEmitResult(samplePoint.P, Normalize(w), samplePoint.N, samplePoint.UV, LightEmit, new LightEmitPdf(pdfPos, pdfDir));
        }

        public override LightEmitPdf EmitPdf(Vector3 pos, Vector3 dir, Vector3 normal)
        {
            Coordinate coord = new Coordinate(normal);
            ShapeSurfacePoint point = new ShapeSurfacePoint(pos, new Vector2(), coord);
            float pdfPos = Shape.Pdf(point);
            Vector3 localDir = coord.ToLocal(dir);
            float pdfDir = Probability.SquareToCosineHemispherePdf(localDir);
            return new LightEmitPdf(pdfPos, Coordinate.CosTheta(localDir) < 0 ? 0 : pdfDir);
        }
    }
}
