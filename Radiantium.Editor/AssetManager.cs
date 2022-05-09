using Radiantium.Core;

namespace Radiantium.Editor
{
    public class AssetManager
    {
        readonly string _rootDirectory;
        readonly Dictionary<string, TriangleModel> _models;
        readonly Dictionary<string, ColorBuffer> _images;

        public AssetManager(string rootDir)
        {
            _rootDirectory = rootDir;
            _models = new Dictionary<string, TriangleModel>();
            _images = new Dictionary<string, ColorBuffer>();
        }

        public TriangleModel LoadModel(string path)
        {
            string fullPath = Path.Combine(_rootDirectory, path);
            using var stream = File.OpenRead(fullPath);
            using var reader = new WavefrontObjReader(stream);
            reader.Read();
            if (!string.IsNullOrWhiteSpace(reader.ErrorInfo)) { Logger.Error(reader.ErrorInfo); }
            var allFaces = reader.AllFacesToModel();
            _models[fullPath] = allFaces; //TODO: sub objects
            return allFaces;
        }

        public TriangleModel? GetModel(string path)
        {
            if (_models.TryGetValue(path, out var m))
            {
                return m;
            }
            return LoadModel(path);
        }
    }
}
