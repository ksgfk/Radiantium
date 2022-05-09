using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radiantium.Editor
{
    public class EditorApplication
    {
        readonly AssetManager _asset;
        readonly SceneManager _scene;
        readonly RenderManager _render;
        readonly ImguiManager _gui;
        readonly GL _gl;
        readonly IWindow _window;
        readonly IInputContext _input;

        DateTime _lastUpdate;
        float _deltaTime;

        public float DeltaTime => _deltaTime;
        public AssetManager Asset => _asset;
        public SceneManager Scene => _scene;
        public RenderManager Render => _render;
        public ImguiManager Gui => _gui;

        public EditorApplication()
        {
            _asset = new AssetManager(".");
            _scene = new SceneManager();
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
            _render = new RenderManager(_gl);
            _gui = new ImguiManager(this, _gl, _window, _input);

            _lastUpdate = DateTime.Now;

            var glfw = Silk.NET.Windowing.Glfw.GlfwWindowing.GetExistingApi(_window);
            glfw!.SwapInterval(1); //TODO: hard code vsync.
        }

        public void Run()
        {
            while (!_window.IsClosing)
            {
                _scene.OnUpdate(DeltaTime);

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
    }
}
