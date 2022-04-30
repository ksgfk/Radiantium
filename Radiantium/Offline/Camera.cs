using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public abstract class Camera
    {
        public abstract int ScreenX { get; }
        public abstract int ScreenY { get; }

        public abstract Ray3F SampleRay(Vector2 samplePosition);
    }

    public class PerspectiveCamera : Camera
    {
        private float _aspect;
        private Vector2 _invResolve;
        private Matrix4x4 _clipToCamera;
        private Matrix4x4 _cameraToWorld;

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
            _invResolve = new Vector2(1.0f) / new Vector2(ScreenX, ScreenX);
            _aspect = ScreenX / (float)ScreenY;

            float recip = 1.0f / (Far - Near);//将相机空间中的向量投影到z=1的平面上
            float cot = 1.0f / MathF.Tan(MathExt.Radian(Fov / 2.0f));//cotangent确保NDC的near到far是[0,1]
            //scale和translate是将裁剪空间变换到[0,1][0,1][0,1]，并考虑纵横比
            //perspective是投影矩阵
            Matrix4x4 perspective = new()
            {
                M11 = cot, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = cot, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = Far * recip, M34 = 1,
                M41 = 0, M42 = 0, M43 = -Near * Far * recip, M44 = 0,
            };
            Matrix4x4 scale = new()//嗯...始终不知道为啥要反转x轴...
            {
                M11 = -0.5f, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = 0.5f * _aspect, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                M41 = 0, M42 = 0, M43 = 0, M44 = 1,
            };
            Matrix4x4 translate = new()
            {
                M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                M41 = -1, M42 = 1 / _aspect, M43 = 0, M44 = 1,
            };
            Matrix4x4 p = perspective * translate * scale;
            //求逆矩阵，从裁剪空间[-1,1][-1,1][0,1]变换回相机空间
            if (!Matrix4x4.Invert(p, out _clipToCamera))
            {
                throw new ArgumentException("无法创建矩阵");
            }

            Matrix4x4 v = MathExt.LookAtLeftHand(Origin, Target, Up);
            _cameraToWorld = v;//构建 相机 局部坐标系 到 世界坐标系 的变换矩阵
        }

        public override Ray3F SampleRay(Vector2 samplePosition)
        {
            Vector3 ndc = new(samplePosition * _invResolve, 0.0f);//NDC近平面上的坐标
            Vector3 near = Vector3.Transform(ndc, _clipToCamera);//在近平面上坐标
            Vector3 dir = Vector3.Normalize(near);//归一化后就是射线方向了
            float invZ = 1.0f / dir.Z;
            Vector3 o = Vector3.Transform(Vector3.Zero, _cameraToWorld);
            Vector3 d = Vector3.TransformNormal(dir, _cameraToWorld);
            return new Ray3F(o, d, Near * invZ, Far * invZ);
        }
    }
}