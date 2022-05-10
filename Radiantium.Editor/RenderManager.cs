using Radiantium.Core;
using Radiantium.Realtime.Graphics.OpenGL;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Radiantium.Editor
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AppVertex
    {
        [FieldOffset(0)] public Vector3 Pos;
        [FieldOffset(12)] public Vector3 Normal;
        [FieldOffset(24)] public Vector2 UV0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ItemDrawData
    {
        [FieldOffset(0)] public Matrix4x4 ModelToWorld;
        [FieldOffset(64)] public Matrix4x4 WorldToModel;
    }

    [StructLayout(LayoutKind.Explicit, Size = 208)]
    public struct GlobalDrawData
    {
        [FieldOffset(0)] public Matrix4x4 WorldToCamera;
        [FieldOffset(64)] public Matrix4x4 CameraToProj;
        [FieldOffset(128)] public Matrix4x4 WorldToProj;
        [FieldOffset(192)] public Vector3 LightPos;
    }

    public class RenderItem
    {
        readonly RenderManager _render;
        internal ItemDrawData _drawData;
        internal UniformBufferOpenGL<ItemDrawData> _drawDataUbo;
        internal VertexBufferOpenGL<AppVertex> _vbo = null!;
        internal ElementBufferOpenGL _ebo = null!;
        internal uint _indexCount;

        public RenderItem(RenderManager render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
            _drawDataUbo = new UniformBufferOpenGL<ItemDrawData>(render._gl);
            _drawDataUbo.Storage();
        }
    }

    public class GPUMesh
    {
        internal VertexBufferOpenGL<AppVertex> Vbo;
        internal ElementBufferOpenGL Ebo;

        public GPUMesh(GL gl, TriangleModel model)
        {
            Vbo = new VertexBufferOpenGL<AppVertex>(gl);
            AppVertex[] v = new AppVertex[model.VertexCount];
            for (int i = 0; i < model.VertexCount; i++)
            {
                v[i].Pos = model.Position[i];
                if (model.Normal != null)
                {
                    v[i].Normal = model.Normal[i];
                }
                if (model.UV != null)
                {
                    v[i].UV0 = model.UV[i];
                }
            }
            Vbo.Storage(v.AsSpan());

            Ebo = new ElementBufferOpenGL(gl);
            Ebo.Storage(model.Indices.AsSpan());
        }

        public void Release()
        {
            Vbo.Destroy();
            Ebo.Destroy();
        }
    }

    public class RenderManager
    {
        private static readonly string Vs = @"#version 450 core
layout(location = 0) in vec3 a_Pos;
layout(location = 1) in vec3 a_Normal;
layout(location = 2) in vec2 a_UV0;

layout(location = 0) out vec3 v_Normal;

layout(std140, binding = 0) uniform _ItemData {
    mat4 modelToWorld;
    mat4 worldToModel;
};

layout(std140, binding = 1) uniform _GlobalData {
    mat4 worldToCamera;
    mat4 cameraToProj;
    mat4 worldToProj;
    vec3 lightPos;
};

void main() {
    mat4 mvp = modelToWorld * worldToProj;
    gl_Position = mvp * vec4(a_Pos, 1.0f);
    v_Normal = mat3(mvp) * a_Normal;
}
";
        private static readonly string Fs = @"#version 450 core
layout(location = 0) in vec3 v_Normal;
layout(location = 0) out vec4 f_Color;

layout(std140, binding = 1) uniform _GlobalData {
    mat4 worldToCamera;
    mat4 cameraToProj;
    mat4 worldToProj;
    vec3 lightPos;
};

void main() {
    vec3 normal = normalize(v_Normal);
    float cosTheta = dot(normal, lightPos);
    f_Color = vec4(vec3(clamp(cosTheta, 0, 1)), 1.0f);
}
";

        readonly EditorApplication _app;
        internal readonly GL _gl;
        readonly List<RenderItem> _renderItem;
        readonly Dictionary<string, GPUMesh> _gpuMeshes;
        readonly List<string> _rmMesh;

        ProgramOpenGL _proj;
        VertexArrayOpenGL _vao;
        FrameBufferOpenGL _fbo;
        Texture2DOpenGL _color;
        Texture2DOpenGL _depth;
        GlobalDrawData _global;
        UniformBufferOpenGL<GlobalDrawData> _globalUbo;

        public Texture2DOpenGL ColorBuffer => _color;
        public Texture2DOpenGL DepthBuffer => _depth;

        public IReadOnlyDictionary<string, GPUMesh> Meshes => _gpuMeshes;

        unsafe public RenderManager(EditorApplication app, GL gl)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _renderItem = new List<RenderItem>();
            _gpuMeshes = new Dictionary<string, GPUMesh>();
            _rmMesh = new List<string>();

            byte* version = _gl.GetString(StringName.Version);
            byte* renderer = _gl.GetString(StringName.Renderer);
            byte* vendor = _gl.GetString(StringName.Vendor);
            byte* shaderVer = _gl.GetString(StringName.ShadingLanguageVersion);

            string ver = Encoding.UTF8.GetString(version, Strlen(version));
            string rer = Encoding.UTF8.GetString(renderer, Strlen(renderer));
            string ven = Encoding.UTF8.GetString(vendor, Strlen(vendor));
            string sha = Encoding.UTF8.GetString(shaderVer, Strlen(shaderVer));

            Logger.Info($"OpenGL Version: {ver}");
            Logger.Info($"Renderer: {rer}");
            Logger.Info($"Driver Vendor: {ven}");
            Logger.Info($"GLSL Version: {sha}");

            static int Strlen(byte* str)
            {
                int len = 0;
                while (str[len] != '\0') { len++; }
                return len;
            }

            _proj = new ProgramOpenGL(gl);
            using ShaderOpenGL vs = new ShaderOpenGL(gl, ShaderType.VertexShader);
            vs.LoadFromSource(Vs);
            using ShaderOpenGL fs = new ShaderOpenGL(gl, ShaderType.FragmentShader);
            fs.LoadFromSource(Fs);
            _proj.Link(new ShaderOpenGL[] { vs, fs });

            _vao = new VertexArrayOpenGL(gl);
            _vao.SetAttribFormat(0, VertexAttribType.Float, 3, false, 0);
            _vao.SetAttribFormat(1, VertexAttribType.Float, 3, false, 12);
            _vao.SetAttribFormat(2, VertexAttribType.Float, 2, false, 24);
            _vao.SetAttribBindingPoint(0, 0);
            _vao.SetAttribBindingPoint(1, 0);
            _vao.SetAttribBindingPoint(2, 0);
            _vao.EnableAttrib(0);
            _vao.EnableAttrib(1);
            _vao.EnableAttrib(2);

            _color = new Texture2DOpenGL(gl);
            _color.SetParam(TextureMagFilter.Nearest);
            _color.SetParam(TextureMinFilter.Nearest);
            _color.SetParam(TextureWrapMode.Repeat, TextureWrapMode.Repeat);
            _color.SetParamMaxMipmap(1);
            _color.Storage(1, SizedInternalFormat.Rgba8, 1920, 1080);
            _depth = new Texture2DOpenGL(gl);
            _depth.SetParam(TextureMagFilter.Nearest);
            _depth.SetParam(TextureMinFilter.Nearest);
            _depth.SetParam(TextureWrapMode.Repeat, TextureWrapMode.Repeat);
            _depth.SetParamMaxMipmap(1);
            _depth.Storage(1, SizedInternalFormat.Depth24Stencil8, 1920, 1080);

            _fbo = new FrameBufferOpenGL(gl);
            _fbo.Attach(FramebufferAttachment.ColorAttachment0, _color);
            _fbo.Attach(FramebufferAttachment.DepthStencilAttachment, _depth);
            if (!_fbo.IsComplete)
            {
                throw new InvalidOperationException();
            }

            _globalUbo = new UniformBufferOpenGL<GlobalDrawData>(gl);
            _globalUbo.Storage();

            //_proj.UniformBlockBinding(0, 0);
            //_proj.UniformBlockBinding(1, 1);
            _globalUbo.BindBase(1);

            _global.LightPos = Vector3.Normalize(new Vector3(1, 1, -1));
        }

        public void OnUpdate()
        {
            foreach (var m in _app.Asset.Models)
            {
                if (!_gpuMeshes.ContainsKey(m.MyPath))
                {
                    _gpuMeshes.Add(m.MyPath, new GPUMesh(_gl, m.Model));
                }
            }
            foreach (var m in _gpuMeshes)
            {
                if (!_app.Asset.AllAssets.ContainsKey(m.Key))
                {
                    _rmMesh.Add(m.Key);
                }
            }
            foreach (var rm in _rmMesh)
            {
                _gpuMeshes.Remove(rm, out var m);
                m!.Release();
            }
            _rmMesh.Clear();

            _renderItem.Clear();
            foreach (var o in _app.Scene.AllObjects)
            {
                MeshRenderer? mr = o.GetComponent<MeshRenderer>();
                if (mr != null && mr.Model.IsValid)
                {
                    mr.UpdateRenderItem();
                    _renderItem.Add(mr.Ri);
                }
            }
        }

        public unsafe void OnRender()
        {
            _global.WorldToCamera = _app.Camera.WorldToCamera();
            _global.CameraToProj = _app.Camera.CameraToProject(1920 / (float)1080);
            _global.WorldToProj = _global.WorldToCamera * _global.CameraToProj;
            _globalUbo.SubData(ref _global);

            _fbo.Bind(FramebufferTarget.Framebuffer);
            _gl.ClearColor(0.1f, 0.1f, 0.25f, 1);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _proj.Bind();
            _vao.Bind();
            _gl.Enable(EnableCap.DepthTest);
            _gl.Viewport(0, 0, 1920, 1080);

            foreach (var ri in _renderItem)
            {
                ri._drawDataUbo.BindBase(0);
                _vao.BindVertexBuffer(ri._vbo, 0, 0, (uint)sizeof(AppVertex));
                _vao.BindElementBuffer(ri._ebo);
                _gl.DrawElements(PrimitiveType.Triangles, ri._indexCount, DrawElementsType.UnsignedInt, null);
            }

            _fbo.Unbind();
        }
    }
}
