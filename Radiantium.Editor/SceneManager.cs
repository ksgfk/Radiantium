using System.Numerics;

namespace Radiantium.Editor
{
    public class SceneManager
    {
        readonly EditorApplication _app;
        readonly SceneObject _root;
        readonly List<SceneObject> _flattenObjs;
        readonly Stack<SceneObject> _dfsCollectStack;
        readonly Queue<SceneObject> _destroyQueue;

        public SceneObject Root => _root;
        public List<SceneObject> AllObjects => _flattenObjs;

        public SceneManager(EditorApplication app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _root = new SceneObject(_app);
            _flattenObjs = new List<SceneObject>();
            _dfsCollectStack = new Stack<SceneObject>();
            _destroyQueue = new Queue<SceneObject>();
        }

        public SceneObject CreateObject(string name, Vector3 pos, Quaternion rotation, SceneObject? parent = null)
        {
            SceneObject obj;
            if (parent == null)
            {
                obj = new SceneObject(_root, name);
            }
            else
            {
                obj = new SceneObject(parent, name);
            }
            obj.Transform.Position = pos;
            obj.Transform.Rotation = rotation;
            return obj;
        }

        public void DestroyObject(SceneObject obj)
        {
            if (!obj.IsMarkDestroy)
            {
                _destroyQueue.Enqueue(obj);
                obj.IsMarkDestroy = true;
            }
        }

        public void OnUpdate(float deltaTime)
        {
            //update matrix
            foreach (var child in _root.Children)
            {
                _dfsCollectStack.Push(child);
                child.Depth = 1;
            }
            while (_dfsCollectStack.Count > 0)
            {
                SceneObject o = _dfsCollectStack.Pop();
                _flattenObjs.Add(o);
                foreach (var child in o.Children)
                {
                    _dfsCollectStack.Push(child);
                    child.Depth = o.Depth + 1;
                }
                o.Transform.UpdateMatrix(o.Parent!.Transform.ModelToWorld);
            }

            //call object update
            foreach (var o in _flattenObjs)
            {
                o.OnUpdate(deltaTime);
            }

            //execute destroy
            while (_destroyQueue.Count > 0)
            {
                var o = _destroyQueue.Dequeue();
                o.OnDestroy();
                foreach (var child in o.Children)
                {
                    _destroyQueue.Enqueue(child);
                }
                o.DisconnectParent();
            }
        }

        public void AfterUpdate()
        {
            _flattenObjs.Clear();
        }

        public void Reset()
        {
            foreach (SceneObject o in Root.Children)
            {
                DestroyObject(o);
            }
        }
    }
}
