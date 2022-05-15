using Radiantium.Core;
using System.Numerics;
using static System.MathF;

namespace Radiantium.Offline.Lights
{
    public class InfiniteAreaLight : InfiniteLight
    {
        readonly Matrix4x4 _modelToWorld;
        readonly Matrix4x4 _worldToModel;
        readonly Color3F _avgPower;
        readonly Distribution2D _dist;
        readonly int _width;
        readonly int _height;
        public Texture2D Lmap { get; }
        public override Color3F Power => _avgPower * PI * WorldRadius * WorldRadius;

        public InfiniteAreaLight(Matrix4x4 modelToWorld, Texture2D lmap)
        {
            _modelToWorld = modelToWorld;
            if (!Matrix4x4.Invert(_modelToWorld, out _worldToModel))
            {
                throw new ArgumentException("invalid matrix");
            }
            Lmap = lmap ?? throw new ArgumentNullException(nameof(lmap));
            _avgPower = new Color3F();

            _width = lmap.Width;
            _height = lmap.Height;
            float[] luminance = new float[_width * _height];
            Color3F avg = new Color3F();
            Parallel.For(0, _height, y =>
            {
                float sinTheta = Sin((y + 0.5f) / _height * PI);
                float v = y / (float)_height;
                for (int x = 0; x < _width; x++)
                {
                    float u = x / (float)_width;
                    Color3F color = Lmap.Sample(new Vector2(u, v));
                    float lum = Lmap.Sample(new Vector2(u, v)).GetLuminance() * sinTheta;
                    luminance[y * _width + x] = lum;
                    lock (this)
                    {
                        avg += color;
                    }
                }
            });
            _avgPower = avg / (_width * _height);
            _dist = new Distribution2D(luminance, _width, _height);
        }

        public override Color3F Le(Ray3F ray)
        {
            Vector3 d = ray.D;
            Vector3 dir = Vector3.Normalize(Vector3.TransformNormal(d, _worldToModel));
            float phi = Atan2(dir.X, -dir.Z);
            phi = (phi < 0) ? (phi + 2 * PI) : phi;
            float theta = Acos(Math.Clamp(dir.Y, -1, 1));
            float u = phi / (2 * PI);
            float v = theta / PI;
            Vector2 uv = new Vector2(u, v);
            return Lmap.Sample(uv);
        }

        public override float PdfLi(Intersection inct, Vector3 wi)
        {
            Vector3 dir = Vector3.Normalize(Vector3.TransformNormal(wi, _worldToModel));
            float phi = Atan2(dir.X, -dir.Z);
            phi = (phi < 0) ? (phi + 2 * PI) : phi;
            float theta = Acos(Math.Clamp(dir.Y, -1, 1));
            float sinTheta = Sin(theta);
            if (sinTheta == 0.0f)
            {
                return 0.0f;
            }
            float u = phi / (2 * PI);
            float v = theta / PI;
            return _dist.ContinuousPdf(u, v) / (2 * PI * PI * sinTheta);
        }

        public override LightSampleResult SampleLi(Intersection inct, Random rand)
        {
            _dist.SampleContinuous(rand.NextVec2(), out float pdf, out (int, int) offset);
            if (pdf <= 0) { return new LightSampleResult(); }
            float y = offset.Item1 / (float)_height;
            float x = offset.Item2 / (float)_width;
            Color3F radiance = Lmap.Sample(new Vector2(x, y));
            float theta = y * PI;
            float phi = x * 2 * PI;
            float sinTheta = Sin(theta);
            if (sinTheta == 0) { return new LightSampleResult(); }
            pdf /= (2 * PI * PI * sinTheta);
            Vector3 sphDir = MathExt.SphericalCoordinates(theta, phi);
            Vector3 dir = new Vector3(sphDir.Y, sphDir.Z, -sphDir.X);
            Vector3 worldDir = Vector3.Normalize(Vector3.TransformNormal(dir, _modelToWorld));
            Vector3 pos = worldDir * WorldRadius * 20 + WorldCenter;
            Vector3 wi = Vector3.Normalize(pos - inct.P);
            return new LightSampleResult(pos, radiance, wi, pdf);
        }
    }
}
