using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class UniformBlockMemberComparer : IComparer<UniformBlockOpenGL.Member>
    {
        public static UniformBlockMemberComparer Default { get; } = new UniformBlockMemberComparer();

        private UniformBlockMemberComparer() { }

        public int Compare(UniformBlockOpenGL.Member? x, UniformBlockOpenGL.Member? y)
        {
            if (x!.Offset == y!.Offset) { return 0; }
            return x.Offset < y.Offset ? -1 : 1;
        }
    }

    public class UniformBlockOpenGL
    {
        public class Member : IEquatable<Member>
        {
            public string Name { get; }
            public int Location { get; }
            public UniformType Type { get; }
            public int Size { get; }
            public int Offset { get; }
            public int Align { get; internal set; }

            internal Member(string name, int location, UniformType type, int size, int offset, int align)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                Location = location;
                Type = type;
                Size = size;
                Offset = offset;
                Align = align;
            }

            public override bool Equals(object? obj)
            {
                if (obj is null) { return false; }
                if (ReferenceEquals(this, obj)) { return true; }
                if (obj is Member t) { return Equals(t); }
                return false;
            }

            public bool Equals(Member? other)
            {
                if (other is null) { return false; }
                //return Name == other.Name && Location == other.Location && Type == other.Type && Size == other.Size && Offset == other.Offset && Align == other.Align;
                return Location == other.Location; //一般来说不可能出现重复location. 如果出现了, 大概是驱动坏掉了吧 (
            }

            public override int GetHashCode()
            {
                //return HashCode.Combine(Name, Location, Type, Size, Offset, Align);
                return HashCode.Combine(Location);
            }

            public static bool operator ==(Member lhs, Member rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(Member lhs, Member rhs)
            {
                return !(lhs == rhs);
            }
        }

        public string Name { get; }
        public uint Index { get; }
        public int Size { get; }
        public Member[] Members { get; }

        internal UniformBlockOpenGL(string name, uint index, int size, Member[] members)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Index = index;
            Size = size;
            Members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) { return false; }
            if (ReferenceEquals(this, obj)) { return true; }
            if (obj is Member t) { return Equals(t); }
            return false;
        }

        public bool Equals(UniformBlockOpenGL? other)
        {
            if (other is null) { return false; }
            return Index == other.Index;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index);
        }

        public static bool operator ==(UniformBlockOpenGL lhs, UniformBlockOpenGL rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(UniformBlockOpenGL lhs, UniformBlockOpenGL rhs)
        {
            return !(lhs == rhs);
        }
    }
}
