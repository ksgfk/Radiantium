using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Numerics;
using System.Text;

namespace Radiantium.Editor
{
    public class ImguiManager : IDisposable
    {
        public class InspectorDataShower
        {
            readonly ImguiManager _gui;
            bool _isHierarchyChangeName;
            byte[] _hierarchyChangeNameBuffer = new byte[128];
            SceneObject? _hierarchySelect = null;
            SceneObject? _hierarchyChangeNameTarget = null;

            string? _projectSelect = null;

            public SceneObject? HierarchySelected => _hierarchySelect;
            public string? ProjectSelected => _projectSelect;

            public InspectorDataShower(ImguiManager gui)
            {
                _gui = gui ?? throw new ArgumentNullException(nameof(gui));
            }

            public void ReleaseHierarchy()
            {
                _hierarchySelect = null;
                _isHierarchyChangeName = false;
                Array.Clear(_hierarchyChangeNameBuffer);
                _hierarchyChangeNameTarget = null;
            }

            public void ReleaseProject()
            {
                _projectSelect = null;
            }

            public void HierarchySelect(SceneObject o)
            {
                ReleaseProject();
                _hierarchySelect = o;
            }

            public void ProjectSelect(string p)
            {
                ReleaseHierarchy();
                _projectSelect = p;
            }

            public void DrawInspector()
            {
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
                        ImGui.Text(com.GetType().Name);
                        com.OnGui(_gui._dragSpeed);
                        ImGui.Separator();
                    }
                    if (ImGui.BeginCombo("Add Com", string.Empty))
                    {
                        foreach (Type type in _gui._app.ComTypes)
                        {
                            if (ImGui.Selectable(type.Name))
                            {
                                var c = (EditorComponent)Activator.CreateInstance(type, _hierarchySelect)!;
                                _hierarchySelect.AddComponent(c);
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
                if (_projectSelect != null)
                {
                    string name = Path.GetFileName(_projectSelect);
                    string rela = Path.GetRelativePath(_gui._app.WorkingDir, _projectSelect);
                    ImGui.Text($"file name: {name}");
                    ImGui.Text($"file path: {rela}");
                    if (File.Exists(_projectSelect))
                    {
                        if (_gui._app.Asset.CanLoad(_projectSelect))
                        {
                            bool isLoaded = _gui._app.Asset.IsLoaded(rela);
                            if (isLoaded)
                            {
                                ImGui.BeginDisabled();
                            }
                            if (ImGui.Button("load"))
                            {
                                _gui._app.Asset.Load(rela);
                            }
                            if (isLoaded)
                            {
                                ImGui.EndDisabled();
                            }

                            if (!isLoaded)
                            {
                                ImGui.BeginDisabled();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("release"))
                            {
                                _gui._app.Asset.Release(rela);
                            }
                            if (!isLoaded)
                            {
                                ImGui.EndDisabled();
                            }
                        }
                    }
                    else
                    {
                        ImGui.Text($"can't find file");
                    }
                }
            }
        }

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
        bool _isShowAssets;
        float _dpi;
        float _dragSpeed;
        string _menuOpenDir;
        string _projectNowDir;
        List<string> _nowDirEntries;

        SceneObject? _hierarchyDragSource = null;
        InspectorDataShower _shower;
        string? _assetSelect;

        public float Dpi { get => _dpi; set => _dpi = value; }
        public float DragSpeed { get => _dragSpeed; set => _dragSpeed = value; }
        public SceneObject? HierarchySelect => _shower.HierarchySelected;

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
            _isShowAssets = true;
            _dragSpeed = 0.01f;
            _menuOpenDir = string.Empty;
            _projectNowDir = string.Empty;
            _shower = new InspectorDataShower(this);
            _nowDirEntries = new List<string>();
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
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.BeginMenu("Open Dir"))
                    {
                        ImGui.InputText("path", ref _menuOpenDir, 512);
                        if (ImGui.Button("Open"))
                        {
                            _app.OpenDir(_menuOpenDir);
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem("Hierarchy", string.Empty, ref _isShowHierarchy)) { }
                    if (ImGui.MenuItem("Inspector", string.Empty, ref _isShowInspector)) { }
                    if (ImGui.MenuItem("Scene", string.Empty, ref _isShowScene)) { }
                    if (ImGui.MenuItem("Project", string.Empty, ref _isShowProject)) { }
                    if (ImGui.MenuItem("Assets", string.Empty, ref _isShowAssets)) { }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Setting"))
                {
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            DrawHierarchy(viewport);
            DrawInspector(viewport);
            DrawScene(viewport);
            DrawProject(viewport);
            DrawAssets(viewport);

            _ctrl.Render();
        }

        private void DrawHierarchy(ImGuiViewportPtr viewport)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            if (_isShowHierarchy && ImGui.Begin("Hierarchy", ref _isShowHierarchy, flags))
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
                        if (_shower.HierarchySelected != null)
                        {
                            _app.Scene.DestroyObject(_shower.HierarchySelected);
                            _shower.ReleaseHierarchy();
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
                        if (_shower.HierarchySelected == node)
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
                            _shower.HierarchySelect(node);
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
            if (_isShowInspector && ImGui.Begin("Inspector", ref _isShowInspector))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);

                _shower.DrawInspector();

                ImGui.End();
            }
        }

        private void DrawScene(ImGuiViewportPtr viewport)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            if (_isShowScene && ImGui.Begin("Scene", ref _isShowScene, flags))
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

                Vector2 size = ImGui.GetWindowSize();
                float aspect = 1920 / (float)1080;
                size.Y -= 10;
                size.X = size.Y * aspect;

                ImGui.Image(new IntPtr(_app.Render.ColorBuffer.Handle), size, new Vector2(0, 1), new Vector2(1, 0));

                ImGui.End();
            }
        }

        private void DrawProject(ImGuiViewportPtr viewport)
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            if (_isShowProject && ImGui.Begin("Project", ref _isShowProject, flags))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);

                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.Button("refresh"))
                    {
                        RefreshProjectDir();
                    }
                    ImGui.EndMenuBar();
                }

                ImGui.Text($"Work Directory: {_app.WorkingDir}");
                ImGui.Text($"Now Path: {_projectNowDir}");
                ImGui.Separator();
                if (_app.HasWorkSpace)
                {
                    if (ImGui.Selectable("..", false, ImGuiSelectableFlags.AllowDoubleClick) &&
                        ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        string tryPath = Path.GetFullPath(Path.Combine(_projectNowDir, ".."));
                        string invalidPath = Path.GetFullPath(Path.Combine(_app.WorkingDir, ".."));
                        if (tryPath != invalidPath && Directory.Exists(tryPath))
                        {
                            _projectNowDir = tryPath;
                        }
                    }
                    else
                    {
                        foreach (string path in Directory.EnumerateFileSystemEntries(_projectNowDir))
                        {
                            string fileName = Path.GetFileName(path);
                            bool isSel = _shower.ProjectSelected != null && _shower.ProjectSelected == path;
                            bool isSelect = ImGui.Selectable(fileName, isSel, ImGuiSelectableFlags.AllowDoubleClick);
                            if (isSelect && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                if (Directory.Exists(path))
                                {
                                    string tryPath = Path.Combine(_projectNowDir, path);
                                    if (Directory.Exists(tryPath))
                                    {
                                        _projectNowDir = tryPath;
                                        break;
                                    }
                                }
                            }
                            else if (isSelect)
                            {
                                if (File.Exists(path))
                                {
                                    _shower.ProjectSelect(path);
                                }
                            }
                        }
                    }
                }

                ImGui.End();
            }
        }

        private void DrawAssets(ImGuiViewportPtr viewport)
        {
            if (_isShowAssets && ImGui.Begin("Assets", ref _isShowAssets))
            {
                ImGui.SetWindowFontScale(viewport.DpiScale);

                if (ImGui.BeginListBox(string.Empty, new Vector2(float.Epsilon, 10 * ImGui.GetTextLineHeightWithSpacing())))
                {
                    foreach (string name in _app.Asset.AllAssets.Keys)
                    {
                        if (ImGui.Selectable(name, _assetSelect != null && _assetSelect == name))
                        {
                            string fullPath = Path.Combine(_app.WorkingDir, name);
                            if (File.Exists(fullPath))
                            {
                                _projectNowDir = Path.GetDirectoryName(fullPath)!;
                                _shower.ProjectSelect(fullPath);
                            }
                            _assetSelect = name;
                        }
                    }
                    ImGui.EndListBox();
                }
                if (_assetSelect != null)
                {
                    if (ImGui.Button("release"))
                    {
                        _app.Asset.Release(_assetSelect);
                        _assetSelect = null;
                    }
                }

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

        public void Reset()
        {
            _projectNowDir = _app.WorkingDir;
            RefreshProjectDir();
        }

        private void RefreshProjectDir()
        {
            _nowDirEntries.Clear();
            if (Directory.Exists(_projectNowDir))
            {
                _nowDirEntries.AddRange(Directory.EnumerateFileSystemEntries(_projectNowDir));
            }
            _shower.ReleaseProject();
        }
    }
}
