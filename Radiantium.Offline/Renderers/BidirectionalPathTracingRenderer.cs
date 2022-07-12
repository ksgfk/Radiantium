using Radiantium.Core;
using Radiantium.Offline.Integrators;
using System.Diagnostics;
using System.Numerics;
using System.Timers;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Renderers
{
    public enum BdptVertexType
    {
        Camera,
        Light,
        Surface,
        Medium
    }

    public struct BdptVertex
    {
        public BdptVertexType Type;
        public Color3F Coeff;
        public float PdfForward;
        public float PdfReverse;

        public Camera? Camera;
        public Light? Light;

        public SampleMediumResult Msr;

        public Intersection Inct;
        public bool IsDelta;

        public Vector3 P
        {
            get => Type == BdptVertexType.Medium ? Msr.P : Inct.P;
            set
            {
                if (Type == BdptVertexType.Medium)
                {
                    Msr.P = value;
                }
                else
                {
                    Inct.P = value;
                }
            }
        }
        public Vector3 N { get => Inct.N; set => Inct.Shading = new Coordinate(value); }
        public Medium? Env { get => Msr.Medium; set => Msr.Medium = value!; }
        public bool IsInfiniteLight => Type == BdptVertexType.Light && (Light == null || Light.IsInfinite);
        public bool IsOnSurface => N != new Vector3();
        public bool IsLight => Type == BdptVertexType.Light || (Type == BdptVertexType.Surface && Inct.IsLight);
        public bool IsConnectible
        {
            get => Type switch
            {
                BdptVertexType.Camera => true,
                BdptVertexType.Light => (Light!.Type & LightType.DeltaDirection) == 0,
                BdptVertexType.Surface => (Inct.Surface.Type & (BxdfType.Diffuse | BxdfType.Glossy | BxdfType.Reflection | BxdfType.Transmission)) != 0,
                BdptVertexType.Medium => true,
                _ => false,
            };
        }
        public bool IsDeltaLight => Type == BdptVertexType.Light && Light != null && (!Light.IsDelta);

        public float ConvertDensity(float pdf, ref BdptVertex next)
        {
            if (next.IsInfiniteLight) { return pdf; }
            Vector3 w = next.P - P;
            if (w.LengthSquared() == 0) return 0;
            float invDist2 = 1 / w.LengthSquared();
            if (next.IsOnSurface) { pdf *= AbsDot(next.N, w * Sqrt(invDist2)); }
            return pdf * invDist2;
        }

        public Color3F Le(Scene scene, ref BdptVertex v)
        {
            if (!IsLight) { return new Color3F(0.0f); }
            Vector3 w = v.P - P;
            if (w.LengthSquared() == 0) { return new Color3F(0.0f); }
            w = Normalize(w);
            if (IsInfiniteLight)
            {
                Color3F le = new Color3F(0.0f);
                foreach (var light in scene.InfiniteLights)
                {
                    le += light.Le(new Ray3F(P, -w));
                }
                return le;
            }
            else
            {
                AreaLight light = Inct.Light;
                Color3F l = light.L(Inct, w);
                return l;
            }
        }

        public Color3F F(ref BdptVertex next, TransportMode mode)
        {
            Vector3 wi = next.P - P;
            if (wi.LengthSquared() == 0) { return new Color3F(0.0f); }
            wi = Normalize(wi);
            return Type switch
            {
                BdptVertexType.Surface => Inct.Surface.Fr(Inct.ToLocal(Inct.Wr), Inct.ToLocal(wi), Inct, mode),
                BdptVertexType.Medium => new Color3F(Msr.Medium.P(Msr.Wo, wi)),
                _ => new Color3F(0.0f),
            };
        }

        public float PdfLightOrigin(Scene scene, ref BdptVertex v)
        {
            Vector3 w = v.P - P;
            if (w.LengthSquared() == 0) { return 0.0f; }
            w = Normalize(w);
            if (IsInfiniteLight)
            {
                return BidirectionalPathTracer.InfiniteLightDensity(scene, w);
            }
            else
            {
                Light light = Type == BdptVertexType.Light ? Light! : Inct.Light;
                var (pdfPos, _) = light.EmitPdf(P, w, N);
                return pdfPos * (1.0f / scene.Lights.Length);
            }
        }

        public float Pdf(Scene scene, ref BdptVertex prev, ref BdptVertex next)
        {
            if (Type == BdptVertexType.Light)
            {
                return PdfLight(scene, ref next);
            }
            Vector3 wn = next.P - P;
            if (wn.LengthSquared() == 0) { return 0; }
            wn = Normalize(wn);
            Vector3 wp = prev.P - P;
            if (wp.LengthSquared() == 0) { return 0; }
            wp = Normalize(wp);
            float pdf = Type switch
            {
                BdptVertexType.Camera => Camera!.PdfWe(new Ray3F(P, wn)).PdfDir,
                BdptVertexType.Surface => Inct.Surface.Pdf(Inct.ToLocal(wp), Inct.ToLocal(wn), Inct, TransportMode.Radiance),
                BdptVertexType.Medium => Msr.Medium.P(wp, wn),
                _ => 0,
            };
            return ConvertDensity(pdf, ref next);
        }

        public float PdfLight(Scene scene, ref BdptVertex v)
        {
            Vector3 w = v.P - P;
            float invDist2 = 1 / w.LengthSquared();
            w *= Sqrt(invDist2);
            float pdf;
            if (IsInfiniteLight)
            {
                pdf = 1 / (PI * Sqr(scene.InfiniteLights[0].WorldRadius * 20));
            }
            else
            {
                Light light = Type == BdptVertexType.Light ? Light! : Inct.Light;
                var (_, pdfDir) = light.EmitPdf(P, w, N);
                pdf = pdfDir * invDist2;
            }
            if (v.IsOnSurface) { pdf *= AbsDot(v.N, w); }
            return pdf;
        }
    }

    public class BdptPath
    {
        BdptVertex[] _v;
        int _count;

        public ref BdptVertex this[int i] => ref _v[i];

        public BdptPath()
        {
            _v = Array.Empty<BdptVertex>();
            _count = 0;
        }

        public int Allocate()
        {
            if (_count + 1 >= _v.Length)
            {
                Array.Resize(ref _v, Math.Max((int)(_v.Length * 1.5f), _v.Length + 1));
            }
            int i = _count;
            _count++;
            return i;
        }

        public void Clear()
        {
            _count = 0;
        }

        public static BdptVertex CreateCamera(Camera camera, Vector3 p, Color3F coeff)
        {
            return new BdptVertex
            {
                Type = BdptVertexType.Camera,
                Camera = camera,
                P = p,
                Coeff = coeff
            };
        }

        public static BdptVertex CreateLight(Light light, Vector3 p, Color3F coeff, float pdf)
        {
            return new BdptVertex
            {
                Type = BdptVertexType.Light,
                Light = light,
                P = p,
                Coeff = coeff,
                PdfForward = pdf
            };
        }

        public ref BdptVertex CreateCamera(Camera camera, Ray3F ray, Color3F coeff, Medium? env)
        {
            int index = Allocate();
            _v[index] = default;
            _v[index].Type = BdptVertexType.Camera;
            _v[index].Camera = camera;
            _v[index].P = ray.O;
            _v[index].Coeff = coeff;
            _v[index].Env = env;
            return ref _v[index];
        }

        public ref BdptVertex CreateLight(Light light, Vector3 p, Vector3 n, Color3F le, float pdf, Medium? env)
        {
            int index = Allocate();
            _v[index] = default;
            _v[index].Type = BdptVertexType.Light;
            _v[index].Light = light;
            _v[index].P = p;
            _v[index].N = n;
            _v[index].Coeff = le;
            _v[index].PdfForward = pdf;
            _v[index].Env = env;
            return ref _v[index];
        }

        public ref BdptVertex CreateMedium(SampleMediumResult msr, Color3F coeff, float pdf, ref BdptVertex prev)
        {
            int index = Allocate();
            _v[index] = default;
            _v[index].Type = BdptVertexType.Medium;
            _v[index].Msr = msr;
            _v[index].Coeff = coeff;
            _v[index].PdfForward = prev.ConvertDensity(pdf, ref _v[index]);
            return ref _v[index];
        }

        public ref BdptVertex CreateSurface(Intersection inct, Color3F coeff, float pdf, ref BdptVertex prev)
        {
            int index = Allocate();
            _v[index] = default;
            _v[index].Type = BdptVertexType.Surface;
            _v[index].Inct = inct;
            _v[index].Coeff = coeff;
            _v[index].PdfForward = prev.ConvertDensity(pdf, ref _v[index]);
            return ref _v[index];
        }
    }

    public class BidirectionalPathTracingRenderer : Renderer, IDisposable
    {
        readonly Scene _scene;
        readonly BidirectionalPathTracer _bdpt;
        readonly ColorBuffer _renderTarget;
        readonly Stopwatch _timer;
        readonly RenderBlockGenerator _blockGen;
        readonly ConsoleProgressBar _prograssBar;
        readonly int _sampleCount;
        readonly int _maxParallelTask;
        ThreadLocal<Random> _threadSafeRand;
        ThreadLocal<BdptPath> _cameraPath;
        ThreadLocal<BdptPath> _lightPath;
        CancellationTokenSource _taskToken;
        System.Timers.Timer _prograssBarDispatcher;
        Task? _renderTask;
        int _completedBlockCount;
        bool _hasException;
        bool _isCanceled;
        bool _disposedValue;

        public override ColorBuffer RenderTarget => _renderTarget;
        public override bool IsCompleted => _renderTask != null && _renderTask.IsCompleted;
        public override bool IsSuccess => !_hasException;
        public override int AllTaskCount => _blockGen.BlockCount;
        public override int CompletedTaskCount => _completedBlockCount;
        public override TimeSpan ElapsedTime => _timer.Elapsed;

        public BidirectionalPathTracingRenderer(Scene scene, BidirectionalPathTracer bdpt, int sampleCount, int blockSize, int maxTask)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _bdpt = bdpt ?? throw new ArgumentNullException(nameof(bdpt));
            _sampleCount = sampleCount;
            _maxParallelTask = maxTask;
            Camera camera = scene.MainCamera;
            _renderTarget = new ColorBuffer(camera.ScreenX, camera.ScreenY, 3);
            _threadSafeRand = new ThreadLocal<Random>(() => new Random(), false);
            _cameraPath = new ThreadLocal<BdptPath>(() => new BdptPath(), false);
            _lightPath = new ThreadLocal<BdptPath>(() => new BdptPath(), false);
            _taskToken = new CancellationTokenSource();
            _blockGen = new RenderBlockGenerator(RenderTarget.Width, RenderTarget.Height, blockSize);
            _timer = new Stopwatch();
            _prograssBar = new ConsoleProgressBar(AllTaskCount);
            _prograssBarDispatcher = new System.Timers.Timer(1000);
            _prograssBarDispatcher.Elapsed += UpdatePrograssBar;
            _renderTask = null;
            _completedBlockCount = 0;
            _hasException = false;
            _isCanceled = false;
            _disposedValue = false;
        }

        public override Task Start()
        {
            if (_renderTask != null) { return _renderTask; }
            Logger.Info("[Offline.Renderer] -> Rendering...");
            _timer.Restart();
            _prograssBar.Start();
            _prograssBarDispatcher.Start();
            ParallelOptions option = GetOption();
            Task renderTask = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        ParallelLoopResult result = Parallel.For(0, AllTaskCount, option, RenderTask);
                    }
                    catch (OperationCanceledException)
                    {
                        //ingore cancel exception
                    }
                },
                _taskToken.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
                .ContinueWith(OnRenderTaskCanceled, TaskContinuationOptions.OnlyOnCanceled)
                .ContinueWith(AfterRender);
            _renderTask = renderTask;
            return renderTask;
        }

        public override void Stop()
        {
            _taskToken.Cancel();
        }

        public override void Wait()
        {
            if (_renderTask == null) { throw new InvalidOperationException("no render task start"); }
            _renderTask.Wait();
        }

        private void RenderTask(int range, ParallelLoopState state)
        {
            if (!_blockGen.Next(out RenderBlock block))
            {
                return;
            }
            Random rand = _threadSafeRand.Value!;
            Camera camera = _scene.MainCamera;
            BdptPath cameraPath = _cameraPath.Value!;
            BdptPath lightPath = _lightPath.Value!;
            int offsetX = block.OffsetX;
            int offsetY = block.OffsetY;
            int sizeX = block.SizeX;
            int sizeY = block.SizeY;
            bool isCanceled = false;
            try
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int y = 0; y < sizeY; y++)
                    {
                        for (int t = 0; t < _sampleCount; t++)
                        {
                            if (_taskToken.IsCancellationRequested)
                            {
                                isCanceled = true;
                            }
                            if (isCanceled)
                            {
                                break;
                            }
                            int pointX = x + offsetX;
                            int pointY = y + offsetY;
                            Vector2 samplePoint = new Vector2(pointX, pointY) + rand.NextVec2();
                            int nCamera = _bdpt.GenerateCameraSubpath(samplePoint, camera, _scene, rand, cameraPath);
                            int nLight = _bdpt.GenerateLightSubpath(_scene, rand, lightPath);
                            _bdpt.Connect(nCamera, nLight, samplePoint, _renderTarget, _scene, rand, lightPath, cameraPath);
                            cameraPath.Clear();
                            lightPath.Clear();
                        }
                        if (isCanceled)
                        {
                            break;
                        }
                    }
                    if (isCanceled)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e);
                _hasException = true;
                state.Stop();
            }
            _prograssBar.Increase();
        }

        private ParallelOptions GetOption()
        {
            ParallelOptions result = new ParallelOptions();
            if (_maxParallelTask > 0)
            {
                result.MaxDegreeOfParallelism = _maxParallelTask;
            }
            result.CancellationToken = _taskToken.Token;
            return result;
        }

        private void UpdatePrograssBar(object? sender, ElapsedEventArgs e)
        {
            _prograssBar.Draw();
        }

        private void AfterRender(Task task)
        {
            _timer.Stop();
            _prograssBarDispatcher.Stop();
            _prograssBar.Stop();
            for (int i = 0; i < RenderTarget.Width; i++)
            {
                for (int j = 0; j < RenderTarget.Height; j++)
                {
                    RenderTarget.RefRGB(i, j) /= _sampleCount;
                }
            }
            if (_isCanceled)
            {
                Logger.Warn($"[Offline.Renderer] -> Render task is canceled");
            }
            else if (_hasException)
            {
                Logger.Warn($"[Offline.Renderer] -> Renderer throw exception");
            }
            else
            {
                Logger.Info($"[Offline.Renderer] -> Render task complete");
            }
        }

        private void OnRenderTaskCanceled(Task task)
        {
            _isCanceled = true;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _taskToken.Dispose();
                    _taskToken = null!;
                    _prograssBarDispatcher.Dispose();
                    _prograssBarDispatcher = null!;
                    _renderTask?.Dispose();
                    _renderTask = null;
                    _threadSafeRand.Dispose();
                    _threadSafeRand = null!;
                    _cameraPath.Dispose();
                    _cameraPath = null!;
                    _lightPath.Dispose();
                    _lightPath = null!;
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
