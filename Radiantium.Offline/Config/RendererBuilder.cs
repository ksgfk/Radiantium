using Radiantium.Core;
using Radiantium.Offline.Accelerators;
using Radiantium.Offline.Bxdf;
using Radiantium.Offline.Integrators;
using Radiantium.Offline.Lights;
using Radiantium.Offline.Materials;
using Radiantium.Offline.Shapes;
using Radiantium.Offline.Textures;
using System.Diagnostics;
using System.Numerics;

namespace Radiantium.Offline.Config
{
    public static class RendererBuilderExtension
    {
        public static void SetDefaultBuilders(this RendererBuilder builder)
        {
            builder.AddShapeBuilder("sphere", (_, mat, param) => new Sphere(param.ReadFloat("radius", 0.5f), mat));
            builder.AddIntegratorBuilder("ao", (_, param) => new AmbientOcclusion(param.ReadBool("is_cos_weight", true)));
            builder.AddIntegratorBuilder("path", (_, param) =>
            {
                int maxDepth = param.ReadInt32("max_depth", -1);
                int minDepth = param.ReadInt32("min_depth", 3);
                float rrThreshold = param.ReadFloat("rr_threshold", 0.99f);
                PathSampleMethod method = Enum.Parse<PathSampleMethod>(param.ReadString("method", "Mis"));
                return new PathTracing(maxDepth, minDepth, rrThreshold, method);
            });
            builder.AddAccelBuilder("bvh", (_, p, param) => new Bvh(p, param.ReadInt32("max_prim", 1), Enum.Parse<SplitMethod>(param.ReadString("split", "SAH"))));
            builder.AddAccelBuilder("octree", (_, p, param) => new Octree(p, param.ReadInt32("max_depth", 10), param.ReadInt32("max_count", 10), param.ReadFloat("out_bound", 2.0f)));
            builder.AddMaterialBuilder("diffuse", (builder, images, param) =>
             {
                 Texture2D kd = param.ReadTex2D("kd", builder, new Color3F(0.5f));
                 return new DiffuseReflection(kd);
             });
            builder.AddMaterialBuilder("perfect_glass", (builder, images, param) =>
            {
                Texture2D r = param.ReadTex2D("r", builder, new Color3F(1));
                Texture2D t = param.ReadTex2D("t", builder, new Color3F(1));
                float etaA = param.ReadFloat("etaA", 1.000277f);
                float etaB = param.ReadFloat("etaB", 1.5046f);
                return new PerfectGlass(r, t, etaA, etaB);
            });
            builder.AddMaterialBuilder("perfect_mirror", (builder, images, param) =>
            {
                Texture2D r = param.ReadTex2D("r", builder, new Color3F(1));
                return new PerfectMirror(r);
            });
            builder.AddMaterialBuilder("rough_plastic", (builder, images, param) =>
            {
                Texture2D r = param.ReadTex2D("r", builder, new Color3F(0.5f));
                MicrofacetDistributionType dist = Enum.Parse<MicrofacetDistributionType>(param.ReadString("dist", "GGX"));
                Texture2D roughness = param.ReadTex2D("roughness", builder, new Color3F(0.01f));
                Texture2D kd = param.ReadTex2D("kd", builder, new Color3F(0.5f));
                Texture2D ks = param.ReadTex2D("ks", builder, new Color3F(1.0f));
                float etaI = param.ReadFloat("etai", 1.0f);
                float etaT = param.ReadFloat("etat", 1.5f);
                return new RoughPlastic(r, dist, roughness, kd, ks, etaI, etaT);
            });
            builder.AddAreaLightBuilder("diffuse_area", (_, shape, param) =>
            {
                Vector3 le = param.ReadVec3Float("le", new Vector3(1));
                return new DiffuseAreaLight(shape, new Color3F(le));
            });
            builder.AddInfiniteLightBuilder("infinite", (builder, mat, param) =>
            {
                Texture2D img = param.ReadTex2D("le", builder, new Color3F(0));
                return new InfiniteAreaLight(mat, img);
            });
        }

        public static Texture2D ReadTex2D(this IConfigParamProvider param,
            string key,
            RendererBuilder builder,
            Color3F defaultValue)
        {
            if (!param.HasKey(key)) { return new ConstColorTexture2D(defaultValue); }
            ConfigParamType type = param.GetParamType(key);
            if (type == ConfigParamType.Vec3)
            {
                Vector3 color = param.ReadVec3Float(key, new Vector3());
                return new ConstColorTexture2D(new Color3F(color));
            }
            else if (type == ConfigParamType.Number)
            {
                float color = param.ReadFloat(key, 0);
                return new ConstColorTexture2D(new Color3F(color));
            }
            else
            {
                IConfigParamProvider cfg = param.GetSubParam(key);
                if (!cfg.HasKey("type")) { throw new ArgumentException("no key type"); }
                string t = cfg.ReadString("type", null);
                if (t == "image")
                {
                    if (!cfg.HasKey("name")) { throw new ArgumentException("no key name"); }
                    string name = cfg.ReadString("name", null);
                    IConfigParamProvider? samplerConfig = null;
                    if (cfg.HasKey("sampler"))
                    {
                        samplerConfig = cfg.GetSubParam("sampler");
                    }
                    return new ImageTexture2D(builder.LoadedImages[name].Image, builder.CreateTextureSampler(samplerConfig));
                }
                else
                {
                    return builder.CreateTexture2D(cfg);
                }
            }
        }
    }

    public class RendererBuilder
    {
        public delegate Aggregate AccelBuilder(RendererBuilder builder, IReadOnlyList<Primitive> primitives, IConfigParamProvider param);
        public delegate Shape ShapeBuilder(RendererBuilder builder, Matrix4x4 modelToWorld, IConfigParamProvider param);
        public delegate Material MaterialBuilder(RendererBuilder builder, Dictionary<string, ImageEntry> images, IConfigParamProvider param);
        public delegate AreaLight AreaLightBuilder(RendererBuilder builder, Shape shape, IConfigParamProvider param);
        public delegate InfiniteLight InfiniteLightBuilder(RendererBuilder builder, Matrix4x4 modelToWorld, IConfigParamProvider param);
        public delegate Integrator IntegratorBuilder(RendererBuilder builder, IConfigParamProvider param);
        public delegate Texture2D Texture2DBuilder(RendererBuilder builder, IConfigParamProvider param);

        public record ModelEntry(string Name, string Location, TriangleModel Model)
        {
            public record Child(string Name, TriangleModel Model);
            public List<Child>? Children { get; init; }
        }

        public record ImageEntry(string Name, string Location, ColorBuffer Image);

        public record InstancedPrimitivesEntry(int Index)
        {
            public IConfigParamProvider? AggregateConfig { get; set; }
            public List<IConfigParamProvider> ShapeConfigs { get; } = new List<IConfigParamProvider>();
            public List<IConfigParamProvider> ModelConfigs { get; } = new List<IConfigParamProvider>();
        }

        public class SceneEntityEntry
        {
            public int Index { get; }
            public Vector3 LocalPosition { get; set; } = new Vector3(0);
            public Matrix4x4 LocalRotation { get; set; } = Matrix4x4.Identity;
            public Vector3 LocalScale { get; set; } = new Vector3(1);
            public List<IConfigParamProvider> Models { get; } = new List<IConfigParamProvider>();
            public List<IConfigParamProvider> Shapes { get; } = new List<IConfigParamProvider>();
            public List<InstancedPrimitivesEntry> Instances { get; } = new List<InstancedPrimitivesEntry>();
            public IConfigParamProvider? MaterialConfig { get; set; }
            public IConfigParamProvider? LightConfig { get; set; }
            public List<SceneEntityEntry> Children { get; } = new List<SceneEntityEntry>();
            public SceneEntityEntry? Parent { get; set; }
            public Matrix4x4 ModelToWorld { get; set; }
            public SceneEntityEntry(int index) { Index = index; }
        }

        readonly string _searchPath;
        readonly string _workSpaceName;
        readonly Dictionary<string, AccelBuilder> _accelBuilder;
        readonly Dictionary<string, MaterialBuilder> _materialBuilder;
        readonly Dictionary<string, AreaLightBuilder> _areaLightBuilder;
        readonly Dictionary<string, InfiniteLightBuilder> _infLightBuilder;
        readonly Dictionary<string, ShapeBuilder> _shapeBuilder;
        readonly Dictionary<string, IntegratorBuilder> _integratorBuilder;
        readonly Dictionary<string, Texture2DBuilder> _tex2dBuilder;

        readonly List<IConfigParamProvider> _modelConfigs;
        readonly List<IConfigParamProvider> _imageConfigs;
        readonly List<InstancedPrimitivesEntry> _instances;
        readonly List<SceneEntityEntry> _root;
        readonly List<SceneEntityEntry> _entities;

        IConfigParamProvider? _integratorConfig;
        IConfigParamProvider? _cameraConfig;
        IConfigParamProvider? _sceneAccelConfig;
        IConfigParamProvider? _rendererConfig;
        IConfigParamProvider? _outputConfig;
        Dictionary<string, ModelEntry> _models = null!;
        Dictionary<string, ImageEntry> _images = null!;
        Aggregate[] _inst = null!;

        public IReadOnlyDictionary<string, ModelEntry> LoadedModels => _models;
        public IReadOnlyDictionary<string, ImageEntry> LoadedImages => _images;
        public IReadOnlyList<Aggregate> CompleteIns => _inst;

        public RendererBuilder(string searchPath, string workSpaceName)
        {
            _searchPath = searchPath ?? throw new ArgumentNullException(nameof(searchPath));
            _workSpaceName = workSpaceName ?? throw new ArgumentNullException(nameof(workSpaceName));

            _accelBuilder = new Dictionary<string, AccelBuilder>();
            _materialBuilder = new Dictionary<string, MaterialBuilder>();
            _areaLightBuilder = new Dictionary<string, AreaLightBuilder>();
            _infLightBuilder = new Dictionary<string, InfiniteLightBuilder>();
            _shapeBuilder = new Dictionary<string, ShapeBuilder>();
            _integratorBuilder = new Dictionary<string, IntegratorBuilder>();
            _tex2dBuilder = new Dictionary<string, Texture2DBuilder>();

            _modelConfigs = new List<IConfigParamProvider>();
            _imageConfigs = new List<IConfigParamProvider>();
            _instances = new List<InstancedPrimitivesEntry>();
            _root = new List<SceneEntityEntry>();
            _entities = new List<SceneEntityEntry>();
        }

        public Shape CreateShape(Matrix4x4 modelToWorld, IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _shapeBuilder[param.ReadString("type", null)](this, modelToWorld, param);
        }

        public Aggregate CreateAccelerator(IReadOnlyList<Primitive> primitives, IConfigParamProvider? param)
        {
            Aggregate result;
            if (param == null)
            {
                result = new Bvh(primitives, 1, SplitMethod.SAH);
            }
            else
            {
                if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
                result = _accelBuilder[param.ReadString("type", null)](this, primitives, param);
            }
            return result;
        }

        public Material CreateMaterial(IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _materialBuilder[param.ReadString("type", null)](this, _images, param);
        }

        public AreaLight CreateAreaLight(Shape shape, IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _areaLightBuilder[param.ReadString("type", null)](this, shape, param);
        }

        public InfiniteLight CreateInfiniteLight(Matrix4x4 modelToWorld, IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _infLightBuilder[param.ReadString("type", null)](this, modelToWorld, param);
        }

        public Integrator CreateIntegrator(IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _integratorBuilder[param.ReadString("type", null)](this, param);
        }

        public Camera CreateCamera(IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            string name = param.ReadString("type", null);
            switch (name)
            {
                case "perspective":
                    (int x, int y) = param.ReadVec2Int32("screen", (1280, 720));
                    return new PerspectiveCamera(
                        fov: param.ReadFloat("fov", 60),
                        near: param.ReadFloat("near", 0.001f),
                        far: param.ReadFloat("far", 1000.0f),
                        origin: param.ReadVec3Float("origin", new Vector3(0, 0, 3)),
                        target: param.ReadVec3Float("target", new Vector3(0)),
                        up: param.ReadVec3Float("up", new Vector3(0, 1, 0)),
                        screenX: x,
                        screenY: y);
                default:
                    throw new ArgumentOutOfRangeException($"no camera {name}");
            }
        }

        public Renderer CreateRenderer(Scene scene, Camera camera, Integrator integrator, IConfigParamProvider? param)
        {
            int sampleCount = 256;
            int maxTask = -1;
            if (param != null)
            {
                sampleCount = param.ReadInt32("spp", 256);
                maxTask = param.ReadInt32("threads", -1);
            }
            return new Renderer(scene, camera, integrator, sampleCount, maxTask);
        }

        public ResultOutput CreateOutput(IConfigParamProvider? param)
        {
            if (param == null)
            {
                return new ResultOutput(false, false, true, _searchPath, _workSpaceName);
            }
            else
            {
                return new ResultOutput(
                    isSavePng: param.ReadBool("is_save_png", false),
                    isPngToSrgb: param.ReadBool("is_png_to_srgb", false),
                    isSaveExr: param.ReadBool("is_save_exr", true),
                    savePath: param.ReadString("save_path", _searchPath),
                    saveName: param.ReadString("save_name", _workSpaceName));
            }
        }

        public Texture2D CreateTexture2D(IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("no key type"); }
            return _tex2dBuilder[param.ReadString("type", null)](this, param);
        }

        public TextureSampler CreateTextureSampler(IConfigParamProvider? param)
        {
            WrapMode wrap = WrapMode.Clamp;
            FilterMode filter = FilterMode.Nearest;
            if (param == null)
            {
                return new TextureSampler(wrap, filter);
            }
            if (param.HasKey("wrap"))
            {
                wrap = Enum.Parse<WrapMode>(param.ReadString("wrap", "Clamp"));
            }
            if (param.HasKey("filter"))
            {
                filter = Enum.Parse<FilterMode>(param.ReadString("filter", "Nearest"));
            }
            return new TextureSampler(wrap, filter);
        }

        public string GetPath(string location)
        {
            if (File.Exists(location)) { return location; }
            string path = Path.GetFullPath(Path.Combine(_searchPath, location));
            if (!File.Exists(path)) { throw new FileNotFoundException($"invalid location {location}"); }
            return path;
        }

        public void AddModel(IConfigParamProvider param)
        {
            _modelConfigs.Add(param);
        }

        public ModelEntry LoadModel(string name, string location, bool isStoreSubModel, bool willGenNormal)
        {
            string path = GetPath(location);
            Stopwatch sw = new Stopwatch();
            long usedMemoryByte = 0;
            sw.Start();
            using FileStream stream = File.OpenRead(path);
            using WavefrontObjReader reader = new WavefrontObjReader(stream);
            reader.Read();
            if (!string.IsNullOrWhiteSpace(reader.ErrorInfo))
            {
                Logger.Warn($"[Offline.RendererBuilder] -> obj reader warning: ");
                Logger.Warn($"    {reader.ErrorInfo}");
            }
            TriangleModel model = reader.ToModel(willGenNormal);
            usedMemoryByte += model.UsedMemory;
            List<ModelEntry.Child>? children = null;
            if (isStoreSubModel)
            {
                children = new List<ModelEntry.Child>();
                foreach (WavefrontObjReader.ModelObject modelObject in reader.Objects)
                {
                    TriangleModel childModel = reader.ToModel(modelObject.Name, willGenNormal);
                    usedMemoryByte += childModel.UsedMemory;
                    ModelEntry.Child child = new ModelEntry.Child(modelObject.Name, childModel);
                    children.Add(child);
                }
            }
            ModelEntry entry = new ModelEntry(name, location, model)
            {
                Children = children
            };
            sw.Stop();
            Logger.Info($"[Offline.RendererBuilder] -> load model {path}");
            Logger.Info($"[Offline.RendererBuilder] -> use time {sw.ElapsedMilliseconds} ms");
            Logger.Info($"[Offline.RendererBuilder] -> vertex {reader.Positions.Count}, face {reader.Faces.Count}");
            Logger.Info($"[Offline.RendererBuilder] -> used memory {usedMemoryByte / 1024.0f / 1024:0.00} MB");
            return entry;
        }

        public ImageEntry LoadImage(string name, string location, bool isFlipY, bool isCastToLinear)
        {
            string path = GetPath(location);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            ColorBuffer image = TextureUtility.LoadImageFromPath(path, isFlipY, isCastToLinear);
            ImageEntry entry = new ImageEntry(name, location, image);
            sw.Stop();
            Logger.Info($"[Offline.RendererBuilder] -> load image {path}");
            Logger.Info($"[Offline.RendererBuilder] -> use time {sw.ElapsedMilliseconds} ms");
            Logger.Info($"[Offline.RendererBuilder] -> used memory {image.UsedMemory / 1024.0f / 1024:0.00} MB");
            return entry;
        }

        public void AddImage(IConfigParamProvider param)
        {
            _imageConfigs.Add(param);
        }

        public int AddInstancedPrimitivesEntry()
        {
            int index = _instances.Count;
            _instances.Add(new InstancedPrimitivesEntry(index));
            return index;
        }

        public void AddInstancedShape(int index, IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("invalid shape config"); }
            string type = param.ReadString("type", null);
            if (type == "model")
            {
                _instances[index].ModelConfigs.Add(param);
            }
            else
            {
                _instances[index].ShapeConfigs.Add(param);
            }
        }

        public void SetInstancedAccelerator(int index, IConfigParamProvider param)
        {
            _instances[index].AggregateConfig = param;
        }

        public int AddEntity(int? parent = null)
        {
            int index = _entities.Count;
            SceneEntityEntry newEntity = new SceneEntityEntry(index);
            _entities.Add(newEntity);
            if (parent == null)
            {
                _root.Add(newEntity);
            }
            else
            {
                _entities[parent.Value].Children.Add(newEntity);
                newEntity.Parent = _entities[parent.Value];
            }
            return index;
        }

        public void SetEntityLocalTransform(int index, IConfigParamProvider param)
        {
            Vector3 position = param.ReadVec3Float("position", new Vector3(0));
            Matrix4x4 rotation;
            if (param.HasKey("rotation"))
            {
                ConfigParamType type = param.GetParamType("rotation");
                switch (type)
                {
                    case ConfigParamType.Mat4:
                        rotation = param.ReadMat4("rotation", Matrix4x4.Identity);
                        break;
                    case ConfigParamType.Object:
                        IConfigParamProvider o = param.GetSubParam("rotation");
                        Vector3 axis = o.ReadVec3Float("axis", new Vector3(0, 1, 0));
                        float angle = o.ReadFloat("angle", 0);
                        rotation = Matrix4x4.CreateFromAxisAngle(axis, MathExt.Radian(angle));
                        break;
                    case ConfigParamType.Vec4:
                        Vector4 v = param.ReadVec4Float("rotation", new Vector4(0, 0, 0, 1));
                        rotation = Matrix4x4.CreateFromQuaternion(new Quaternion(v.X, v.Y, v.Z, v.W));
                        break;
                    default:
                        throw new ArgumentException("invalid param");
                }
            }
            else
            {
                rotation = Matrix4x4.Identity;
            }
            Vector3 scale = param.ReadVec3Float("scale", new Vector3(1));

            _entities[index].LocalPosition = position;
            _entities[index].LocalRotation = rotation;
            _entities[index].LocalScale = scale;
        }

        public void AddEntityShape(int index, IConfigParamProvider param)
        {
            if (!param.HasKey("type")) { throw new ArgumentException("invalid shape config"); }
            string type = param.ReadString("type", null);
            if (type == "model")
            {
                _entities[index].Models.Add(param);
            }
            else
            {
                _entities[index].Shapes.Add(param);
            }
        }

        public void AddEntityInstanced(int index, int instanceIndex)
        {
            _entities[index].Instances.Add(_instances[instanceIndex]);
        }

        public void SetEntityMaterial(int index, IConfigParamProvider param)
        {
            _entities[index].MaterialConfig = param;
        }

        public void SetEntityAreaLight(int index, IConfigParamProvider param)
        {
            _entities[index].LightConfig = param;
        }

        public void SetSceneAccelerator(IConfigParamProvider param)
        {
            _sceneAccelConfig = param;
        }

        public void SetIntegrator(IConfigParamProvider param)
        {
            _integratorConfig = param;
        }

        public void SetRenderer(IConfigParamProvider param)
        {
            _rendererConfig = param;
        }

        public void SetOutput(IConfigParamProvider param)
        {
            _outputConfig = param;
        }

        public void SetCamera(IConfigParamProvider param)
        {
            _cameraConfig = param;
        }

        public void AddAccelBuilder(string accel, AccelBuilder builder)
        {
            _accelBuilder.Add(accel, builder);
        }

        public void AddIntegratorBuilder(string name, IntegratorBuilder builder)
        {
            _integratorBuilder.Add(name, builder);
        }

        public void AddMaterialBuilder(string name, MaterialBuilder builder)
        {
            _materialBuilder.Add(name, builder);
        }

        public void AddAreaLightBuilder(string name, AreaLightBuilder builder)
        {
            _areaLightBuilder.Add(name, builder);
        }

        public void AddShapeBuilder(string name, ShapeBuilder builder)
        {
            _shapeBuilder.Add(name, builder);
        }

        public void AddTex2DBuilder(string name, Texture2DBuilder builder)
        {
            _tex2dBuilder.Add(name, builder);
        }

        public void AddInfiniteLightBuilder(string name, InfiniteLightBuilder builder)
        {
            _infLightBuilder.Add(name, builder);
        }

        public TriangleModel FindTriangleModel(IConfigParamProvider param)
        {
            if (!param.HasKey("name")) { throw new ArgumentException("no key name"); }
            string name = param.ReadString("name", null);
            string? child = param.ReadString("child", null);
            ModelEntry entry = _models[name];
            TriangleModel? model;
            if (child == null)
            {
                model = entry.Model;
            }
            else
            {
                model = entry.Children?.Find(e => e.Name == child)?.Model;
            }
            if (model == null)
            {
                throw new ArgumentException($"can't find model {name}${child}");
            }
            return model;
        }

        public (Renderer, ResultOutput) Build()
        {
            _models = _modelConfigs.Select(param =>
           {
               if (!param.HasKey("name")) { throw new ArgumentException("no key name"); }
               if (!param.HasKey("location")) { throw new ArgumentException("no key location"); }
               return LoadModel(param.ReadString("name", null),
                   param.ReadString("location", null),
                   param.ReadBool("is_store_sub_model", false),
                   param.ReadBool("will_gen_normal", false));
           }).ToDictionary(m => m.Name, m => m);

            _images = _imageConfigs.Select(param =>
           {
               if (!param.HasKey("name")) { throw new ArgumentException("no key name"); }
               if (!param.HasKey("location")) { throw new ArgumentException("no key location"); }
               string location = param.ReadString("location", null);
               bool isCastToLinear = param.ReadBool("is_cast_to_linear", !TextureUtility.IsHdr(location));
               return LoadImage(param.ReadString("name", null),
                   location,
                   param.ReadBool("is_flip_y", true),
                   isCastToLinear);
           }).ToDictionary(m => m.Name, m => m);

            _inst = _instances.Select(entry =>
            {
                List<Primitive> primitives = new List<Primitive>();
                foreach (IConfigParamProvider param in entry.ShapeConfigs)
                {
                    Shape shape = CreateShape(Matrix4x4.Identity, param);
                    ShapeWrapperPrimitive wrapper = new ShapeWrapperPrimitive(shape);
                    primitives.Add(wrapper);
                }
                foreach (IConfigParamProvider param in entry.ModelConfigs)
                {
                    TriangleModel model = FindTriangleModel(param);
                    TriangleMesh mesh = new TriangleMesh(Matrix4x4.Identity, model);
                    List<Triangle> triangles = mesh.ToTriangle();
                    foreach (Triangle triangle in triangles)
                    {
                        ShapeWrapperPrimitive wrapper = new ShapeWrapperPrimitive(triangle);
                        primitives.Add(wrapper);
                    }
                }
                Aggregate accel = CreateAccelerator(primitives, entry.AggregateConfig);
                return accel;
            }).ToArray();

            Queue<SceneEntityEntry> bfsQ = new Queue<SceneEntityEntry>(_root);
            while (bfsQ.Count > 0)
            {
                SceneEntityEntry e = bfsQ.Dequeue();
                Matrix4x4 par;
                if (e.Parent == null) { par = Matrix4x4.Identity; }
                else { par = e.Parent.ModelToWorld; }
                Matrix4x4 trans = Matrix4x4.CreateTranslation(e.LocalPosition);
                Matrix4x4 rotate = e.LocalRotation;
                Matrix4x4 scale = Matrix4x4.CreateScale(e.LocalScale);
                Matrix4x4 thisModel = scale * rotate * trans;
                e.ModelToWorld = par * thisModel;
                foreach (SceneEntityEntry child in e.Children) { bfsQ.Enqueue(child); }
            }

            List<Primitive> primitives = new List<Primitive>();
            List<Light> lights = new List<Light>();
            List<InfiniteLight> infiniteLights = new List<InfiniteLight>();
            foreach (SceneEntityEntry e in _entities)
            {
                if (e.MaterialConfig == null) //infinite light
                {
                    if (e.LightConfig == null)
                    {
                        continue;
                    }
                    InfiniteLight light = CreateInfiniteLight(e.ModelToWorld, e.LightConfig);
                    lights.Add(light);
                    infiniteLights.Add(light);
                }
                else //scene object
                {
                    Material material = CreateMaterial(e.MaterialConfig);
                    foreach (IConfigParamProvider param in e.Shapes)
                    {
                        Shape shape = CreateShape(e.ModelToWorld, param);
                        GeometricPrimitive prim = new GeometricPrimitive(shape, material)
                        {
                            Light = e.LightConfig == null ? null : CreateAreaLight(shape, e.LightConfig)
                        };
                        if (prim.Light != null)
                        {
                            lights.Add(prim.Light);
                        }
                        primitives.Add(prim);
                    }
                    foreach (IConfigParamProvider param in e.Models)
                    {
                        TriangleModel model = FindTriangleModel(param);
                        TriangleMesh mesh = new TriangleMesh(e.ModelToWorld, model);
                        List<Triangle> triangles = mesh.ToTriangle();
                        foreach (Triangle triangle in triangles)
                        {
                            GeometricPrimitive prim = new GeometricPrimitive(triangle, material)
                            {
                                Light = e.LightConfig == null ? null : CreateAreaLight(triangle, e.LightConfig)
                            };
                            if (prim.Light != null)
                            {
                                lights.Add(prim.Light);
                            }
                            primitives.Add(prim);
                        }
                    }
                    foreach (InstancedPrimitivesEntry entry in e.Instances)
                    {
                        InstancedPrimitive prim = new InstancedPrimitive(_inst[entry.Index], new InstancedTransform(e.ModelToWorld), material);
                        primitives.Add(prim);
                    }
                }
            }

            Aggregate aggregate = CreateAccelerator(primitives, _sceneAccelConfig);

            Scene scene = new Scene(aggregate, lights.ToArray(), infiniteLights.ToArray());

            Integrator integrator = CreateIntegrator(_integratorConfig!);

            Camera camera = CreateCamera(_cameraConfig!);

            Renderer renderer = CreateRenderer(scene, camera, integrator, _rendererConfig);

            ResultOutput output = CreateOutput(_outputConfig);

            return (renderer, output);
        }
    }
}
