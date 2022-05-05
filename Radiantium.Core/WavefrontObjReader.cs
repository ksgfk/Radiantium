using System.Numerics;
using System.Text;

namespace Radiantium.Core
{
    public class WavefrontObjReader : IDisposable
    {
        public class ModelObject
        {
            public string Name { get; }
            public string Material { get; internal set; }
            public List<int> Faces { get; }
            internal ModelObject(string name)
            {
                Name = name;
                Faces = new List<int>();
                Material = string.Empty;
            }
        }
        public readonly struct Index3
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            internal Index3(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
        public readonly struct Face
        {
            public readonly Index3 Vertex;
            public readonly Index3 TexCoord;
            public readonly Index3 Normal;
            internal Face(Index3 vertex, Index3 texCoord, Index3 normal)
            {
                Vertex = vertex;
                TexCoord = texCoord;
                Normal = normal;
            }
        }

        readonly TextReader _reader;
        readonly List<Vector3> _vertices;
        readonly List<Vector3> _normals;
        readonly List<Vector2> _texCoords;
        readonly List<Face> _faces;
        readonly List<ModelObject> _objs;
        readonly List<string> _mtlsFile;
        readonly StringBuilder _error;
        string? _errorInfo;
        bool _disposed;
        Task _parseTask;

        public string ErrorInfo
        {
            get
            {
                if (_errorInfo == null)
                {
                    _errorInfo = _error.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(_errorInfo))
                    {
                        _errorInfo = string.Empty;
                    }
                }
                return _errorInfo;
            }
        }

        public List<Vector3> Vertices => _vertices;
        public List<Vector3> Normals => _normals;
        public List<Vector2> TexCoords => _texCoords;
        public List<Face> Faces => _faces;
        public List<ModelObject> Objects => _objs;
        public List<string> MtlFileNames => _mtlsFile;

        public WavefrontObjReader(Stream stream) : this(new StreamReader(stream, Encoding.UTF8)) { }

        public WavefrontObjReader(string sourceStr) : this(new StringReader(sourceStr)) { }

        public WavefrontObjReader(TextReader reader)
        {
            _reader = reader;
            _vertices = new List<Vector3>();
            _normals = new List<Vector3>();
            _texCoords = new List<Vector2>();
            _faces = new List<Face>();
            _objs = new List<ModelObject>();
            _mtlsFile = new List<string>();
            _error = new StringBuilder();
            _disposed = false;
            _parseTask = Task.CompletedTask;
        }

        public void Read()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("this reader is disposed");
            }
            var allLine = 0;
            try
            {
                while (_reader.Peek() != -1)
                {
                    var line = _reader.ReadLine();
                    allLine++;
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    Parse(line, allLine);
                }
            }
            catch (Exception e)
            {
                _error.Append("line ").Append(allLine).Append(": ").Append(e.Message);
            }
        }

        public Task ReadAsync()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("this reader is disposed");
            }
            if (!_parseTask.IsCompleted)
            {
                throw new InvalidOperationException("this reader reading asynchronously");
            }
            _parseTask = ReadAsyncInternal();
            return _parseTask;
        }

        private async Task ReadAsyncInternal()
        {
            var allLine = 0;
            try
            {
                while (_reader.Peek() != -1)
                {
                    var line = await _reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    allLine++;
                    Parse(line, allLine);
                }
            }
            catch (Exception e)
            {
                _error.Append("line ").Append(allLine).Append(": ").Append(e.Message);
            }
        }

        public static bool TrySplit3(ReadOnlySpan<char> span, char sep, out (int, int) result)
        {
            Span<int> b = stackalloc int[2];
            var bCnt = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if (c == sep)
                {
                    if (bCnt < 2)
                    {
                        b[bCnt] = i;
                    }
                    bCnt++;
                }
            }
            var succ = bCnt >= 2;
            result = (b[0], b[1]);
            return succ;
        }

        public static bool TryReadVector2(ReadOnlySpan<char> span, out Vector2 result)
        {
            var bPos = -1;
            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if (c == ' ')
                {
                    bPos = i;
                    break;
                }
            }
            var succ = bPos > 0;
            if (succ)
            {
                var x = span[..bPos];
                var y = span[(bPos + 1)..];
                succ &= float.TryParse(x, out var rx);
                succ &= float.TryParse(y, out var ry);
                result = succ ? new Vector2(rx, ry) : Vector2.Zero;
            }
            else
            {
                result = Vector2.Zero;
            }
            return succ;
        }

        public static bool TryReadVector3(ReadOnlySpan<char> span, out Vector3 result)
        {
            var succ = TrySplit3(span, ' ', out var b);
            var b0 = b.Item1;
            var b1 = b.Item2;
            if (succ)
            {
                var x = span[..b0];
                var y = span.Slice(b0 + 1, b1 - b0 - 1);
                var z = span.Slice(b1 + 1, span.Length - 1 - b1);
                succ &= float.TryParse(x, out var rx);
                succ &= float.TryParse(y, out var ry);
                succ &= float.TryParse(z, out var rz);
                result = succ ? new Vector3(rx, ry, rz) : Vector3.Zero;
            }
            else
            {
                result = Vector3.Zero;
            }
            return succ;
        }

        public static bool TryReadFaceVertex(ReadOnlySpan<char> span, out Index3 index)
        {
            var succ = TrySplit3(span, '/', out var b);
            var b0 = b.Item1;
            var b1 = b.Item2;
            if (succ)
            {
                var v = span[..b0];
                var vt = span.Slice(b0 + 1, b1 - b0 - 1);
                var vn = span.Slice(b1 + 1, span.Length - 1 - b1);
                succ &= int.TryParse(v, out var rv);
                int rvt;
                if (b1 - b0 == 1)
                {
                    rvt = 0;
                }
                else
                {
                    succ &= int.TryParse(vt, out rvt);
                }
                succ &= int.TryParse(vn, out var rvn);
                index = succ ? new Index3(rv, rvt, rvn) : default;
            }
            else
            {
                var symbol = span.IndexOf('/');
                if (symbol > 0)
                {
                    succ = true;
                    var v = span[..symbol];
                    var vt = span[(symbol + 1)..];
                    succ &= int.TryParse(v, out var rv);
                    succ &= int.TryParse(vt, out var rvt);
                    index = succ ? new Index3(rv, rvt, -1) : default;
                }
                else
                {
                    succ = true;
                    succ &= int.TryParse(span, out var rv);
                    index = succ ? new Index3(rv, -1, -1) : default;
                }
            }
            return succ;
        }

        public static bool TryReadFace(ReadOnlySpan<char> span, out Face face)
        {
            var succ = TrySplit3(span, ' ', out var b);
            var b0 = b.Item1;
            var b1 = b.Item2;
            if (succ)
            {
                var x = span[..b0];
                var y = span.Slice(b0 + 1, b1 - b0 - 1);
                var z = span.Slice(b1 + 1, span.Length - 1 - b1);
                succ &= TryReadFaceVertex(x, out var rx);
                succ &= TryReadFaceVertex(y, out var ry);
                succ &= TryReadFaceVertex(z, out var rz);
                var v = new Index3(rx.X - 1, ry.X - 1, rz.X - 1);
                var vt = new Index3(rx.Y - 1, ry.Y - 1, rz.Y - 1);
                var vn = new Index3(rx.Z - 1, ry.Z - 1, rz.Z - 1);
                face = succ ? new Face(v, vt, vn) : default;
            }
            else
            {
                face = default;
            }
            return succ;
        }

        private void Parse(string line, int lineNum)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#') //comment
            {
                return;
            }
            if (line.StartsWith("o ") || line.StartsWith("g "))
            {
                var start = IndexOfNoWhiteSpace(line, 1);
                var name = line[start..];
                if (_objs.FindIndex(x => x.Name == name) >= 0)
                {
                    _error.Append("line ").Append(lineNum).Append(": duplicate object name ").Append(name).Append('\n');
                }
                else
                {
                    _objs.Add(new ModelObject(name));
                }
            }
            else if (line.StartsWith("v "))
            {
                var start = IndexOfNoWhiteSpace(line, 1);
                if (TryReadVector3(line.AsSpan()[start..], out var v))
                {
                    _vertices.Add(v);
                }
                else
                {
                    _error.Append("line ").Append(lineNum).Append(": can't parse pos, use default (0, 0, 0)\n");
                    _vertices.Add(Vector3.Zero);
                }
            }
            else if (line.StartsWith("f "))
            {
                var start = IndexOfNoWhiteSpace(line, 1);
                if (TryReadFace(line.AsSpan()[start..], out var f))
                {
                    _faces.Add(f);
                    if (_objs.Count > 0)
                    {
                        _objs[^1].Faces.Add(_faces.Count - 1);
                    }
                }
                else
                {
                    _error.Append("line ").Append(lineNum).Append(": can't parse face, ignore\n");
                }
            }
            else if (line.StartsWith("vt "))
            {
                var start = IndexOfNoWhiteSpace(line, 2);
                if (TryReadVector2(line.AsSpan()[start..], out var vt))
                {
                    _texCoords.Add(vt);
                }
                else
                {
                    _error.Append("line ").Append(lineNum).Append(": can't parse uv, use default (0, 0)\n");
                    _texCoords.Add(Vector2.Zero);
                }
            }
            else if (line.StartsWith("vn "))
            {
                var start = IndexOfNoWhiteSpace(line, 2);
                if (TryReadVector3(line.AsSpan()[start..], out var vn))
                {
                    _normals.Add(vn);
                }
                else
                {
                    _error.Append("line ").Append(lineNum).Append(": can't parse normal, use default (0, 1, 0)\n");
                    _normals.Add(Vector3.UnitY);
                }
            }
            else if (line.StartsWith("mtllib "))
            {
                var start = IndexOfNoWhiteSpace(line, 6);
                _mtlsFile.Add(line[start..]);
            }
            else if (line.StartsWith("usemtl "))
            {
                if (_objs.Count > 0)
                {
                    var start = IndexOfNoWhiteSpace(line, 6);
                    _objs[^1].Material = line[start..];
                }
            }
            else if (line.StartsWith("s "))
            {
                //ignore
            }
            else
            {
                _error.Append("line ").Append(lineNum).Append(": unknown command, ignore\n");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private static int IndexOfNoWhiteSpace(string str, int start = 0)
        {
            var span = str.AsSpan()[start..];
            for (var i = 0; i < span.Length; i++)
            {
                var ch = span[i];
                if (ch != ' ')
                {
                    return i + start;
                }
            }
            return -1;
        }

        private struct VertexHash : IEquatable<VertexHash>
        {
            public int P, N, T;
            public VertexHash(int p, int n, int t)
            {
                P = p;
                N = n;
                T = t;
            }
            public bool Equals(VertexHash other)
            {
                return P == other.P && N == other.N && T == other.T;
            }
            public override int GetHashCode()
            {
                return HashCode.Combine(P, N, T);
            }
            public override bool Equals(object? obj)
            {
                return obj is VertexHash hash && Equals(hash);
            }
        }

        public TriangleModel AllFacesToModel()
        {
            Dictionary<VertexHash, int> unique = new Dictionary<VertexHash, int>();
            List<Vector3> p = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> u = new List<Vector2>();
            List<int> ind = new List<int>();
            int count = 0;
            for (int j = 0; j < Faces.Count; j++)
            {
                Face f = Faces[j];
                for (int i = 0; i < 3; i++)
                {
                    VertexHash v = new VertexHash(
                        MathExt.IndexerUnsafeReadonly(in f.Vertex.X, i),
                        MathExt.IndexerUnsafeReadonly(in f.Normal.X, i),
                        MathExt.IndexerUnsafeReadonly(in f.TexCoord.X, i)
                    );
                    if (!unique.TryGetValue(v, out int index))
                    {
                        unique.Add(v, count);
                        index = count;
                        p.Add(Vertices[v.P]);
                        if (v.N >= 0) { n.Add(Normals[v.N]); }
                        if (v.T >= 0) { u.Add(TexCoords[v.T]); }
                        count++;
                    }
                    ind.Add(index);
                }
            }
            return new TriangleModel(
                p.ToArray(),
                ind.ToArray(),
                n.Count > 0 ? n.ToArray() : null,
                u.Count > 0 ? u.ToArray() : null
            );
        }
    }
}
