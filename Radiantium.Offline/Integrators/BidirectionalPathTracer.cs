using Radiantium.Core;
using Radiantium.Offline.Renderers;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;
using static System.MathF;

namespace Radiantium.Offline.Integrators
{
    public class BidirectionalPathTracer : IIntegrator
    {
        private ref struct ScopedAssignment<T>
        {
            public T Backup;
            public bool HasValue = false;
            public ScopedAssignment(ref T target, ref T value)
            {
                Backup = target;
                target = value;
                HasValue = true;
            }
            public void End(ref T target)
            {
                if (HasValue)
                {
                    target = Backup;
                }
            }
        }

        public int MaxDepth { get; }
        public int MinDepth { get; }
        public string TargetRendererName => "bdpt";

        public BidirectionalPathTracer(int maxDepth, int minDepth)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
        }

        public int GenerateCameraSubpath(
            Vector2 screenPos,
            Camera camera,
            Scene scene,
            Random rand,
            BdptPath cameraPath)
        {
            Ray3F ray = camera.SampleRay(screenPos);
            Medium? rayEnv = scene.GlobalMedium;
            Color3F coeff = new Color3F(1.0f);
            cameraPath.CreateCamera(camera, ray, coeff, rayEnv);
            CameraPdfWeResult cpwr = camera.PdfWe(ray);
            int maxDepth = MaxDepth;
            int walkCount = RandomWalk(ray, rayEnv, scene, rand, coeff, cpwr.PdfDir, cameraPath, maxDepth - 1, TransportMode.Radiance);
            return walkCount + 1;
        }

        public int GenerateLightSubpath(
            Scene scene, Random rand,
            BdptPath lightPath)
        {
            float selectPdf = scene.SampleLight(rand, out Light light);
            var (p, dir, n, _, le, pdf) = light.SampleEmit(rand);
            var (pdfPos, pdfDir) = pdf;
            if (pdfPos == 0 || pdfDir == 0 || le == Zero) { return 0; }
            Medium? lightEnv = light.GetMedium(n, dir);
            lightPath.CreateLight(light, p, n, le, pdfPos * pdfDir, lightEnv);
            Color3F coeff = le * AbsDot(dir, n) / (selectPdf * pdfPos * pdfDir);
            Ray3F ray = new Ray3F(p, dir, 0.0001f);
            int maxDepth = MaxDepth;
            int walkCount = RandomWalk(ray, lightEnv, scene, rand, coeff, pdfDir, lightPath, maxDepth - 1, TransportMode.Importance);
            if (lightPath[0].IsInfiniteLight)
            {
                if (walkCount > 0)
                {
                    lightPath[1].PdfForward = pdfPos;
                    if (lightPath[1].IsOnSurface)
                    {
                        lightPath[1].PdfForward *= AbsDot(ray.D, lightPath[1].N);
                    }
                }
                lightPath[0].PdfForward = InfiniteLightDensity(scene, ray.D);
            }
            return walkCount + 1;
        }

        public static int RandomWalk(
            Ray3F ray, Medium? rayEnv, Scene scene, Random rand,
            Color3F coeff, float pdf, BdptPath path, int maxDepth, TransportMode mode)
        {
            if (maxDepth == 0) { return 0; }
            int bounces = 1;
            float pdfFwd = pdf;
            while (true)
            {
                bool isHit = scene.Intersect(ray, out Intersection inct);
                SampleMediumResult msr = new SampleMediumResult { IsSampleMedium = false };
                if (rayEnv != null)
                {
                    Ray3F realRay = new Ray3F(ray.O, ray.D, ray.MinT, isHit ? inct.T : float.MaxValue);
                    msr = rayEnv.Sample(realRay, rand);
                    coeff *= msr.Tr;
                }
                if (coeff == Color3F.Black)
                {
                    break;
                }
                int vertexIndex = bounces;
                ref BdptVertex prev = ref path[bounces - 1];
                float pdfRev;
                if (msr.IsSampleMedium)
                {
                    path.CreateMedium(msr, coeff, pdfFwd, ref prev);
                    if (++bounces >= maxDepth)
                    {
                        break;
                    }
                    Medium envMedium = rayEnv!;
                    SamplePhaseFunctionResult sample = envMedium.SampleWi(msr.Wo, rand);
                    pdfFwd = pdfRev = sample.P;
                    ray = new Ray3F(msr.P, sample.Wi, ray.MinT);
                }
                else
                {
                    if (!isHit)
                    {
                        if (mode == TransportMode.Radiance)
                        {
                            path.CreateLight(null!, ray.At(10), -ray.D, coeff, pdfFwd, rayEnv);
                            ++bounces;
                        }
                        break;
                    }
                    if (!inct.HasSurface)
                    {
                        ray = new Ray3F(inct.P, ray.D, ray.MinT);
                        rayEnv = inct.GetMedium(ray.D);
                        continue;
                    }
                    ref BdptVertex vertex = ref path.CreateSurface(inct, coeff, pdfFwd, ref prev);
                    if (++bounces >= maxDepth) { break; }
                    SampleBxdfResult sample = inct.Surface.Sample(inct.ToLocal(inct.Wr), inct, rand, mode);
                    pdfFwd = sample.Pdf;
                    if (sample.Pdf > 0.0f)
                    {
                        coeff *= sample.Fr * AbsCosTheta(sample.Wi) / sample.Pdf;
                    }
                    else
                    {
                        break;
                    }
                    pdfRev = inct.Surface.Pdf(sample.Wi, inct.ToLocal(inct.Wr), inct, mode);
                    if (sample.HasSpecular)
                    {
                        vertex.IsDelta = true;
                        pdfFwd = pdfRev = 0;
                    }
                    Vector3 nextDir = inct.ToWorld(sample.Wi);
                    ray = new Ray3F(inct.P, nextDir, ray.MinT);
                    rayEnv = inct.GetMedium(nextDir);
                }
                prev.PdfReverse = path[vertexIndex].ConvertDensity(pdfRev, ref prev);
            }
            return bounces - 1; //我们从1开始, 所以结果要减去1
        }

        public void Connect(
            int nCamera, int nLight, Vector2 samplePoint, ColorBuffer renderTarget,
            Scene scene, Random rand,
            BdptPath lightPath, BdptPath cameraPath)
        {
            Color3F l = new Color3F(0.0f);
            for (int t = 1; t < nCamera; t++)
            {
                for (int s = 0; s < nLight; s++)
                {
                    int depth = t + s - 2;
                    if ((s == 1 && t == 1) || depth < 0 || depth > MaxDepth)
                    {
                        continue;
                    }
                    Vector2 pixel = new Vector2(-1, -1);
                    Color3F path = ConnectBdpt(scene, rand, lightPath, s, cameraPath, t, ref pixel);
                    if (!path.IsValid)
                    {
                        Logger.Warn($"[Offline.Renderer] -> Invalid color: {path}");
                        path = new Color3F(0.0f);
                    }
                    if (t != 1)
                    {
                        l += path;
                    }
                    else
                    {
                        int x, y;
                        if (pixel.X < 0)
                        {
                            x = (int)Floor(pixel.X);
                            y = (int)Floor(pixel.Y);
                        }
                        else
                        {
                            x = (int)Floor(samplePoint.X);
                            y = (int)Floor(samplePoint.Y);
                        }
                        renderTarget.AtomicAddRGB(x, y, path);
                    }
                }
            }
            {
                int x = (int)Floor(samplePoint.X);
                int y = (int)Floor(samplePoint.Y);
                renderTarget.RefRGB(x, y) += l;
            }
        }

        private Color3F ConnectBdpt(
            Scene scene, Random rand,
            BdptPath lightPath, int s,
            BdptPath cameraPath, int t,
            ref Vector2 pixel)
        {
            Color3F l = new Color3F(0.0f);
            if (t > 1 && s != 0 && cameraPath[t - 1].Type == BdptVertexType.Light)
            {
                return new Color3F(0.0f);
            }
            BdptVertex sampled = default;
            if (s == 0)
            {
                ref BdptVertex pt = ref cameraPath[t - 1];
                if (pt.IsLight)
                {
                    l = pt.Le(scene, ref cameraPath[t - 2]) * pt.Coeff;
                }
            }
            else if (t == 1)
            {
                ref BdptVertex qs = ref lightPath[s - 1];
                if (qs.IsConnectible)
                {
                    Camera camera = scene.MainCamera;
                    SampleCameraWiResult sample = camera.SampleWi(qs.P, rand);
                    pixel = sample.ScreenPos;
                    if (sample.Pdf > 0 && sample.We != Zero)
                    {
                        sampled = BdptPath.CreateCamera(camera, sample.Pos, sample.We / sample.Pdf);
                        l = qs.Coeff * qs.F(ref sampled, TransportMode.Importance) * sampled.Coeff;
                        if (qs.IsOnSurface)
                        {
                            l *= AbsDot(sample.Dir, qs.N);
                        }
                        if (l != Zero)
                        {
                            l *= scene.Transmittance(qs.P, sample.Pos, qs.Env, rand);
                        }
                    }
                }
            }
            else if (s == 1)
            {
                ref BdptVertex pt = ref cameraPath[t - 1];
                if (pt.IsConnectible)
                {
                    float selectPdf = scene.SampleLight(rand, out Light light);
                    SampleLightResult sample = light.SampleLi(new LightEvalParam(pt.P, 0), rand);
                    if (sample.Pdf > 0 && sample.Li != Zero)
                    {
                        sampled = BdptPath.CreateLight(light, sample.P, sample.Li / (sample.Pdf * selectPdf), 0.0f);
                        sampled.PdfForward = sampled.PdfLightOrigin(scene, ref pt);
                        l = pt.Coeff * pt.F(ref sampled, TransportMode.Radiance) * sampled.Coeff;
                        if (pt.IsOnSurface)
                        {
                            l *= AbsDot(sample.Wi, pt.N);
                        }
                        if (l != Zero)
                        {
                            l *= scene.Transmittance(pt.P, sample.P, pt.Env, rand);
                        }
                    }
                }
            }
            else
            {
                ref BdptVertex qs = ref lightPath[s - 1];
                ref BdptVertex pt = ref cameraPath[t - 1];
                if (qs.IsConnectible && pt.IsConnectible)
                {
                    l = qs.Coeff * qs.F(ref pt, TransportMode.Importance) * pt.F(ref qs, TransportMode.Radiance) * pt.Coeff;
                    if (l != Zero)
                    {
                        l *= G(scene, rand, ref qs, ref pt);
                    }
                }
            }
            float misWeight = l == Color3F.Black ? 0.0f : MisWeight(scene, lightPath, cameraPath, ref sampled, s, t);
            l *= misWeight;
            return l;
        }

        public static float InfiniteLightDensity(Scene scene, Vector3 w)
        {
            float pdf = 0;
            foreach (var light in scene.InfiniteLights)
            {
                pdf += light.PdfLi(new LightEvalParam(), -w);
            }
            return pdf / scene.InfiniteLights.Length;
        }

        public static Color3F G(Scene scene, Random rand, ref BdptVertex v0, ref BdptVertex v1)
        {
            Vector3 d = v0.P - v1.P;
            float g = 1 / d.LengthSquared();
            d *= Sqrt(g);
            if (v0.IsOnSurface) { g *= AbsDot(v0.N, d); }
            if (v1.IsOnSurface) { g *= AbsDot(v1.N, d); }
            Color3F tr = scene.Transmittance(v0.P, v1.P, v0.Env, rand);
            return g * tr;
        }

        public static float MisWeight(Scene scene, BdptPath lightPath, BdptPath cameraPath, ref BdptVertex sampled, int s, int t)
        {
            if (s + t == 2) { return 1; }
            float sumRi = 0;

            //bool hasQs = s > 0;
            //bool hasPt = t > 0;
            //bool hasQsMinus = s > 1;
            //bool hasPtMinus = t > 1;

            //ScopedAssignment<BdptVertex> a1 = new();
            //if (s == 1)
            //{
            //    if (hasQs) { a1 = new(ref qs(lightPath, s), ref sampled); }
            //}
            //else if (t == 1)
            //{
            //    if (hasPt) { a1 = new(ref pt(cameraPath, t), ref sampled); }
            //}
            //bool fs = false;
            //ScopedAssignment<bool> a2 = new(), a3 = new();
            //if (hasPt) { a2 = new(ref pt(cameraPath, t).IsDelta, ref fs); }
            //if (hasQs) { a3 = new(ref qs(lightPath, s).IsDelta, ref fs); }
            //ScopedAssignment<float> a4 = new();
            //if (hasPt)
            //{
            //    float pdf = s > 0 ?
            //        qs(lightPath, s).Pdf(scene, ref qsMinus(lightPath, s), ref pt(cameraPath, t)) :
            //        pt(cameraPath, t).PdfLightOrigin(scene, ref ptMinus(cameraPath, t));
            //    a4 = new(ref pt(cameraPath, t).PdfReverse, ref pdf);
            //}
            //ScopedAssignment<float> a5 = new();
            //if (hasPtMinus)
            //{
            //    float pdf = s > 0 ?
            //        pt(cameraPath, t).Pdf(scene, ref qs(lightPath, s), ref ptMinus(cameraPath, t)) :
            //        pt(cameraPath, t).PdfLight(scene, ref ptMinus(cameraPath, t));
            //    a5 = new(ref ptMinus(cameraPath, t).PdfReverse, ref pdf);
            //}
            //ScopedAssignment<float> a6 = new();
            //if (hasQs)
            //{
            //    float pdf = pt(cameraPath, t).Pdf(scene, ref ptMinus(cameraPath, t), ref qs(lightPath, s));
            //    a6 = new(ref qs(lightPath, s).PdfReverse, ref pdf);
            //}
            //ScopedAssignment<float> a7 = new();
            //if (hasQsMinus)
            //{
            //    float pdf = qs(lightPath, s).Pdf(scene, ref pt(cameraPath, t), ref qsMinus(lightPath, s));
            //    a7 = new(ref qsMinus(lightPath, s).PdfReverse, ref pdf);
            //}

            //float ri = 1;
            //for (int i = t - 1; i > 0; i--)
            //{
            //    ri *= remap0(cameraPath[i].PdfReverse) / remap0(cameraPath[i].PdfForward);
            //    if (!cameraPath[i].IsDelta && !cameraPath[i - 1].IsDelta)
            //    {
            //        sumRi += ri;
            //    }
            //}
            //ri = 1;
            //for (int i = s - 1; i >= 0; i--)
            //{
            //    ri *= remap0(lightPath[i].PdfReverse) / remap0(lightPath[i].PdfForward);
            //    bool deltaLightvertex = i > 0 ? lightPath[i - 1].IsDelta : lightPath[0].IsDeltaLight;
            //    if (!lightPath[i].IsDelta && !deltaLightvertex) { sumRi += ri; }
            //}

            //{
            //    if (s == 1)
            //    {
            //        a1.End(ref qs(lightPath, s));
            //    }
            //    else if (t == 1)
            //    {
            //        a1.End(ref pt(cameraPath, t));
            //    }
            //    if (hasPt) { a2.End(ref pt(cameraPath, t).IsDelta); }
            //    if (hasQs) { a3.End(ref qs(lightPath, s).IsDelta); }
            //    if (hasPt)
            //    {
            //        a4.End(ref pt(cameraPath, t).PdfReverse);
            //    }
            //    if (hasPtMinus) { a5.End(ref ptMinus(cameraPath, t).PdfReverse); }
            //    if (hasQs) { a6.End(ref qs(lightPath, s).PdfReverse); }
            //    if (hasQsMinus) { a7.End(ref qsMinus(lightPath, s).PdfReverse); }
            //}

            return 1 / (1 + sumRi);

            //static float remap0(float f)
            //{
            //    return f != 0 ? f : 1;
            //}
            //static ref BdptVertex qs(BdptPath lightPath, int s)
            //{
            //    return ref lightPath[s - 1];
            //}
            //static ref BdptVertex pt(BdptPath cameraPath, int t)
            //{
            //    return ref cameraPath[t - 1];
            //}
            //static ref BdptVertex qsMinus(BdptPath lightPath, int s)
            //{
            //    return ref lightPath[s - 2];
            //}
            //static ref BdptVertex ptMinus(BdptPath cameraPath, int t)
            //{
            //    return ref cameraPath[t - 2];
            //}
        }
    }
}
