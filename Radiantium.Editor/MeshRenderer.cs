using ImGuiNET;

namespace Radiantium.Editor
{
    public class MeshRenderer : EditorComponent
    {
        readonly AssetManager _asset;
        readonly AssetEntryReference<AssetModel> _model;
        readonly RenderItem _ri;

        public AssetEntryReference<AssetModel> Model => _model;
        public RenderItem Ri => _ri;

        public MeshRenderer(SceneObject sceneObject) : base(sceneObject)
        {
            _asset = sceneObject.App.Asset;
            _model = TargetObject.App.GetAssetReference<AssetModel>();
            _ri = new RenderItem(sceneObject.App.Render);
        }

        public override void OnGui(float dragSpeed)
        {
            if (!_model.IsValid)
            {
                _model.SetReference(null);
            }
            string name = _model.Key ?? "no ref";
            if (ImGui.BeginCombo("model ref", name))
            {
                foreach (var m in _asset.Models)
                {
                    if (ImGui.Selectable(m.MyPath))
                    {
                        _model.SetReference(m.MyPath);
                    }
                }
                ImGui.EndCombo();
            }
        }

        public void UpdateRenderItem()
        {
            _ri._drawData = new ItemDrawData()
            {
                ModelToWorld = TargetObject.Transform.ModelToWorld,
                WorldToModel = TargetObject.Transform.WorldToModel,
            };
            var mesh = TargetObject.App.Render.Meshes[_model.Key!];
            _ri._vbo = mesh.Vbo;
            _ri._ebo = mesh.Ebo;
            _ri._indexCount = (uint)_model.Get()!.Model.Indices.Length;
            _ri._drawDataUbo.SubData(ref _ri._drawData);
        }
    }
}
