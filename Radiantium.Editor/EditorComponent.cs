namespace Radiantium.Editor
{
    public class EditorComponent
    {
        readonly SceneObject _sceneObject;

        public SceneObject TargetObject => _sceneObject;

        protected EditorComponent(SceneObject sceneObject)
        {
            _sceneObject = sceneObject ?? throw new ArgumentNullException(nameof(sceneObject));
        }

        public virtual void OnUpdate(float deltaTime) { }

        public virtual void OnDestroy() { }

        public virtual void OnGui(float dragSpeed) { }
    }
}
