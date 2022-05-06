using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class RenderBufferOpenGL : ObjectOpenGL
    {
        public RenderBufferOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateRenderbuffer();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteRenderbuffer(_handle);
                _handle = 0;
                DebugOpenGL.Check(_gl);
            }
        }

        public void SetFormat(InternalFormat format, uint width, uint height)
        {
            _gl.NamedRenderbufferStorage(_handle, format, width, height);
            DebugOpenGL.Check(_gl);
        }
    }
}
