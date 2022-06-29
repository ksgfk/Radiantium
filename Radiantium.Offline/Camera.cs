using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline
{
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
        public Vector3 Dir;
        public float Distance;
        public Vector3 N;
        public float Pdf;
        public Color3F We;
        public Vector2 ScreenPos;

        public SampleCameraWiResult(Vector3 pos, Vector3 dir, float distance, Vector3 n, float pdf, Color3F we, Vector2 screenPos)
        {
            Pos = pos;
            Dir = dir;
            Distance = distance;
            N = n;
            Pdf = pdf;
            We = we;
            ScreenPos = screenPos;
        }
    }

    [Flags]
    public enum CameraType
    {
        DeltaPosition = 0b0001,
        DeltaDirection = 0b0010
    }

    public abstract class Camera
    {
        /// <summary>
        /// 摄像机x轴分辨率
        /// </summary>
        public abstract int ScreenX { get; }

        /// <summary>
        /// 摄像机y轴分辨率
        /// </summary>
        public abstract int ScreenY { get; }
        public abstract CameraType Type { get; }

        /// <summary>
        /// 在屏幕空间采样一条世界坐标系下的射线
        /// </summary>
        /// <param name="samplePosition">屏幕空间坐标</param>
        public abstract Ray3F SampleRay(Vector2 samplePosition);

        public abstract Color3F We(Ray3F ray);

        public abstract CameraPdfWeResult PdfWe(Ray3F ray);

        /// <summary>
        /// 根据世界空间下的一个坐标获取摄像机响应参数
        /// </summary>
        /// <param name="pos">世界空间坐标</param>
        /// <param name="rand">随机数发生器</param>
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
        public Matrix4x4 VP => _worldToCamera * _cameraToClip;
        public override CameraType Type => CameraType.DeltaPosition;

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
            _min = new Vector2(pMax.X, pMax.Y);
            _max = new Vector2(pMin.X, pMin.Y);
            float area = MathF.Abs((_max.X - _min.X) * (_max.Y - _min.Y));
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

        public float Importance(Vector3 d) //normalized direction in local camera space
        {
            float cosTheta = CosTheta(d);
            if (cosTheta <= 0) { return 0; }
            float focus = 1 / cosTheta; //at distance 1 plane
            Vector2 p = new(d.X * focus, d.Y * focus);
            if (p.X < _min.X || p.X > _max.X || p.Y < _min.Y || p.Y > _max.Y) //check point inside plane
            {
                return 0.0f;
            }
            return _normalization * focus * focus * focus;
        }

        public override Color3F We(Ray3F ray)
        {
            Vector3 local = TransformNormal(ray.D, _worldToCamera);
            float res = Importance(local);
            return new Color3F(res);
        }

        public override CameraPdfWeResult PdfWe(Ray3F ray)
        {
            Vector3 local = TransformNormal(ray.D, _worldToCamera);
            float pdfDir = Importance(local);
            float pdfPos = pdfDir == 0.0f ? 0.0f : 1.0f;
            return new CameraPdfWeResult(pdfPos, pdfDir);
        }

        public override SampleCameraWiResult SampleWi(Vector3 pos, Random rand)
        {
            Vector3 localTarget = Transform(pos, _worldToCamera);
            if (localTarget.Z < Near || localTarget.Z > Far) { return new SampleCameraWiResult(); }
            Vector3 screen = Transform(localTarget, _cameraToClip);
            screen.X /= screen.Z;
            screen.Y /= screen.Z;
            screen.Z = 1;
            if (screen.X < 0 || screen.X > 1 || screen.Y < 0 || screen.Y > 1) { return new SampleCameraWiResult(); }
            Vector2 ssPos = new Vector2(screen.X, screen.Y) * new Vector2(ScreenX, ScreenY);
            float dist = localTarget.Length();
            float inv = 1 / dist;
            Vector3 localDir = localTarget * inv;
            Vector3 samplePos = Transform(new Vector3(0, 0, 0), _cameraToWorld);
            Vector3 dir = (samplePos - pos) / dist;
            Vector3 normal = TransformNormal(new Vector3(0, 0, 1), _cameraToWorld);
            float pdf = 1;
            float importance = Importance(localDir) * inv * inv;
            return new SampleCameraWiResult(samplePos, dir, dist, normal, pdf, new Color3F(importance), ssPos);
        }
    }
}
