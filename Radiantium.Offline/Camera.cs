using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.Numerics.Vector3;
using static Radiantium.Offline.Coordinate;

namespace Radiantium.Offline
{
    public struct CameraWeResult
    {
        public Color3F We;
        public Vector2 Raster;

        public CameraWeResult(Color3F we, Vector2 raster)
        {
            We = we;
            Raster = raster;
        }
    }

    public struct CameraPdfWeResult
    {
        public float PdfPos;
        public float PdfDir;

        public CameraPdfWeResult(float pdfPos, float pdfDir)
        {
            PdfPos = pdfPos;
            PdfDir = pdfDir;
        }
    }

    public struct SampleCameraWiResult
    {
        public Vector3 Pos;
        public Vector3 Wi;
        public Color3F We;
        public float PdfDir;
        public Vector2 Raster;

        public SampleCameraWiResult(Vector3 pos, Vector3 wi, Color3F we, float pdfDir, Vector2 raster)
        {
            Pos = pos;
            Wi = wi;
            We = we;
            PdfDir = pdfDir;
            Raster = raster;
        }
    }

    public abstract class Camera
    {
        public abstract int ScreenX { get; }
        public abstract int ScreenY { get; }

        public abstract Ray3F SampleRay(Vector2 samplePosition);

        public abstract CameraWeResult We(Ray3F ray);

        public abstract CameraPdfWeResult PdfWe(Ray3F ray);

        public abstract SampleCameraWiResult SampleWi(Vector3 pos, Random rand);
    }

    public class PerspectiveCamera : Camera
    {
        float _aspect;
        Vector2 _invResolve;
        Matrix4x4 _clipToCamera;
        Matrix4x4 _cameraToClip;
        Matrix4x4 _worldToCamera;
        Matrix4x4 _cameraToWorld;
        Vector2 _min;
        Vector2 _max;
        float _normalization;

        public float Fov { get; }
        public float Near { get; }
        public float Far { get; }
        public Vector3 Origin { get; }
        public Vector3 Target { get; }
        public Vector3 Up { get; }
        public override int ScreenX { get; }
        public override int ScreenY { get; }

        public PerspectiveCamera(float fov,
            float near, float far,
            Vector3 origin, Vector3 target, Vector3 up,
            int screenX, int screenY)
        {
            Fov = fov;
            Near = near;
            Far = far;
            Origin = origin;
            Target = target;
            Up = up;
            ScreenX = screenX;
            ScreenY = screenY;
            Update();
        }

        private void Update()
        {
            _invResolve = new Vector2(1.0f) / new Vector2(ScreenX, ScreenY);
            _aspect = ScreenX / (float)ScreenY;

            float recip = 1.0f / (Far - Near);//将相机空间中的向量投影到z=1的平面上
            float cot = 1.0f / MathF.Tan(Radian(Fov / 2.0f));//cotangent确保NDC的near到far是[0,1]
            //scale和translate是将裁剪空间变换到[0,1][0,1][0,1]，并考虑纵横比
            //perspective是投影矩阵
            Matrix4x4 perspective = new()
            {
                M11 = cot, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = cot, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = Far * recip, M34 = 1,
                M41 = 0, M42 = 0, M43 = -Near * Far * recip, M44 = 0,
            };
            Matrix4x4 scale = new()
            {
                M11 = -0.5f, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = -0.5f * _aspect, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                M41 = 0, M42 = 0, M43 = 0, M44 = 1,
            };
            Matrix4x4 translate = new()
            {
                M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                M41 = -1, M42 = -1 / _aspect, M43 = 0, M44 = 1,
            };
            Matrix4x4 p = perspective * translate * scale;
            _cameraToClip = p;
            //求逆矩阵，从裁剪空间[-1,1][-1,1][0,1]变换回相机空间
            if (!Matrix4x4.Invert(p, out _clipToCamera))
            {
                throw new ArgumentException("can't create invert mat");
            }

            Matrix4x4 v = LookAtLeftHand(Origin, Target, Up);
            _cameraToWorld = v;//构建 相机 局部坐标系 到 世界坐标系 的变换矩阵
            if (!Matrix4x4.Invert(v, out _worldToCamera))
            {
                throw new ArgumentException("can't create invert mat");
            }

            Vector3 pMin = Transform(new Vector3(0, 0, 0), _clipToCamera);
            Vector3 pMax = Transform(new Vector3(1, 1, 0), _clipToCamera);
            pMin /= pMin.Z;
            pMax /= pMax.Z;
            float area = MathF.Abs((pMax.X - pMin.X) * (pMax.Y - pMin.Y));
            _min = new Vector2(pMin.X, pMin.Y);
            _max = new Vector2(pMax.X, pMax.Y);
            _normalization = 1 / area;
        }

        public override Ray3F SampleRay(Vector2 samplePosition)
        {
            Vector3 ndc = new(samplePosition * _invResolve, 0.0f);//NDC近平面上的坐标
            Vector3 near = Transform(ndc, _clipToCamera);//在近平面上坐标
            Vector3 dir = Normalize(near);//归一化后就是射线方向了
            float invZ = 1.0f / dir.Z;
            Vector3 o = Transform(Zero, _cameraToWorld);
            Vector3 d = Normalize(TransformNormal(dir, _cameraToWorld));
            return new Ray3F(o, d, Near * invZ, Far * invZ);
        }

        public float Importance(Vector3 w)
        {
            float cosTheta = CosTheta(w);
            if (cosTheta <= 0) { return 0; }
            float focus = 1 / cosTheta; //at distance 1 plane
            Vector2 p = new(w.X * focus, w.Y * focus);
            if (p.X < _min.X || p.X > _max.X || p.Y < _min.Y || p.Y > _max.Y) //check point inside plane
            {
                return 0.0f;
            }
            return _normalization * focus * Sqr(focus);
        }

        public bool GetScreenPosition(Vector3 local, out Vector2 pos)
        {
            if (CosTheta(local) <= 0) { pos = new Vector2(); return false; }
            Vector3 ndc = Transform(local, _cameraToClip);
            if (ndc.X < 0 || ndc.X > 1 || ndc.Y < 0 || ndc.Y > 1)
            {
                pos = new Vector2(); return false;
            }
            pos = new Vector2(ndc.X, ndc.Y) * new Vector2(ScreenX, ScreenY);
            return true;
        }

        public override CameraWeResult We(Ray3F ray)
        {
            Vector3 d = Transform(ray.D, _worldToCamera);
            float importance = Importance(d);
            if (importance == 0 || !GetScreenPosition(d, out Vector2 screenPos)) { return new CameraWeResult(); }
            return new CameraWeResult(new Color3F(importance), screenPos);
        }

        public override CameraPdfWeResult PdfWe(Ray3F ray)
        {
            Vector3 d = Transform(ray.D, _worldToCamera);
            float importance = Importance(d);
            return new CameraPdfWeResult(1, importance);
        }

        public override SampleCameraWiResult SampleWi(Vector3 pos, Random rand)
        {
            Vector2 rng = rand.NextVec2();
            Vector3 samplePos = new Vector3(rng.X, rng.Y, 0);
            throw new NotImplementedException();
        }
    }
}
