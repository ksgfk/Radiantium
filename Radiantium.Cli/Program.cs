using Radiantium.Core;
using Radiantium.Offline;
using Radiantium.Offline.Config;
using System.Text.Json;

namespace Radiantium.Cli
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Logger.Error("CLI needs to pass the scene config file path in the command line");
                return;
            }
            (Renderer renderer, ResultOutput output) = GetRenderer(args);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            System.Timers.Timer timer = new System.Timers.Timer(1000);
            ConsoleProgressBar bar = new ConsoleProgressBar(renderer.AllBlockCount);
            renderer.CompleteBlock += _ => bar.Increase();
            timer.Elapsed += (_, _) => bar.Draw();
            timer.Start();
            bar.Start();
            Task task = renderer.Start().ContinueWith(t => { timer.Stop(); bar.Stop(); });
            task.Wait();
            Logger.Info($"[CLI] -> render used time: {renderer.RenderUseTime.TotalMilliseconds} ms");
            output.Save(renderer);
        }

        private static (Renderer, ResultOutput) GetRenderer(string[] args)
        {
            string sceneConfigPath = args[0];
            using FileStream stream = File.OpenRead(sceneConfigPath);
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonConfigProvider config = new JsonConfigProvider(doc.RootElement);
            string workDir = Path.GetDirectoryName(sceneConfigPath)!;
            string workSpaceName = Path.GetFileNameWithoutExtension(sceneConfigPath);
            RendererBuilder builder = new RendererBuilder(workDir, workSpaceName);
            builder.SetDefaultBuilders();
            if (config.HasKey("renderer"))
            {
                builder.SetRenderer(config.GetSubParam("renderer"));
            }
            builder.SetIntegrator(config.GetSubParam("integrator"));
            IConfigParamProvider cameraConfig = config.GetSubParam("camera");
            builder.SetCamera(cameraConfig);
            if (cameraConfig.HasKey("medium"))
            {
                builder.SetGlobalMedium(cameraConfig.GetSubParam("medium"));
            }
            if (config.HasKey("accel"))
            {
                builder.SetSceneAccelerator(config.GetSubParam("accel"));
            }
            if (config.HasKey("output"))
            {
                builder.SetOutput(config.GetSubParam("output"));
            }
            if (config.HasKey("assets"))
            {
                foreach (IConfigParamProvider asset in config.GetSubParams("assets"))
                {
                    if (!asset.HasKey("type")) { throw new ArgumentException("no key type"); }
                    string type = asset.ReadString("type", null);
                    switch (type)
                    {
                        case "model":
                            builder.AddModel(asset);
                            break;
                        case "image":
                            builder.AddImage(asset);
                            break;
                        default:
                            throw new ArgumentException($"unknown asset type {type}");
                    }
                }
            }
            if (config.HasKey("instanced"))
            {
                foreach (IConfigParamProvider instanced in config.GetSubParams("instanced"))
                {
                    int index = builder.AddInstancedPrimitivesEntry();
                    if (instanced.HasKey("accel"))
                    {
                        builder.SetInstancedAccelerator(index, instanced.GetSubParam("accel"));
                    }
                    if (instanced.HasKey("shape"))
                    {
                        IConfigParamProvider shape = instanced.GetSubParam("shape");
                        builder.AddInstancedShape(index, shape);
                    }
                    if (instanced.HasKey("transform"))
                    {
                        builder.SetInstancedLocalTransform(index, instanced.GetSubParam("transform"));
                    }
                }
            }
            Queue<(IConfigParamProvider, int?)> q = new Queue<(IConfigParamProvider, int?)>();
            if (config.HasKey("scene"))
            {
                foreach (IConfigParamProvider entity in config.GetSubParams("scene")) { q.Enqueue((entity, null)); }
            }
            while (q.Count > 0)
            {
                (IConfigParamProvider entity, int? parent) = q.Dequeue();
                int index = builder.AddEntity(parent);
                if (entity.HasKey("material"))
                {
                    builder.SetEntityMaterial(index, entity.GetSubParam("material"));
                }
                if (entity.HasKey("light"))
                {
                    builder.SetEntityAreaLight(index, entity.GetSubParam("light"));
                }
                if (entity.HasKey("transform"))
                {
                    builder.SetEntityLocalTransform(index, entity.GetSubParam("transform"));
                }
                if (entity.HasKey("shape"))
                {
                    IConfigParamProvider shape = entity.GetSubParam("shape");
                    builder.AddEntityShape(index, shape);
                }
                if (entity.HasKey("instanced"))
                {
                    IConfigParamProvider instanced = entity.GetSubParam("instanced");
                    if (!instanced.HasKey("id")) { throw new ArgumentException("invalid instanced config"); }
                    builder.AddEntityInstanced(index, instanced.ReadInt32("id", -1));
                }
                if (entity.HasKey("children"))
                {
                    foreach (IConfigParamProvider child in entity.GetSubParams("children"))
                    {
                        q.Enqueue((child, index));
                    }
                }
                if (entity.HasKey("medium"))
                {
                    builder.SetEntityMedium(index, entity.GetSubParam("medium"));
                }
            }
            return builder.Build();
        }
    }
}
