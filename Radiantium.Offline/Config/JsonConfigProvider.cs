using Radiantium.Core;
using System.Numerics;
using System.Text.Json;

namespace Radiantium.Offline.Config
{
    public class JsonConfigProvider : IConfigParamProvider
    {
        readonly JsonElement _json;
        readonly bool _hasData;

        public JsonConfigProvider()
        {
            _json = new JsonElement();
            _hasData = false;
        }

        public JsonConfigProvider(JsonElement json)
        {
            _json = json;
            _hasData = true;
        }

        public ConfigParamType GetParamType(string key)
        {
            JsonElement target = _json.GetProperty(key);
            switch (target.ValueKind)
            {
                case JsonValueKind.Undefined:
                    return ConfigParamType.Object;
                case JsonValueKind.Object:
                    return ConfigParamType.Object;
                case JsonValueKind.Array:
                    {
                        int length = target.GetArrayLength();
                        for (int i = 0; i < length; i++)
                        {
                            if (target[i].ValueKind != JsonValueKind.Number)
                            {
                                return ConfigParamType.Array;
                            }
                        }
                        return length switch
                        {
                            2 => ConfigParamType.Vec2,
                            3 => ConfigParamType.Vec3,
                            4 => ConfigParamType.Vec4,
                            16 => ConfigParamType.Mat4,
                            _ => ConfigParamType.Array,
                        };
                    }
                case JsonValueKind.String:
                    return ConfigParamType.String;
                case JsonValueKind.Number:
                    return ConfigParamType.Number;
                case JsonValueKind.True:
                    return ConfigParamType.Bool;
                case JsonValueKind.False:
                    return ConfigParamType.Bool;
                case JsonValueKind.Null:
                    return ConfigParamType.Null;
                default:
                    throw new NotSupportedException();
            }
        }

        public IConfigParamProvider GetSubParam(string key)
        {
            return new JsonConfigProvider(_json.GetProperty(key));
        }

        public IReadOnlyList<IConfigParamProvider> GetSubParams(string key)
        {
            JsonElement arr = _json.GetProperty(key);
            return arr.EnumerateArray().Select(e => new JsonConfigProvider(e)).ToArray();
        }

        public bool HasKey(string key)
        {
            return _hasData && _json.TryGetProperty(key, out _);
        }

        public bool ReadBool(string key, bool defaultValue)
        {
            return _hasData && _json.TryGetProperty(key, out var v) ? v.GetBoolean() : defaultValue;
        }

        public double ReadDouble(string key, double defaultValue)
        {
            return _hasData && _json.TryGetProperty(key, out var v) ? v.GetDouble() : defaultValue;
        }

        public float ReadFloat(string key, float defaultValue)
        {
            return _hasData && _json.TryGetProperty(key, out var v) ? v.GetSingle() : defaultValue;
        }

        public int ReadInt32(string key, int defaultValue)
        {
            return _hasData && _json.TryGetProperty(key, out var v) ? v.GetInt32() : defaultValue;
        }

        public Matrix4x4 ReadMat4(string key, Matrix4x4 defaultValue)
        {
            if (!_hasData || !_json.TryGetProperty(key, out var node))
            {
                return defaultValue;
            }
            Matrix4x4 result = default;
            for (int i = 0; i < 16; i++)
            {
                MathExt.IndexerUnsafe(ref result.M11, i) = node[i].GetSingle();
            }
            return Matrix4x4.Transpose(result);
        }

        public string ReadString(string key, string? defaultValue)
        {
            return _hasData && _json.TryGetProperty(key, out var v) ? v.GetString()! : defaultValue!;
        }

        public Vector2 ReadVec2Float(string key, Vector2 defaultValue)
        {
            if (!_hasData || !_json.TryGetProperty(key, out var node))
            {
                return defaultValue;
            }
            Vector2 result = new Vector2(node[0].GetSingle(), node[1].GetSingle());
            return result;
        }

        public (int, int) ReadVec2Int32(string key, (int, int) defaultValue)
        {
            if (!_hasData || !_json.TryGetProperty(key, out var node))
            {
                return defaultValue;
            }
            (int, int) result = (node[0].GetInt32(), node[1].GetInt32());
            return result;
        }

        public Vector3 ReadVec3Float(string key, Vector3 defaultValue)
        {
            if (!_hasData || !_json.TryGetProperty(key, out var node))
            {
                return defaultValue;
            }
            Vector3 result = new Vector3(node[0].GetSingle(), node[1].GetSingle(), node[2].GetSingle());
            return result;
        }

        public Vector4 ReadVec4Float(string key, Vector4 defaultValue)
        {
            if (!_hasData || !_json.TryGetProperty(key, out var node))
            {
                return defaultValue;
            }
            Vector4 result = new Vector4(node[0].GetSingle(), node[1].GetSingle(), node[2].GetSingle(), node[3].GetSingle());
            return result;
        }
    }
}
