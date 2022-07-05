using Radiantium.Core;
using System.Diagnostics;
using System.Numerics;
using System.Timers;

namespace Radiantium.Offline.Renderers
{
    public class BlockBasedRenderer : Renderer, IDisposable
    {
        readonly Scene _scene;
        readonly MonteCarloIntegrator _integrator;
        readonly ColorBuffer _renderTarget;
        readonly Stopwatch _timer;
        readonly RenderBlockGenerator _blockGen;
        readonly ConsoleProgressBar _prograssBar;
        readonly int _sampleCount;
        readonly int _maxParallelTask;
        ThreadLocal<Random> _threadSafeRand;
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

        public BlockBasedRenderer(Scene scene, MonteCarloIntegrator integrator, int sampleCount, int blockSize, int maxTask)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
            _sampleCount = sampleCount;
            _maxParallelTask = maxTask;
            Camera camera = scene.MainCamera;
            _renderTarget = new ColorBuffer(camera.ScreenX, camera.ScreenY, 3);
            _threadSafeRand = new ThreadLocal<Random>(() => new Random(), false);
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

        private void RenderTask(int range, ParallelLoopState state)
        {
            if (!_blockGen.Next(out RenderBlock block))
            {
                return;
            }
            Random rand = _threadSafeRand.Value!;
            Camera camera = _scene.MainCamera;
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
                            Ray3F ray = camera.SampleRay(samplePoint);
                            var color = _integrator.Li(ray, _scene, rand);
                            if (!color.IsValid)
                            {
                                Logger.Warn($"[Offline.Renderer] -> Invalid color: {color} at <{pointX},{pointY}>");
                                color = new Color3F(0.0f);
                            }
                            _renderTarget.RefRGB(pointX, pointY) += color;
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
                Interlocked.Add(ref _completedBlockCount, 1);
                _prograssBar.Increase();
            }
            catch (Exception e)
            {
                Logger.Exception(e);
                _hasException = true;
                state.Stop();
            }
            finally
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int y = 0; y < sizeY; y++)
                    {
                        int pointX = x + offsetX;
                        int pointY = y + offsetY;
                        _renderTarget.RefRGB(pointX, pointY) /= _sampleCount;
                    }
                }
            }
        }

        private void AfterRender(Task task)
        {
            _timer.Stop();
            _prograssBarDispatcher.Stop();
            _prograssBar.Stop();
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

        private void UpdatePrograssBar(object? sender, ElapsedEventArgs e)
        {
            _prograssBar.Draw();
        }

        protected virtual void Dispose(bool disposing)
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
