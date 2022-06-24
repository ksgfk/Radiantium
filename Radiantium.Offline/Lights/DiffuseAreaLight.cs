﻿using Radiantium.Core;
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

        public override LightEmitResult SampleEmit(Random rand)
        {
            ShapeSurfacePoint samplePoint = Shape.Sample(rand, out float pdfPos);
            Vector3 localW = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            float pdfDir = Probability.SquareToCosineHemispherePdf(localW);
            Vector3 w = samplePoint.Shading.ToWorld(localW);
            return new LightEmitResult(samplePoint.P, Normalize(w), samplePoint.N, samplePoint.UV, Lemit, new LightEmitPdf(pdfPos, pdfDir));
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
