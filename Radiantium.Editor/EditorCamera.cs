using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Editor
{
    public class EditorCamera
    {
        public float Fov;
        public float Near;
        public float Far;
        public Vector3 Origin;
        public Vector3 Target;
        public Vector3 Up;

        public Matrix4x4 WorldToCamera()
        {
            return Matrix4x4.CreateLookAt(Origin, Target, Up);
        }

        public Matrix4x4 CameraToProject(float ratio)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(MathExt.Radian(Fov), ratio, Near, Far);
        }
    }
}
