using ImGuiNET;
using Radiantium.Core;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Text;

namespace Radiantium.Editor
{
    public class ImguiManager : IDisposable
    {
        public static string PanelHierarchy = "Hierarchy";
        public static string PanelInspector = "Inspector";
        public static string PanelScene = "Scene";
        public static string PanelProject = "Project";

        readonly EditorApplication _app;
        readonly GL _gl;
        readonly IView _view;
        readonly ImGuiController _ctrl;
        readonly Stack<SceneObject> _dfsStack;

        bool _disposed;
        bool _isShowHierarchy;
        bool _isShowInspector;
        bool _isShowScene;
        bool _isShowProject;
        float _dpi;
        float _dragSpeed;

        bool _isHierarchyChangeName;
        byte[] _hierarchyChangeNameBuffer;

        SceneObject? _hierarchyChangeNameTarget = null;
        SceneObject? _hierarchySelect = null;
        SceneObject? _hierarchyDragSource = null;

        public float Dpi { get => _dpi; set => _dpi = value; }
        public float DragSpeed { get => _dragSpeed; set => _dragSpeed = value; }
        public SceneObject? HierarchySelect => _hierarchySelect;

        public ImguiManager(EditorApplication app, GL gl, IView view, IInputContext input)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _dfsStack = new Stack<SceneObject>();
            _ctrl = new ImGuiController(gl, view, input, () =>
            {
                ImGuiIOPtr io = ImGui.GetIO(); io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            });

            _dpi = 1.25f;
            _isShowHierarchy = true;
            _isShowInspector = true;
            _isShowScene = true;
            _isShowProject = true;
            _dragSpeed = 0.01f;
            _hierarchyChangeNameBuffer = new byte[64];
        }

        ~ImguiManager()
        {
            Clear();
        }

        public void OnRender(float delta)
        {
            var fb = _view.FramebufferSize;
            _gl.Viewport(0, 0, (uint)fb.X, (uint)fb.Y);
            DrawImGui(delta / 1000);
        }

        private void DrawImGui(float delta)
        {
            _ctrl.Update(delta);

            //ImGui.ShowDemoWindow();

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            viewport.DpiScale = _dpi;

            ImGui.DockSpaceOverViewport(viewport);

            if (ImGui.BeginMainMenuBar())
            {
                ImGui.SetWindowFontScale(_dpi);
                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem(PanelHierarchy, string.Empty, ref _isShowHierarchy)) { }
                    if (ImGui.MenuItem(PanelInspector, string.Empty, ref _isShowInspector)) { }
                    if (ImGui.MenuItem(PanelScene, string.Empty, ref _isShowScene)) { }
                    if (ImGui.MenuItem(PanelProject, string.Empty, ref _isShowProject)) { }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            DrawHierarchy(viewport);
            DrawInspector(viewport);
            DrawScene(viewport);
            DrawProject(viewport);

            _ctrl.Render();
        }

        private void DrawHierarchy(ImGuiViewportPtr viewport)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            if (_isShowHierarchy && ImGui.Begin(PanelHierarchy, ref _isShowHierarchy, flags))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.Button("+"))
                    {
                        _app.Scene.CreateObject("ScenObject", System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity);
                    }
                    if (ImGui.Button("-"))
                    {
                        if (_hierarchySelect != null)
                        {
                            _app.Scene.DestroyObject(_hierarchySelect);
                            _hierarchySelect = null;
                        }
                    }
                    ImGui.EndMenuBar();
                }
                if (ImGui.BeginChild("SceneObjects"))
                {
                    ImGuiTreeNodeFlags baseFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth;
                    _dfsStack.Push(_app.Scene.Root);
                    int lastDepth = 0;
                    while (_dfsStack.Count > 0)
                    {
                        SceneObject node = _dfsStack.Pop();
                        for (int i = 0; i < lastDepth - node.Depth; i++)
                        {
                            ImGui.TreePop();
                        }
                        lastDepth = node.Depth;
                        ImGuiTreeNodeFlags nodeFlag = baseFlags;
                        if (_hierarchySelect == node)
                        {
                            nodeFlag |= ImGuiTreeNodeFlags.Selected;
                        }
                        if (node.Children.Count == 0)
                        {
                            nodeFlag |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                        }
                        int hash = node.GetHashCode();
                        bool isOpen = ImGui.TreeNodeEx(new IntPtr(hash), nodeFlag, node.Name);
                        if (node != _app.Scene.Root && ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
                        {
                            _hierarchySelect = node;
                        }
                        if (isOpen)
                        {
                            foreach (SceneObject child in node.Children)
                            {
                                _dfsStack.Push(child);
                            }
                        }
                        ImGui.PushID(hash);
                        if (node != _app.Scene.Root && ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
                        {
                            ImGui.SetDragDropPayload("DFS", IntPtr.Zero, 0);
                            _hierarchyDragSource = node;
                            ImGui.EndDragDropSource();
                        }
                        if (ImGui.BeginDragDropTarget())
                        {
                            ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload("DFS");
                            unsafe
                            {
                                if (payload.NativePtr != null)
                                {
                                    SceneObject source = _hierarchyDragSource!;
                                    SceneObject target = node;
                                    source.SetParent(target);
                                    _hierarchyDragSource = null;
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
        }

        private void DrawInspector(ImGuiViewportPtr viewport)
        {
            if (_isShowInspector && ImGui.Begin(PanelInspector, ref _isShowInspector))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);
                if (_hierarchySelect != null)
                {
                    if (!_isHierarchyChangeName)
                    {
                        Encoding.UTF8.GetBytes(_hierarchySelect.Name.AsSpan(), _hierarchyChangeNameBuffer.AsSpan());
                    }
                    ImGui.InputText("name", _hierarchyChangeNameBuffer, (uint)_hierarchyChangeNameBuffer.Length);
                    bool isAct = ImGui.IsItemActive();
                    if (isAct && !_isHierarchyChangeName)
                    {
                        _isHierarchyChangeName = true;
                        _hierarchyChangeNameTarget = _hierarchySelect;
                    }
                    if (!isAct && _isHierarchyChangeName)
                    {
                        _isHierarchyChangeName = false;
                        _hierarchyChangeNameTarget!.Name = Encoding.UTF8.GetString(_hierarchyChangeNameBuffer);
                        _hierarchyChangeNameTarget = null;
                    }
                    ImGui.Separator();
                    foreach (EditorComponent com in _hierarchySelect.Components)
                    {
                        com.OnGui(_dragSpeed);
                        ImGui.Separator();
                    }
                }
                ImGui.End();
            }
        }

        private void DrawScene(ImGuiViewportPtr viewport)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            if (_isShowScene && ImGui.Begin(PanelScene, ref _isShowScene, flags))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.BeginMenu("Aspect"))
                    {
                        if (ImGui.MenuItem("0.0")) { }
                        ImGui.EndMenu();
                    }
                    ImGui.Button("Stats");
                    ImGui.EndMenuBar();
                }
                ImGui.End();
            }
        }

        private void DrawProject(ImGuiViewportPtr viewport)
        {
            if (_isShowProject && ImGui.Begin(PanelProject, ref _isShowProject))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);

                ImGui.End();
            }
        }

        public void Clear()
        {
            if (!_disposed)
            {
                _ctrl.Dispose();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Clear();
            GC.SuppressFinalize(this);
        }
    }
}
