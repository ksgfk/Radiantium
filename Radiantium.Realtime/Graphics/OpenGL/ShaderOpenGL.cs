using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class ShaderOpenGL : ObjectOpenGL
    {
        public ShaderType Type { get; }

        public ShaderOpenGL(GL gl, ShaderType type) : base(gl)
        {
            Type = type;
            _handle = _gl.CreateShader(Type);
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteShader(_handle);
                DebugOpenGL.Check(_gl);
                _handle = 0;
            }
        }

        public void LoadFromSource(string src)
        {
            _gl.ShaderSource(_handle, src);
            DebugOpenGL.Check(_gl);
            _gl.CompileShader(_handle);
            DebugOpenGL.Check(_gl);
            _gl.GetShader(_handle, ShaderParameterName.CompileStatus, out int status);
            DebugOpenGL.Check(_gl);
            if (status != (int)GLEnum.True)
            {
                string log = _gl.GetShaderInfoLog(_handle);
                DebugOpenGL.Check(_gl);
                throw new OpenGLException($"shader compile fail\nCompile Log:\n{log}");
            }
        }

        public void LoadFromFile(string path)
        {
            string src = File.ReadAllText(path);
            LoadFromSource(src);
        }
    }
}
