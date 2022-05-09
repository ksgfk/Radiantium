using ImGuiNET;
using System.Numerics;

namespace Radiantium.Editor
{
    public class Transform : EditorComponent
    {
        Vector3 _position;
        Quaternion _rotation;
        Vector3 _scale;

        Matrix4x4 _modelToWorld;
        Matrix4x4 _worldToModel;

        public ref Vector3 Position => ref _position;
        public ref Quaternion Rotation => ref _rotation;
        public ref Vector3 Scale => ref _scale;

        public Matrix4x4 ModelToWorld => _modelToWorld;
        public Matrix4x4 WorldToModel => _worldToModel;

        public Transform(SceneObject sceneObject) : base(sceneObject)
        {
            _position = Vector3.Zero;
            _rotation = Quaternion.Identity;
            _scale = Vector3.One;
            _modelToWorld = Matrix4x4.Identity;
            _worldToModel = Matrix4x4.Identity;
        }

        internal void UpdateMatrix(Matrix4x4 parent)
        {
            Matrix4x4 trans = Matrix4x4.CreateTranslation(Position);
            Matrix4x4 rotate = Matrix4x4.CreateFromQuaternion(_rotation);
            Matrix4x4 scale = Matrix4x4.CreateScale(_scale);
            Matrix4x4 thisModel = scale * rotate * trans;
            _modelToWorld = parent * thisModel;
            if (!Matrix4x4.Invert(_modelToWorld, out Matrix4x4 inv))
            {
                throw new InvalidOperationException("invalid matrix");
            }
            _worldToModel = inv;
        }

        public override void OnGui(float dragSpeed)
        {
            ImGui.DragFloat3("position", ref _position, dragSpeed);
            Vector4 tempRot = new Vector4(_rotation.X, _rotation.Y, _rotation.Z, _rotation.W);
            if (ImGui.DragFloat4("rotation", ref tempRot, dragSpeed))
            {
                _rotation = new Quaternion(tempRot.X, tempRot.Y, tempRot.Z, tempRot.W);
            }
            ImGui.DragFloat3("scale", ref _scale, dragSpeed);
        }
    }
}
