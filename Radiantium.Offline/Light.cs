using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    [Flags]
    public enum LightType
    {
        DeltaPosition = 0b0001,
        DeltaDirection = 0b0010,
        Area = 0b0100,
        Infinite = 0b1000
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

        public static implicit operator LightEvalParam(SampleMediumResult msr)
        {
            return new LightEvalParam(msr.P, msr.T);
        }
    }

    public struct SampleLightResult
    {
        public Vector3 P;
        public Color3F Li;
        public Vector3 Wi;
        public float Pdf;

        public SampleLightResult(Vector3 p, Color3F li, Vector3 wi, float pdf)
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

    public struct LightEmitResult
    {
        public Vector3 Pos;
        public Vector3 Dir;
        public Vector3 Normal;
        public Vector2 UV;
        public Color3F Radiance;
        public LightEmitPdf Pdf;

        public LightEmitResult(Vector3 pos, Vector3 dir, Vector3 normal, Vector2 uv, Color3F radiance, LightEmitPdf pdf)
        {
            Pos = pos;
            Dir = dir;
            Normal = normal;
            UV = uv;
            Radiance = radiance;
            Pdf = pdf;
        }

        public void Deconstruct(out Vector3 pos, out Vector3 dir, out Vector3 normal, out Vector2 uv, out Color3F radiance, out LightEmitPdf pdf)
        {
            pos = Pos;
            dir = Dir;
            normal = Normal;
            uv = UV;
            radiance = Radiance;
            pdf = Pdf;
        }
    }

    public struct LightEmitPdf
    {
        public float PdfPos;
        public float PdfDir;

        public LightEmitPdf(float pdfPos, float pdfDir)
        {
            PdfPos = pdfPos;
            PdfDir = pdfDir;
        }

        public void Deconstruct(out float pdfPos, out float pdfDir)
        {
            pdfPos = PdfPos;
            pdfDir = PdfDir;
        }
    }

    public abstract class Light
    {
        /// <summary>
        /// 光源总功率
        /// </summary>
        public abstract Color3F Power { get; }

        public abstract LightType Type { get; }

        /// <summary>
        /// 光源是不是delta分布
        /// </summary>
        public bool IsDelta => (Type & LightType.DeltaPosition) != 0 || (Type & LightType.DeltaDirection) != 0;

        /// <summary>
        /// 光源是不是无穷远光
        /// </summary>
        public bool IsInfinite => (Type & LightType.Infinite) != 0 || (Type & LightType.DeltaDirection) != 0;

        /// <summary>
        /// 采样光源入射
        /// </summary>
        public abstract SampleLightResult SampleLi(LightEvalParam inct, Random rand);

        /// <summary>
        /// 光源入射的概率密度
        /// </summary>
        public abstract float PdfLi(LightEvalParam inct, Vector3 wi);

        /// <summary>
        /// 光源自发光
        /// </summary>
        public virtual Color3F Le(Ray3F ray)
        {
            return new Color3F(0.0f);
        }

        /// <summary>
        /// 采样光源出射
        /// </summary>
        public abstract LightEmitResult SampleEmit(Random rand);

        /// <summary>
        /// 光源出射的概率密度
        /// </summary>
        public abstract LightEmitPdf EmitPdf(Vector3 pos, Vector3 dir, Vector3 normal);

        /// <summary>
        /// 获取光源所在的介质
        /// </summary>
        public abstract Medium? GetMedium(Vector3 n, Vector3 dir);
    }

    public abstract class AreaLight : Light
    {
        public sealed override LightType Type => LightType.Area;

        /// <summary>
        /// 面光源发光
        /// </summary>
        public abstract Color3F L(Intersection inct, Vector3 w);
    }

    public abstract class InfiniteLight : Light
    {
        public float WorldRadius { get; private set; }
        public Vector3 WorldCenter { get; private set; }
        public sealed override LightType Type => LightType.Infinite;

        /// <summary>
        /// 预处理无限远光源
        /// </summary>
        public virtual void Preprocess(Scene scene)
        {
            BoundingBox3F bound = scene.Aggregate.WorldBound;
            WorldCenter = bound.Center;
            float length = bound.Diagonal.Length();
            WorldRadius = length / 2.0f;
        }

        public override Medium? GetMedium(Vector3 n, Vector3 dir)
        {
            return null; //无限远光源的介质默认从scene获取
        }
    }
}
