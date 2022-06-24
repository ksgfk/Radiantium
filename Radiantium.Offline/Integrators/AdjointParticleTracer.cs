using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.Color3F;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;

namespace Radiantium.Offline.Integrators
{
    public class AdjointParticleRenderer : DefaultRenderer //TODO: reforge renderer
    {
        public int MaxDepth { get; }
        public int MinDepth { get; }
        public float RRThreshold { get; }
        public int ParticleEmit => _sampleCount;

        public AdjointParticleRenderer(
            Scene scene,
            Camera camera,
            Integrator integrator,
            int sampleCount,
            int maxTask,
            int maxDepth,
            int minDepth,
            float rrThreshold) : base(
                scene,
                camera,
                integrator,
                sampleCount,
                maxTask)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
            RRThreshold = rrThreshold;
        }

        protected override void RenderTask(int range, ParallelLoopState state)
        {
            if (!_gen.Next(out _))
            {
                return;
            }
            Random rand = _rnd.Value!;
            try
            {
                for (int t = 0; t < ParticleEmit; t++)
                {
                    Trace(_scene, rand);
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e);
                state.Stop();
            }
            Interlocked.Add(ref _nowCount, 1);
        }

        private void Trace(Scene scene, Random rand)
        {
            float selectPdf = scene.SampleLight(rand, out Light light);
            if (selectPdf == 0.0f) { return; }
            LightEmitResult emit = light.SampleEmit(rand);
            if (emit.Radiance == Black) { return; }
            Camera camera = scene.MainCamera;
            Color3F coeff = emit.Radiance * AbsDot(emit.Dir, emit.Normal) / (selectPdf * emit.Pdf.PdfPos * emit.Pdf.PdfDir);
            if ((camera.Type & CameraType.DeltaPosition) != 0)
            {
                SampleCameraWiResult sample = camera.SampleWi(emit.Pos, rand);
                if (sample.Pdf > 0 && sample.We != Black)
                {
                    if (!scene.IsOccluded(sample.Pos, emit.Pos))
                    {
                        Color3F color = coeff * sample.We / sample.Pdf;
                        Vector2 ssPos = sample.ScreenPos;
                        int x = (int)Floor(ssPos.X);
                        int y = (int)Floor(ssPos.Y);
                        lock (RenderTarget)
                        {
                            RenderTarget.RefRGB(x, y) += color;
                        }
                    }
                }
            }
            Ray3F lightRay = new Ray3F(emit.Pos, emit.Dir, 0.0001f);
            for (int bounces = 1; ; bounces++)
            {
                if (MaxDepth != -1 && bounces >= MaxDepth)
                {
                    break;
                }
                if (bounces > MinDepth)
                {
                    if (rand.NextFloat() > RRThreshold) { break; }
                    coeff /= RRThreshold;
                }
                bool anyHit = scene.Intersect(lightRay, out Intersection inct);
                if (!anyHit) { break; }
                if (!inct.HasSurface)
                {
                    lightRay = new Ray3F(inct.P, lightRay.D, lightRay.MinT);
                    bounces--;
                    continue;
                }
                SampleCameraWiResult sample = camera.SampleWi(inct.P, rand);
                if (sample.Pdf > 0 && sample.We != Black)
                {
                    if (!scene.IsOccluded(sample.Pos, inct.P))
                    {
                        Color3F f = inct.Surface.Fr(inct.ToLocal(inct.Wr), inct.ToLocal(sample.Dir), inct, TransportMode.Importance);
                        float absCos = AbsCosTheta(inct.ToLocal(sample.Dir));
                        Color3F color = coeff * f * sample.We * absCos / sample.Pdf;
                        Vector2 ssPos = sample.ScreenPos;
                        int x = (int)Floor(ssPos.X);
                        int y = (int)Floor(ssPos.Y);
                        lock (RenderTarget)
                        {
                            RenderTarget.RefRGB(x, y) += color;
                        }
                    }
                }
                (Vector3 wi, Color3F fr, float pdf, _) = inct.Surface.Sample(inct.ToLocal(inct.Wr), inct, rand, TransportMode.Importance);
                if (pdf > 0.0f)
                {
                    coeff *= fr * AbsCosTheta(wi) / pdf;
                }
                else
                {
                    break;
                }
                lightRay = inct.SpawnRay(inct.ToWorld(wi));
            }
        }

        protected override void AfterRender()
        {
            float pathCount = AllBlockCount * ParticleEmit;
            float v = (float)_camera.ScreenX * _camera.ScreenY / pathCount;
            for (int x = 0; x < _camera.ScreenX; x++)
            {
                for (int y = 0; y < _camera.ScreenY; y++)
                {
                    RenderTarget.RefRGB(x, y) *= v;
                }
            }
        }
    }
}
