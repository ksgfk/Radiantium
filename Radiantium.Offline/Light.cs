using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public enum LightType
    {
        DeltaPosition,
        DeltaDirection,
        Area,
        Infinite
    }

    public struct LightEvalParam
    {
        public Vector3 P;
        public float T;

        public LightEvalParam(Vector3 p, float t)
        {
            P = p;
            T = t;
        }

        public static implicit operator LightEvalParam(Intersection inct)
        {
            return new LightEvalParam(inct.P, inct.T);
        }
    }

    public struct LightSampleResult
    {
        public Vector3 P;
        public Color3F Li;
        public Vector3 Wi;
        public float Pdf;

        public LightSampleResult(Vector3 p, Color3F li, Vector3 wi, float pdf)
        {
            P = p;
            Li = li;
            Wi = wi;
            Pdf = pdf;
        }

        public void Deconstruct(out Vector3 p, out Vector3 wi, out float pdf, out Color3F li)
        {
            p = P;
            wi = Wi;
            pdf = Pdf;
            li = Li;
        }
    }

    public abstract class Light
    {
        public abstract Color3F Power { get; }

        public abstract LightType Type { get; }

        public bool IsDelta => (Type & LightType.DeltaPosition) != 0 || (Type & LightType.DeltaDirection) != 0;

        public abstract LightSampleResult SampleLi(LightEvalParam inct, Random rand);

        public abstract float PdfLi(LightEvalParam inct, Vector3 wi);

        public virtual Color3F Le(Ray3F ray)
        {
            return new Color3F(0.0f);
        }
    }

    public abstract class AreaLight : Light
    {
        public sealed override LightType Type => LightType.Area;

        public abstract Color3F L(Intersection inct, Vector3 w);
    }

    public abstract class InfiniteLight : Light
    {
        public float WorldRadius { get; private set; }
        public Vector3 WorldCenter { get; private set; }
        public sealed override LightType Type => LightType.Infinite;

        public virtual void Preprocess(Scene scene)
        {
            BoundingBox3F bound = scene.Aggregate.WorldBound;
            WorldCenter = bound.Center;
            float length = bound.Diagonal.Length();
            WorldRadius = length / 2.0f;
        }
    }
}
