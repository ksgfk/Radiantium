using Silk.NET.OpenGL;
using System.Runtime.InteropServices;
using System.Text;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class ProgramOpenGL : ObjectOpenGL
    {
        public ProgramOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateProgram();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteProgram(_handle);
                DebugOpenGL.Check(_gl);
                _handle = 0;
            }
        }

        public void Bind()
        {
            _gl.UseProgram(_handle);
            DebugOpenGL.Check(_gl);
        }

        public void Unbind()
        {
            DebugOpenGL.CheckNowProgram(_gl, _handle);
            _gl.UseProgram(0);
            DebugOpenGL.Check(_gl);
        }

        public void UniformBlockBinding(uint uboIndex, uint bindingPoint)
        {
            _gl.UniformBlockBinding(_handle, uboIndex, bindingPoint);
            DebugOpenGL.Check(_gl);
        }

        public void Link(ShaderOpenGL[] shaders)
        {
            foreach (var shader in shaders)
            {
                _gl.AttachShader(_handle, shader.Handle);
                DebugOpenGL.Check(_gl);
            }
            _gl.LinkProgram(_handle);
            DebugOpenGL.Check(_gl);
            _gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int status);
            DebugOpenGL.Check(_gl);
            try
            {
                if (status != (int)GLEnum.True)
                {
                    string log = _gl.GetProgramInfoLog(_handle);
                    DebugOpenGL.Check(_gl);
                    throw new OpenGLException($"program link fail\nLink Log:\n{log}");
                }
            }
            finally
            {
                foreach (var shader in shaders)
                {
                    _gl.DetachShader(_handle, shader.Handle);
                    DebugOpenGL.Check(_gl);
                }
            }
        }

        public AttributeOpenGL[] ReflectAttributes()
        {
            _gl.GetProgram(_handle, ProgramPropertyARB.ActiveAttributes, out int attribCnt);
            DebugOpenGL.Check(_gl);
            _gl.GetProgram(_handle, ProgramPropertyARB.ActiveAttributeMaxLength, out int nameLen);
            DebugOpenGL.Check(_gl);
            AttributeOpenGL[] attributes = new AttributeOpenGL[attribCnt];
            byte[] nameBuffer = new byte[nameLen];
            for (uint i = 0; i < attribCnt; i++)
            {
                _gl.GetActiveAttrib(_handle, i,
                    out uint nameLength,
                    out int size,
                    out AttributeType type,
                    new Span<byte>(nameBuffer));
                DebugOpenGL.Check(_gl);
                int location = _gl.GetAttribLocation(_handle, new ReadOnlySpan<byte>(nameBuffer));
                DebugOpenGL.Check(_gl);
                string name = Encoding.UTF8.GetString(nameBuffer, 0, (int)nameLength);
                var (vType, vSize) = MapToVertexAttributeType(type);
                AttributeOpenGL va = new AttributeOpenGL(name, (uint)location, vType, vSize, size);
                attributes[i] = va;
            }
            return attributes;
        }

        public UniformOpenGL[] ReflectUniforms()
        {
            _gl.GetProgram(_handle, ProgramPropertyARB.ActiveUniforms, out int uniCnt);
            DebugOpenGL.Check(_gl);
            _gl.GetProgram(_handle, ProgramPropertyARB.ActiveUniformMaxLength, out int nameLen);
            DebugOpenGL.Check(_gl);
            UniformOpenGL[] uniforms = new UniformOpenGL[uniCnt];
            byte[] nameBuffer = new byte[nameLen];
            for (uint i = 0; i < uniCnt; i++)
            {
                _gl.GetActiveUniform(_handle, i,
                    out uint nameLength,
                    out int size,
                    out UniformType type,
                    new Span<byte>(nameBuffer));
                DebugOpenGL.Check(_gl);
                int location = _gl.GetUniformLocation(_handle, new ReadOnlySpan<byte>(nameBuffer));
                DebugOpenGL.Check(_gl);
                string name = Encoding.UTF8.GetString(nameBuffer, 0, (int)nameLength);
                int index = name.IndexOf("[0]");
                if (index >= 0)
                {
                    name = name.Remove(index);
                }
                UniformOpenGL su = new UniformOpenGL(name, location, type, size);
                uniforms[i] = su;
            }
            return uniforms;
        }

        public UniformBlockOpenGL[] ReflectUniformBlocks()
        {
            _gl.GetProgram(_handle, ProgramPropertyARB.ActiveUniformBlocks, out int blockCnt);
            DebugOpenGL.Check(_gl);
            UniformBlockOpenGL[] blocks = new UniformBlockOpenGL[blockCnt];
            for (uint i = 0; i < blockCnt; i++)
            {
                _gl.GetActiveUniformBlock(_handle, i, UniformBlockPName.UniformBlockNameLength, out int nameLen);
                DebugOpenGL.Check(_gl);
                byte[] blockNameBuffer = new byte[nameLen];
                _gl.GetActiveUniformBlock(_handle, i, UniformBlockPName.UniformBlockActiveUniforms, out int uniCnt);
                DebugOpenGL.Check(_gl);
                _gl.GetActiveUniformBlock(_handle, i, UniformBlockPName.UniformBlockDataSize, out int dataSize);
                DebugOpenGL.Check(_gl);
                _gl.GetActiveUniformBlockName(_handle, i, out uint nameRelLen, new Span<byte>(blockNameBuffer));
                DebugOpenGL.Check(_gl);
                uint index = _gl.GetUniformBlockIndex(_handle, new ReadOnlySpan<byte>(blockNameBuffer));
                DebugOpenGL.Check(_gl);
                int[] uniformIndices = new int[uniCnt];
                _gl.GetActiveUniformBlock(_handle, i, UniformBlockPName.UniformBlockActiveUniformIndices, new Span<int>(uniformIndices));
                DebugOpenGL.Check(_gl);
                int[] uniformNameLen = new int[uniCnt];
                int[] uniformType = new int[uniCnt];
                int[] uniformSize = new int[uniCnt];
                int[] uniformOffset = new int[uniCnt];
                _gl.GetActiveUniforms(_handle, (uint)uniCnt, MemoryMarshal.Cast<int, uint>(new ReadOnlySpan<int>(uniformIndices)), UniformPName.UniformNameLength, new Span<int>(uniformNameLen));
                DebugOpenGL.Check(_gl);
                _gl.GetActiveUniforms(_handle, (uint)uniCnt, MemoryMarshal.Cast<int, uint>(new ReadOnlySpan<int>(uniformIndices)), UniformPName.UniformType, new Span<int>(uniformType));
                DebugOpenGL.Check(_gl);
                _gl.GetActiveUniforms(_handle, (uint)uniCnt, MemoryMarshal.Cast<int, uint>(new ReadOnlySpan<int>(uniformIndices)), UniformPName.UniformSize, new Span<int>(uniformSize));
                DebugOpenGL.Check(_gl);
                _gl.GetActiveUniforms(_handle, (uint)uniCnt, MemoryMarshal.Cast<int, uint>(new ReadOnlySpan<int>(uniformIndices)), UniformPName.UniformOffset, new Span<int>(uniformOffset));
                DebugOpenGL.Check(_gl);
                UniformBlockOpenGL.Member[] members = new UniformBlockOpenGL.Member[uniCnt];
                for (int j = 0; j < uniCnt; j++)
                {
                    byte[] uniformName = new byte[uniformNameLen[j]];
                    _gl.GetActiveUniformName(_handle, (uint)uniformIndices[j], out uint realLen, new Span<byte>(uniformName));
                    DebugOpenGL.Check(_gl);
                    string name = Encoding.UTF8.GetString(uniformName, 0, (int)realLen);
                    int arrIdx = name.IndexOf("[0]");
                    if (arrIdx >= 0)
                    {
                        name = name.Remove(arrIdx);
                    }
                    if (!Enum.IsDefined((UniformType)uniformType[j]))
                    {
                        throw new ArgumentException("internal error. unknown enum value");
                    }
                    members[j] = new(name, uniformIndices[j], (UniformType)uniformType[j], uniformSize[j], uniformOffset[j], 0);
                }
                Array.Sort(members, UniformBlockMemberComparer.Default);
                for (int j = 0; j < uniCnt; j++)
                {
                    if (members[j].Size > 1)
                    {
                        if (j + 1 == uniCnt)
                        {
                            members[j].Align = (dataSize - members[j].Offset) / members[j].Size;
                        }
                        else
                        {
                            members[j].Align = (members[j + 1].Offset - members[j].Offset) / members[j].Size;
                        }
                    }
                }
                blocks[i] = new UniformBlockOpenGL(Encoding.UTF8.GetString(blockNameBuffer, 0, (int)nameRelLen), index, dataSize, members);
            }
            return blocks;
        }

        public static (VertexAttribType, int) MapToVertexAttributeType(AttributeType type)
        {
            return type switch
            {
                AttributeType.Int => (VertexAttribType.Int, 1),
                AttributeType.UnsignedInt => (VertexAttribType.UnsignedInt, 1),
                AttributeType.Float => (VertexAttribType.Float, 1),
                AttributeType.Double => (VertexAttribType.Double, 1),
                AttributeType.FloatVec2 => (VertexAttribType.Float, 2),
                AttributeType.FloatVec3 => (VertexAttribType.Float, 3),
                AttributeType.FloatVec4 => (VertexAttribType.Float, 4),
                AttributeType.IntVec2 => (VertexAttribType.Int, 2),
                AttributeType.IntVec3 => (VertexAttribType.Int, 3),
                AttributeType.IntVec4 => (VertexAttribType.Int, 4),
                AttributeType.UnsignedIntVec2 => (VertexAttribType.UnsignedInt, 2),
                AttributeType.UnsignedIntVec3 => (VertexAttribType.UnsignedInt, 3),
                AttributeType.UnsignedIntVec4 => (VertexAttribType.UnsignedInt, 4),
                AttributeType.DoubleVec2 => (VertexAttribType.Double, 2),
                AttributeType.DoubleVec3 => (VertexAttribType.Double, 3),
                AttributeType.DoubleVec4 => (VertexAttribType.Double, 4),
                _ => throw new NotImplementedException(),
            };
        }
    }
}
