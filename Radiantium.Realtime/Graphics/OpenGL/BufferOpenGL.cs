using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class BufferOpenGL : ObjectOpenGL
    {
        public BufferOpenGL(GL gl) : base(gl)
        {
            _handle = _gl.CreateBuffer();
            DebugOpenGL.Check(_gl);
        }

        public override void Destroy()
        {
            if (_handle != 0)
            {
                _gl.DeleteBuffer(_handle);
                DebugOpenGL.Check(_gl);
                _handle = 0;
            }
        }

        public void Storage(ReadOnlySpan<byte> data, BufferStorageMask flags = 0)
        {
            _gl.NamedBufferStorage(_handle, data, flags);
            DebugOpenGL.Check(_gl);
        }

        public virtual unsafe void Storage(int size, BufferStorageMask flags = 0)
        {
            _gl.NamedBufferStorage(_handle, (uint)size, null, flags);
            DebugOpenGL.Check(_gl);
        }

        public void SubData(ReadOnlySpan<byte> data, int start)
        {
            _gl.NamedBufferSubData(_handle, start, (nuint)(data.Length * sizeof(byte)), data);
            DebugOpenGL.Check(_gl);
        }
    }

    public class VertexBufferOpenGL<T> : BufferOpenGL where T : unmanaged
    {
        public VertexBufferOpenGL(GL gl) : base(gl) { }

        public void Storage(ReadOnlySpan<T> data, BufferStorageMask flags = 0)
        {
            Storage(MemoryMarshal.AsBytes(data), flags);
        }

        public override unsafe void Storage(int size, BufferStorageMask flags = 0)
        {
            base.Storage(size * sizeof(T), flags);
        }

        public unsafe void SubData(ReadOnlySpan<T> data, int start)
        {
            SubData(MemoryMarshal.AsBytes(data), start * sizeof(T));
        }
    }

    public class ElementBufferOpenGL : BufferOpenGL
    {
        public ElementBufferOpenGL(GL gl) : base(gl) { }

        public void Storage(ReadOnlySpan<int> data, BufferStorageMask flags = 0)
        {
            Storage(MemoryMarshal.AsBytes(data), flags);
        }

        public unsafe void StorageInt32(int size, BufferStorageMask flags = 0)
        {
            Storage(size * sizeof(int), flags);
        }

        public void Storage(ReadOnlySpan<uint> data, BufferStorageMask flags = 0)
        {
            Storage(MemoryMarshal.AsBytes(data), flags);
        }

        public unsafe void StorageUInt32(int size, BufferStorageMask flags = 0)
        {
            Storage(size * sizeof(uint), flags);
        }

        public void Storage(ReadOnlySpan<ushort> data, BufferStorageMask flags = 0)
        {
            Storage(MemoryMarshal.AsBytes(data), flags);
        }

        public unsafe void StorageUInt16(int size, BufferStorageMask flags = 0)
        {
            Storage(size * sizeof(ushort), flags);
        }

        public void SubData(ReadOnlySpan<int> data, int start)
        {
            SubData(MemoryMarshal.AsBytes(data), start * sizeof(int));
        }

        public void SubData(ReadOnlySpan<uint> data, int start)
        {
            SubData(MemoryMarshal.AsBytes(data), start * sizeof(uint));
        }

        public void SubData(ReadOnlySpan<ushort> data, int start)
        {
            SubData(MemoryMarshal.AsBytes(data), start * sizeof(ushort));
        }
    }

    public class UniformBufferOpenGL<T> : BufferOpenGL where T : unmanaged
    {
        public UniformBufferOpenGL(GL gl) : base(gl) { }

        public unsafe void Storage()
        {
            Storage(sizeof(T), BufferStorageMask.DynamicStorageBit);
        }

        public void SubData(ref T data)
        {
            SubData(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref data, 1)), 0);
        }

        public void BindRange(uint index, uint offset, uint size)
        {
            _gl.BindBufferRange(BufferTargetARB.UniformBuffer, index, _handle, (int)offset, size);
            DebugOpenGL.Check(_gl);
        }

        public void BindBase(uint index)
        {
            _gl.BindBufferBase(BufferTargetARB.UniformBuffer, index, _handle);
            DebugOpenGL.Check(_gl);
        }
    }
}
