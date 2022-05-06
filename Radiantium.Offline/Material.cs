﻿using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public abstract class Material
    {
        public abstract BxdfType Type { get; }

        public abstract Color3F Fr(Vector3 wo, Vector3 wi);

        public abstract SampleBxdfResult Sample(Vector3 wo, Random rand);

        public abstract float Pdf(Vector3 wo, Vector3 wi);
    }
}