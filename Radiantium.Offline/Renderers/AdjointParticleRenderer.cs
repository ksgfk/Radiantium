using Radiantium.Core;
using Radiantium.Offline.Integrators;
using System.Diagnostics;
using System.Timers;

namespace Radiantium.Offline.Renderers
{
    public sealed class AdjointParticleRenderer : Renderer, IDisposable
    {
        readonly Scene _scene;
        readonly AdjointParticleIntegrator _integrator;
        readonly ColorBuffer _renderTarget;
        readonly Stopwatch _timer;
        readonly ConsoleProgressBar _prograssBar;
        readonly int _particleCount;
        readonly int _maxParallelTask;
        readonly int _allTaskCount;
        ThreadLocal<Random> _threadSafeRand;
        CancellationTokenSource _taskToken;
        System.Timers.Timer _prograssBarDispatcher;
        Task? _renderTask;
        int _completedTaskCount;
        int _emitParticleCount;
        bool _hasException;
        bool _isCanceled;
        bool _disposedValue;

        public override ColorBuffer RenderTarget => _renderTarget;
        public override bool IsCompleted => _renderTask != null && _renderTask.IsCompleted;
        public override bool IsSuccess => !_hasException;
        public override int AllTaskCount => _allTaskCount;
        public override int CompletedTaskCount => _completedTaskCount;
        public override TimeSpan ElapsedTime => _timer.Elapsed;

        public AdjointParticleRenderer(Scene scene, AdjointParticleIntegrator integrator, int particleCount, int maxTask)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
            _particleCount = particleCount;
            _maxParallelTask = maxTask;
            Camera camera = scene.MainCamera;
            _renderTarget = new ColorBuffer(camera.ScreenX, camera.ScreenY, 3);
            _threadSafeRand = new ThreadLocal<Random>(() => new Random(), false);
            _taskToken = new CancellationTokenSource();
            _timer = new Stopwatch();
            _prograssBar = new ConsoleProgressBar(_particleCount);
            _prograssBarDispatcher = new System.Timers.Timer(1000);
            _prograssBarDispatcher.Elapsed += UpdatePrograssBar;
            _renderTask = null;
            _completedTaskCount = 0;
            _hasException = false;
            _isCanceled = false;
            _disposedValue = false;

            if (_maxParallelTask <= 0)
            {
                _allTaskCount = Environment.ProcessorCount;
            }
            else
            {
                _allTaskCount = _maxParallelTask;
            }
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
            throw new NotImplementedException();
        }

        public override void Wait()
        {
            throw new NotImplementedException();
        }

        private void UpdatePrograssBar(object? sender, ElapsedEventArgs e)
        {
            _prograssBar.SetValue(_emitParticleCount);
            _prograssBar.Draw();
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
            Random rand = _threadSafeRand.Value!;
            int extra = range < (_particleCount % _allTaskCount) ? 1 : 0;
            int emitParticle = _particleCount / _allTaskCount + extra;
            try
            {
                for (int i = 0; i < emitParticle; i++)
                {
                    if (_taskToken.IsCancellationRequested)
                    {
                        break;
                    }
                    _integrator.EmitParticle(RenderTarget, _scene, rand);
                    Interlocked.Increment(ref _emitParticleCount);
                }
                Interlocked.Increment(ref _completedTaskCount);
            }
            catch (Exception e)
            {
                Logger.Exception(e);
                _hasException = true;
                state.Stop();
            }
        }

        private void AfterRender(Task task)
        {
            _timer.Stop();
            _prograssBarDispatcher.Stop();
            _prograssBar.Stop();

            Camera camera = _scene.MainCamera;

            if (_emitParticleCount != _particleCount) { throw new Exception(); }

            float pathCount = _emitParticleCount;
            float v = (float)camera.ScreenX * camera.ScreenY / pathCount;
            for (int x = 0; x < camera.ScreenX; x++)
            {
                for (int y = 0; y < camera.ScreenY; y++)
                {
                    RenderTarget.RefRGB(x, y) *= v;
                }
            }

            for (int x = 0; x < camera.ScreenX; x++)
            {
                for (int y = 0; y < camera.ScreenY; y++)
                {
                    RenderTarget.RefRGB(x, y) += _integrator.CameraTraceLight(camera.SampleRay(new(x, y)), _scene);
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
