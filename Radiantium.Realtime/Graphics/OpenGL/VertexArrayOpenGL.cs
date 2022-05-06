using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class VertexArrayOpenGL : ObjectOpenGL
    {
        public VertexArrayOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateVertexArray();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteVertexArray(_handle);
                DebugOpenGL.Check(_gl);
                _handle = 0;
            }
        }

        public void SetAttribBindingPoint(uint attribLocation, uint bindingPoint)
        {
            _gl.VertexArrayAttribBinding(_handle, attribLocation, bindingPoint);
            DebugOpenGL.Check(_gl);
        }

        public void SetAttribFormat(uint attribLocation,
            VertexAttribType type, int size, bool isNormalize,
            uint relativeOffset)
        {
            _gl.VertexArrayAttribFormat(_handle, attribLocation, size, type, isNormalize, relativeOffset);
            DebugOpenGL.Check(_gl);
        }

        public void EnableAttrib(uint attribLocation)
        {
            _gl.EnableVertexArrayAttrib(_handle, attribLocation);
            DebugOpenGL.Check(_gl);
        }

        public void BindVertexBuffer(BufferOpenGL buffer, uint bindingPoint, int start, uint stride)
        {
            _gl.VertexArrayVertexBuffer(_handle, bindingPoint, buffer.Handle, start, stride);
            DebugOpenGL.Check(_gl);
        }

        public void BindElementBuffer(BufferOpenGL buffer)
        {
            _gl.VertexArrayElementBuffer(_handle, buffer.Handle);
            DebugOpenGL.Check(_gl);
        }

        public void Bind()
        {
            _gl.BindVertexArray(_handle);
            DebugOpenGL.Check(_gl);
        }

        public void Unbind()
        {
            DebugOpenGL.CheckNowVertexArray(_gl, _handle);
            _gl.BindVertexArray(0);
            DebugOpenGL.Check(_gl);
        }
    }
}
