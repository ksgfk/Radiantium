using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.MathF;

namespace Radiantium.Offline.Lights
{
    //无限远的环境光, 接收一张latitude–longitude radiance map
    //重要性采样: https://pbr-book.org/3ed-2018/Light_Transport_I_Surface_Reflection/Sampling_Light_Sources#InfiniteAreaLights
    public class InfiniteAreaLight : InfiniteLight
    {
        readonly Matrix4x4 _modelToWorld;
        readonly Matrix4x4 _worldToModel;
        readonly Color3F _avgPower;
        readonly Distribution2D _dist; //光源亮度的二维分布情况
        readonly int _width;
        readonly int _height;
        public Texture2D LightMap { get; }
        public override Color3F Power => _avgPower * PI * WorldRadius * WorldRadius;

        public InfiniteAreaLight(Matrix4x4 modelToWorld, Texture2D lmap)
        {
            _modelToWorld = modelToWorld; //理论上只能有旋转矩阵生效, 但是这里没做限制
            if (!Matrix4x4.Invert(_modelToWorld, out _worldToModel))
            {
                throw new ArgumentException("invalid matrix");
            }
            LightMap = lmap ?? throw new ArgumentNullException(nameof(lmap));
            _avgPower = new Color3F();

            _width = lmap.Width;
            _height = lmap.Height;
            float[] luminance = new float[_width * _height]; //计算光源亮度
            Color3F avg = new Color3F();
            Parallel.For(0, _height, y =>
            {
                //因为经纬图越靠近两极, 数据越稀疏 (相对于赤道福建), 所以越靠近两极提供的照明越少
                float sinTheta = Sin((y + 0.5f) / _height * PI);
                float v = y / (float)_height;
                for (int x = 0; x < _width; x++)
                {
                    float u = x / (float)_width;
                    Color3F color = LightMap.Sample(new Vector2(u, v));
                    float lum = color.GetLuminance() * sinTheta;
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
            //将射线方向转化到模型空间下
            Vector3 d = ray.D;
            Vector3 dir = Vector3.Normalize(Vector3.TransformNormal(d, _worldToModel));
            //在转化为球坐标
            float phi = Atan2(dir.X, -dir.Z);
            phi = (phi < 0) ? (phi + 2 * PI) : phi;
            float theta = Acos(Math.Clamp(dir.Y, -1, 1));
            float u = phi / (2 * PI);
            float v = theta / PI;
            Vector2 uv = new Vector2(u, v);
            return LightMap.Sample(uv);
        }

        private float PdfDir(Vector3 wi)
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

        public override float PdfLi(LightEvalParam inct, Vector3 wi)
        {
            return PdfDir(wi);
        }

        private (Vector3, float) SampleLightDir(Random rand, out Vector2 uv)
        {
            //亮度越高的方向, 采样到的概率越大 (重要性采样)
            _dist.SampleContinuous(rand.NextVec2(), out float pdf, out (int, int) offset);
            if (pdf <= 0) { uv = default; return (new Vector3(), 0); }
            float y = offset.Item1 / (float)_height;
            float x = offset.Item2 / (float)_width;
            uv = new Vector2(x, y);
            float theta = y * PI;
            float phi = x * 2 * PI;
            float sinTheta = Sin(theta);
            if (sinTheta == 0) { return (new Vector3(), 0); }
            pdf /= (2 * PI * PI * sinTheta);
            Vector3 sphDir = SphericalCoordinates(theta, phi);
            Vector3 dir = new Vector3(sphDir.Y, sphDir.Z, -sphDir.X);
            Vector3 worldDir = Vector3.Normalize(Vector3.TransformNormal(dir, _modelToWorld));
            return (worldDir, pdf);
        }

        public override SampleLightResult SampleLi(LightEvalParam inct, Random rand)
        {
            (Vector3 worldDir, float pdfDir) = SampleLightDir(rand, out Vector2 uv);
            if (pdfDir <= 0) { return new SampleLightResult(); }
            Color3F radiance = LightMap.Sample(uv);
            Vector3 pos = worldDir * WorldRadius * 20 + WorldCenter; //不要改这个20, 太近距离的点遮挡测试会失败
            Vector3 wi = Vector3.Normalize(pos - inct.P);
            return new SampleLightResult(pos, radiance, wi, pdfDir);
        }

        //https://pbr-book.org/3ed-2018/Light_Transport_III_Bidirectional_Methods/The_Path-Space_Measurement_Equation#x2-DistantLights
        public override LightEmitResult SampleEmit(Random rand)
        {
            (Vector3 worldDir, float pdfDir) = SampleLightDir(rand, out Vector2 uv);
            Color3F radiance = LightMap.Sample(uv);
            Vector3 normal = -worldDir;
            Coordinate coord = new Coordinate(normal);
            Vector2 cd = Probability.SquareToConcentricDisk(rand.NextVec2());
            Vector3 disk = WorldCenter + WorldRadius * (cd.X * coord.X + cd.Y * coord.Y);
            Vector3 pos = disk + worldDir * WorldRadius;
            float pdfPos = 1 / (PI * Sqr(WorldRadius));
            return new LightEmitResult(pos, -worldDir, -worldDir, uv, radiance, new LightEmitPdf(pdfPos, pdfDir));
        }

        public override LightEmitPdf EmitPdf(Vector3 pos, Vector3 dir, Vector3 normal)
        {
            float pdfDir = PdfDir(dir);
            float pdfPos = 1 / (PI * Sqr(WorldRadius));
            return new LightEmitPdf(pdfPos, pdfDir);
        }
    }
}
