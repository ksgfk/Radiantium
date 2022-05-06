using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class SamplerOpenGL : ObjectOpenGL
    {
        public SamplerOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateSampler();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteSampler(_handle);
                _handle = 0;
                DebugOpenGL.Check(_gl);
            }
        }

        public void SetParam(TextureWrapMode wrapS, TextureWrapMode wrapT)
        {
            _gl.SamplerParameter(_handle, SamplerParameterI.TextureWrapS, (int)wrapS);
            DebugOpenGL.Check(_gl);
            _gl.SamplerParameter(_handle, SamplerParameterI.TextureWrapT, (int)wrapT);
            DebugOpenGL.Check(_gl);
        }

        public void SetParam(TextureMagFilter magFilter)
        {
            _gl.SamplerParameter(_handle, SamplerParameterI.TextureMagFilter, (int)magFilter);
            DebugOpenGL.Check(_gl);
        }

        public void SetParam(TextureMinFilter migFilter)
        {
            _gl.SamplerParameter(_handle, SamplerParameterI.TextureMinFilter, (int)migFilter);
            DebugOpenGL.Check(_gl);
        }

        public void Bind(uint slot)
        {
            _gl.BindSampler(slot, _handle);
            DebugOpenGL.Check(_gl);
        }

        public void Unbind(uint slot)
        {
            _gl.BindSampler(slot, _handle);
            DebugOpenGL.Check(_gl);
        }
    }
}
