﻿using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    [Flags]
    public enum BxdfType
    {
        Reflection,
        Transmission,
        Diffuse,
        Glossy,
        Specular
    }

    public struct SampleBxdfResult
    {
        public Vector3 Wi;
        public Color3F Fr;
        public float Pdf;
        public BxdfType Type;

        public SampleBxdfResult(Vector3 wi, Color3F fr, float pdf, BxdfType type)
        {
            Wi = wi;
            Fr = fr;
            Pdf = pdf;
            Type = type;
        }

        public void Deconstruct(out Vector3 wi, out Color3F fr, out float pdf, out BxdfType type)
        {
            wi = Wi;
            fr = Fr;
            pdf = Pdf;
            type = Type;
        }
    }

    public interface IBxdf
    {
        BxdfType Type { get; }

        Color3F Fr(Vector3 wo, Vector3 wi);

        SampleBxdfResult Sample(Vector3 wo, Random rand);

        float Pdf(Vector3 wo, Vector3 wi);
    }
}