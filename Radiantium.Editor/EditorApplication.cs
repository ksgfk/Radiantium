using Radiantium.Core;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Radiantium.Editor
{
    //TODO: 不行了, 这坨屎山玩意写不下去的, 绝对不可能写下去的
    //得重新设计一遍
    public class EditorApplication
    {
        readonly AssetManager _asset;
        readonly SceneManager _scene;
        readonly RenderManager _render;
        readonly ImguiManager _gui;
        readonly GL _gl;
        readonly IWindow _window;
        readonly IInputContext _input;
        readonly List<Type> _comTypeList;
        readonly EditorCamera _camera;

        DateTime _lastUpdate;
        float _deltaTime;
        bool _hasWorkSpace;
        string _workingDir;

        public float DeltaTime => _deltaTime;
        public AssetManager Asset => _asset;
        public SceneManager Scene => _scene;
        public RenderManager Render => _render;
        public ImguiManager Gui => _gui;
        public bool HasWorkSpace => _hasWorkSpace;
        public string WorkingDir => _workingDir;
        public List<Type> ComTypes => _comTypeList;
        public IWindow WindowInstance => _window;
        public EditorCamera Camera => _camera;

        public EditorApplication()
        {
            _comTypeList = new List<Type>();
            CollectEditorComponentType();
            _asset = new AssetManager(this);
            _scene = new SceneManager(this);
            Silk.NET.Windowing.Glfw.GlfwWindowing.RegisterPlatform();
            Silk.NET.Input.Glfw.GlfwInput.RegisterPlatform();
            Silk.NET.Windowing.Glfw.GlfwWindowing.Use();
            WindowOptions desc = WindowOptions.Default;
            desc.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 5));
            desc.IsEventDriven = false;
            desc.ShouldSwapAutomatically = false;
            desc.WindowState = WindowState.Maximized;
            desc.VSync = true;
            _window = Window.Create(desc);
            _window.Initialize();
            _gl = _window.CreateOpenGL();
            _input = _window.CreateInput();
            _render = new RenderManager(this, _gl);
            _gui = new ImguiManager(this, _gl, _window, _input);
            _workingDir = string.Empty;

            _lastUpdate = DateTime.Now;

            var glfw = Silk.NET.Windowing.Glfw.GlfwWindowing.GetExistingApi(_window);
            glfw!.SwapInterval(1); //TODO: hard code vsync.

            _camera = new EditorCamera
            {
                Origin = new System.Numerics.Vector3(2, 2, 2),
                Target = new System.Numerics.Vector3(0, 0, 0),
                Up = new System.Numerics.Vector3(0, 1, 0),
                Fov = 60,
                Near = 0.001f,
                Far = 10
            };
        }

        private void CollectEditorComponentType()
        {
            _comTypeList.Add(typeof(Transform));
            _comTypeList.Add(typeof(MeshRenderer));
        }

        public void Run()
        {
            while (!_window.IsClosing)
            {
                _scene.OnUpdate(DeltaTime);
                _render.OnUpdate();
                _render.OnRender();
                _gui.OnRender(DeltaTime);
                _scene.AfterUpdate();

                _window.DoEvents();
                _window.SwapBuffers();

                DateTime now = DateTime.Now;
                _deltaTime = (float)(now - _lastUpdate).TotalMilliseconds;
                _lastUpdate = now;
            }
            _window.Close();
            _window.Dispose();
        }

        public void OpenDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Logger.Error($"[App] Can't open {path}");
            }
            _workingDir = path;
            Asset.Reset();
            Scene.Reset();
            Gui.Reset();
            _hasWorkSpace = true;
            Logger.Info($"[App] Open directory {path}");
        }

        public AssetEntryReference<T> GetAssetReference<T>() where T : AssetEntry
        {
            return new AssetEntryReference<T>(Asset, null);
        }
    }
}
