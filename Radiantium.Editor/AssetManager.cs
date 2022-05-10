using Radiantium.Core;
using System.Diagnostics;

namespace Radiantium.Editor
{
    public class AssetEntryReference<T> where T : AssetEntry
    {
        readonly AssetManager _asset;
        string? _key;

        public string? Key => _key;
        public bool IsValid => _key != null && _asset.AllAssets.ContainsKey(_key);

        public AssetEntryReference(AssetManager asset, string? key)
        {
            _asset = asset;
            _key = key;
        }

        public void SetReference(string? key) { _key = key; }

        public T? Get()
        {
            if (_key == null) { return null; }
            return _asset.AllAssets[_key] as T;
        }

        public bool TryGet(out T o)
        {
            if (_key == null) { o = null!; return false; }
            if (_asset.AllAssets.TryGetValue(_key, out var temp))
            {
                o = (temp as T)!;
                return o != null;
            }
            o = null!;
            return false;
        }
    }

    public abstract class AssetEntry
    {
        public string MyPath { get; }
        public abstract IReadOnlySet<string> MatchedExtension { get; }

        public AssetEntry(string fullPath)
        {
            MyPath = fullPath;
        }
    }

    public class AssetModel : AssetEntry
    {
        private static readonly HashSet<string> _modelType = new HashSet<string>
        {
            ".obj"
        };

        public TriangleModel Model { get; }
        public override IReadOnlySet<string> MatchedExtension => _modelType;

        public AssetModel(string fullPath, TriangleModel model) : base(fullPath)
        {
            Model = model;
        }
    }

    public class AssetImage : AssetEntry
    {
        private static readonly HashSet<string> _imgType = new HashSet<string>
        {
            ".png",".jpg",".jpeg",".bmp",".ppm",".tga",
            ".exr",".hdr"
        };

        public ColorBuffer Image { get; }
        public override IReadOnlySet<string> MatchedExtension => _imgType;

        public AssetImage(string fullPath, ColorBuffer image) : base(fullPath)
        {
            Image = image;
        }
    }

    public class AssetManager
    {
        readonly EditorApplication _app;
        readonly Dictionary<string, AssetEntry> _assets;
        readonly HashSet<string> _canLoadFileExt;
        readonly AssetModel _dummyModel;
        readonly AssetImage _dummyImage;
        readonly List<AssetModel> _models;
        readonly List<AssetImage> _images;

        public string RootDirectory => _app.WorkingDir;
        public IReadOnlyDictionary<string, AssetEntry> AllAssets => _assets;
        public IReadOnlyList<AssetModel> Models => _models;
        public IReadOnlyList<AssetImage> Images => _images;

        public AssetManager(EditorApplication app)
        {
            _app = app;
            _assets = new Dictionary<string, AssetEntry>();
            _canLoadFileExt = new HashSet<string>();
            _models = new List<AssetModel>();
            _images = new List<AssetImage>();

            _dummyModel = new AssetModel(null!, null!);
            _dummyImage = new AssetImage(null!, null!);
            foreach (var item in _dummyModel.MatchedExtension)
            {
                _canLoadFileExt.Add(item);
            }
            foreach (var item in _dummyImage.MatchedExtension)
            {
                _canLoadFileExt.Add(item);
            }
        }

        public bool IsImage(string path)
        {
            return _dummyImage.MatchedExtension.Contains(Path.GetExtension(path));
        }

        public bool IsModel(string path)
        {
            return _dummyModel.MatchedExtension.Contains(Path.GetExtension(path));
        }

        public TriangleModel LoadModel(string path)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            string fullPath = Path.Combine(RootDirectory, path);
            using var stream = File.OpenRead(fullPath);
            using var reader = new WavefrontObjReader(stream);
            reader.Read();
            sw.Stop();
            Logger.Info($"[Asset] load model: {fullPath}");
            Logger.Info($"[Asset] use time: {sw.ElapsedMilliseconds} ms");
            Logger.Info($"[Asset] vertex count: {reader.Vertices.Count}, face: {reader.Faces.Count}");
            if (!string.IsNullOrWhiteSpace(reader.ErrorInfo))
            {
                Logger.Info($"[Asset] reader warning: ");
                Logger.Error($"  {reader.ErrorInfo}");
            }
            var allFaces = reader.AllFacesToModel();
            _assets[path] = new AssetModel(path, allFaces); //TODO: sub objects
            return allFaces;
        }

        public void Reset()
        {
            _assets.Clear();
            _models.Clear();
            _images.Clear();
        }

        public TriangleModel? GetModel(string path)
        {
            if (_assets.TryGetValue(path, out var m))
            {
                return (m as AssetModel)?.Model;
            }
            return LoadModel(path);
        }

        public bool CanLoad(string path)
        {
            return _canLoadFileExt.Contains(Path.GetExtension(path));
        }

        public bool IsLoaded(string path)
        {
            return _assets.ContainsKey(path);
        }

        public void Load(string path)
        {
            if (IsImage(path))
            {

            }
            else if (IsModel(path))
            {
                LoadModel(path);
            }

            _models.Clear();
            _images.Clear();
            foreach (var (_, entry) in _assets)
            {
                switch (entry)
                {
                    case AssetModel m:
                        _models.Add(m);
                        break;
                    case AssetImage img:
                        _images.Add(img);
                        break;
                }
            }
        }

        public void Release(string path)
        {
            _assets.Remove(path);
        }
    }
}
