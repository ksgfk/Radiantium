using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public abstract class ObjectOpenGL : IDisposable
    {
        protected readonly GL _gl;
        protected uint _handle;

        public uint Handle => _handle;

        protected ObjectOpenGL(GL gl)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        }

        ~ObjectOpenGL() { Destroy(); }

        public abstract void Destroy();

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            Destroy();
        }
    }
}
