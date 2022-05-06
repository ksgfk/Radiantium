using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class Texture2DOpenGL : ObjectOpenGL
    {
        public Texture2DOpenGL(GL gl) : base(gl)
        {
            _gl.CreateTextures(TextureTarget.Texture2D, 1, out _handle);
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteTexture(_handle);
                DebugOpenGL.Check(_gl);
                _handle = 0;
            }
        }

        public void SetParam(TextureWrapMode wrapS, TextureWrapMode wrapT)
        {
            _gl.TextureParameter(_handle, TextureParameterName.TextureWrapS, (int)wrapS);
            DebugOpenGL.Check(_gl);
            _gl.TextureParameter(_handle, TextureParameterName.TextureWrapT, (int)wrapT);
            DebugOpenGL.Check(_gl);
        }

        public void SetParam(TextureMagFilter magFilter)
        {
            _gl.TextureParameter(_handle, TextureParameterName.TextureMagFilter, (int)magFilter);
            DebugOpenGL.Check(_gl);
        }

        public void SetParam(TextureMinFilter migFilter)
        {
            _gl.TextureParameter(_handle, TextureParameterName.TextureMinFilter, (int)migFilter);
            DebugOpenGL.Check(_gl);
        }

        public void SetParamMaxMipmap(int level)
        {
            _gl.TextureParameter(_handle, TextureParameterName.TextureMaxLevel, level);
            DebugOpenGL.Check(_gl);
        }

        public void Storage(
            uint level, SizedInternalFormat internalForamt, uint width, uint height,
            PixelFormat imageFormat, PixelType imageType, ReadOnlySpan<byte> data)
        {
            _gl.TextureStorage2D(_handle, level, internalForamt, width, height);
            DebugOpenGL.Check(_gl);
            _gl.TextureSubImage2D(_handle, (int)level, 0, 0, width, height, imageFormat, imageType, data);
            DebugOpenGL.Check(_gl);
            GenerateMipmap();
        }

        public void Storage(uint level, SizedInternalFormat internalForamt, uint width, uint height)
        {
            _gl.TextureStorage2D(_handle, level, internalForamt, width, height);
            DebugOpenGL.Check(_gl);
        }

        public void SubImage(uint level, int x, int y, uint width, uint height, PixelFormat imageFormat, PixelType imageType, ReadOnlySpan<byte> data)
        {
            _gl.TextureSubImage2D(_handle, (int)level, x, y, width, height, imageFormat, imageType, data);
            DebugOpenGL.Check(_gl);
            GenerateMipmap();
        }

        public void Bind(uint slot)
        {
            _gl.BindTextureUnit(slot, _handle);
            DebugOpenGL.Check(_gl);
        }

        public void Unbind(uint slot)
        {
            _gl.BindTextureUnit(slot, 0);
            DebugOpenGL.Check(_gl);
        }

        public void GenerateMipmap()
        {
            _gl.GenerateTextureMipmap(_handle);
            DebugOpenGL.Check(_gl);
        }
    }
}
