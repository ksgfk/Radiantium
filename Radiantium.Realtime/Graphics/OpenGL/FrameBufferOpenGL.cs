using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class FrameBufferOpenGL : ObjectOpenGL
    {
        public bool IsComplete
        {
            get
            {
                return _gl.CheckNamedFramebufferStatus(_handle, FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
            }
        }

        public FrameBufferOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateFramebuffer();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteFramebuffer(_handle);
                _handle = 0;
                DebugOpenGL.Check(_gl);
            }
        }

        public void Attach(FramebufferAttachment attachment, Texture2DOpenGL texture2d, int level = 0)
        {
            _gl.NamedFramebufferTexture(_handle, attachment, texture2d.Handle, level);
            DebugOpenGL.Check(_gl);
        }

        public void Attach(FramebufferAttachment attachment, RenderBufferOpenGL rbo)
        {
            _gl.NamedFramebufferRenderbuffer(_handle, attachment, RenderbufferTarget.Renderbuffer, rbo.Handle);
            DebugOpenGL.Check(_gl);
        }

        public void Bind(FramebufferTarget target = FramebufferTarget.Framebuffer)
        {
            _gl.BindFramebuffer(target, _handle);
            DebugOpenGL.Check(_gl);
        }

        public void Unbind(FramebufferTarget target = FramebufferTarget.Framebuffer)
        {
            _gl.BindFramebuffer(target, 0);
            DebugOpenGL.Check(_gl);
        }
    }
}
