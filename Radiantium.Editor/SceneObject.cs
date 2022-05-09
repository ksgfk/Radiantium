namespace Radiantium.Editor
{
    public class SceneObject
    {
        readonly LinkedListNode<SceneObject> _nodeInParent;
        readonly LinkedList<SceneObject> _children;
        readonly List<EditorComponent> _components;
        readonly Transform _transform;
        SceneObject? _parent;
        string _name;
        int _depth;
        bool _isMarkDestroy;

        public string Name { get => _name; set => _name = value; }
        public Transform Transform => _transform;
        public SceneObject? Parent => _parent;
        public IReadOnlyCollection<SceneObject> Children => _children;
        public int Depth { get => _depth; internal set => _depth = value; }
        public bool IsMarkDestroy { get => _isMarkDestroy; internal set => _isMarkDestroy = value; }
        public List<EditorComponent> Components => _components;

        internal SceneObject()
        {
            _nodeInParent = null!;
            _children = new LinkedList<SceneObject>();
            _components = new List<EditorComponent>(1);
            _parent = null;
            _name = "root";
            _transform = new Transform(this);
            _components.Add(_transform);
        }

        public SceneObject(SceneObject parent, string name = "")
        {
            _nodeInParent = new LinkedListNode<SceneObject>(this);
            _children = new LinkedList<SceneObject>();
            _components = new List<EditorComponent>();
            _name = name;

            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent._children.AddLast(_nodeInParent);

            _transform = new Transform(this);
            _components.Add(_transform);
        }

        public void SetParent(SceneObject parent)
        {
            SceneObject origin = _parent!;
            SceneObject newPar = parent;
            origin._children.Remove(_nodeInParent);
            newPar._children.AddLast(_nodeInParent);
            _parent = newPar;
        }

        public void DisconnectParent()
        {
            _parent!._children.Remove(_nodeInParent);
            _parent = null!;
        }

        public void OnUpdate(float deltaTime)
        {
            foreach (var com in _components)
            {
                com.OnUpdate(deltaTime);
            }
        }

        public void OnDestroy()
        {
            foreach (var com in _components)
            {
                com.OnDestroy();
            }
        }

        public void AddComponent(EditorComponent com)
        {
            _components.Add(com);
        }

        public void RemoveComponent(EditorComponent com)
        {
            _components.Remove(com);
        }

        public T? GetComponent<T>() where T : EditorComponent
        {
            foreach (var com in _components)
            {
                if (com.GetType() == typeof(T))
                {
                    return com as T;
                }
            }
            return default;
        }
    }
}
