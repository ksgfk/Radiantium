namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class OpenGLException : Exception
    {
        public OpenGLException()
        {
        }

        public OpenGLException(string? message) : base(message)
        {
        }

        public OpenGLException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
