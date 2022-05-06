using Silk.NET.OpenGL;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public static class DebugOpenGL
    {
        [Conditional("DEBUG")]
        public static void Check(GL gl,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int line = 0)
        {
            GLEnum err = gl.GetError();
            if (err == GLEnum.NoError)
            {
                return;
            }
            string msg = err switch
            {
                GLEnum.InvalidEnum => "Invalid Enum",
                GLEnum.InvalidValue => "Invalid Value",
                GLEnum.InvalidOperation => "Invalid Operation",
                GLEnum.InvalidFramebufferOperation => "Invalid Framebuffer Operation",
                GLEnum.OutOfMemory => "Out Of Memory",
                GLEnum.StackOverflow => "Stack Overflow",
                GLEnum.StackUnderflow => "Stack Underflow",
                _ => "Unknown Error"
            };
            string errorMsg = $"OGL Error: {msg}\n    at {memberName}\n    in {sourceFile}, line {line}";
            throw new OpenGLException(errorMsg);
        }

        [Conditional("DEBUG")]
        public static void CheckNowVertexArray(GL gl, uint handle,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int line = 0)
        {
            gl.GetInteger(GetPName.VertexArrayBinding, out int current);
            if (handle != current)
            {
                string errorMsg = $"Invalid state\n    at {memberName}\n    in {sourceFile}, line {line}";
                throw new OpenGLException(errorMsg);
            }
        }

        [Conditional("DEBUG")]
        public static void CheckNowProgram(GL gl, uint handle,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int line = 0)
        {
            gl.GetInteger(GetPName.CurrentProgram, out int current);
            if (handle != current)
            {
                string errorMsg = $"Invalid state\n    at {memberName}\n    in {sourceFile}, line {line}";
                throw new OpenGLException(errorMsg);
            }
        }
    }
}
