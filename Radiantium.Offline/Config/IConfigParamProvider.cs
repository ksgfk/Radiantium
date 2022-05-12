using System.Numerics;

namespace Radiantium.Offline.Config
{
    public enum ConfigParamType
    {
        Bool,
        Number,
        Vec2,
        Vec3,
        Vec4,
        Mat4,
        String,
        Object,
        Array,
        Null
    }

    public interface IConfigParamProvider
    {
        bool HasKey(string key);
        ConfigParamType GetParamType(string key);
        bool ReadBool(string key, bool defaultValue);
        int ReadInt32(string key, int defaultValue);
        float ReadFloat(string key, float defaultValue);
        double ReadDouble(string key, double defaultValue);
        Vector2 ReadVec2Float(string key, Vector2 defaultValue);
        (int, int) ReadVec2Int32(string key, (int, int) defaultValue);
        Vector3 ReadVec3Float(string key, Vector3 defaultValue);
        Vector4 ReadVec4Float(string key, Vector4 defaultValue);
        string ReadString(string key, string? defaultValue);
        Matrix4x4 ReadMat4(string key, Matrix4x4 defaultValue);
        IConfigParamProvider GetSubParam(string key);
        IReadOnlyList<IConfigParamProvider> GetSubParams(string key);
    }
}
