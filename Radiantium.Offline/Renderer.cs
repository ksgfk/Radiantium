using Radiantium.Core;
using System.Diagnostics;
using System.Numerics;

namespace Radiantium.Offline
{
    public struct RenderBlock
    {
        public int OffsetX;
        public int OffsetY;
        public int SizeX;
        public int SizeY;
    }

    public class Renderer
    {
        private class RenderBlockGenerator
        {
            public enum Direction : int
            {
                Right, Down, Left, Up
            }

            int m_blockX;
            int m_blockY;
            readonly int m_numBlockX;
            readonly int m_numBlockY;
            readonly int m_sizeX;
            readonly int m_sizeY;
            readonly int m_blockSize;
            readonly int m_blockCount;
            int m_numSteps;
            int m_blocksLeft;
            int m_stepsLeft;
            Direction m_direction;
            readonly object _lock;

            public int BlockCount => m_blockCount;

            public RenderBlockGenerator(int width, int height, int blockSize)
            {
                m_sizeX = width;
                m_sizeY = height;
                m_blockSize = blockSize;
                m_numBlockX = (int)Math.Ceiling(width / (float)blockSize);
                m_numBlockY = (int)Math.Ceiling(height / (float)blockSize);
                m_blockCount = m_numBlockX * m_numBlockY;
                m_blocksLeft = BlockCount;
                m_direction = Direction.Right;
                m_blockX = m_numBlockX / 2;
                m_blockY = m_numBlockY / 2;
                m_stepsLeft = 1;
                m_numSteps = 1;
                _lock = new object();
            }

            public bool Next(out RenderBlock block)
            {
                lock (_lock)
                {
                    if (m_blocksLeft == 0)
                    {
                        block = default;
                        return false;
                    }
                    int posX = m_blockX * m_blockSize;
                    int posY = m_blockY * m_blockSize;
                    block.OffsetX = posX;
                    block.OffsetY = posY;
                    int sizeX = m_sizeX - posX;
                    int sizeY = m_sizeY - posY;
                    sizeX = Math.Min(sizeX, m_blockSize);
                    sizeY = Math.Min(sizeY, m_blockSize);
                    block.SizeX = sizeX;
                    block.SizeY = sizeY;
                    if (--m_blocksLeft == 0)
                    {
                        return true;
                    }
                    do
                    {
                        switch (m_direction)
                        {
                            case Direction.Up:
                                ++m_blockY;
                                break;
                            case Direction.Down:
                                --m_blockY;
                                break;
                            case Direction.Left:
                                --m_blockX;
                                break;
                            case Direction.Right:
                                ++m_blockX;
                                break;
                        }
                        if (--m_stepsLeft == 0)
                        {
                            m_direction = (Direction)(((int)m_direction + 1) % 4);
                            if (m_direction == Direction.Left || m_direction == Direction.Right)
                            {
                                ++m_numSteps;
                            }
                            m_stepsLeft = m_numSteps;
                        }
                    } while ((m_blockX < 0 || m_blockY < 0) || (m_blockX >= m_numBlockX) || (m_blockY >= m_numBlockY));
                }
                return true;
            }
        }

        readonly Scene _scene;
        readonly Camera _camera;
        readonly Integrator _integrator;
        readonly ColorBuffer _renderTarget;
        readonly ThreadLocal<Random> _rnd;
        readonly CancellationTokenSource _token;
        readonly int _sampleCount;
        readonly int _maxTask;
        readonly RenderBlockGenerator _gen;
        readonly Stopwatch _timer;
        bool _isRendering;
        bool _isSuccess;
        int _nowCount;

        public ColorBuffer RenderTarget => _renderTarget;
        public bool IsComplete => _nowCount == _gen.BlockCount;
        public bool IsSuccess => _isSuccess;
        public int CompleteBlockCount => _nowCount;
        public int AllBlockCount => _gen.BlockCount;
        public TimeSpan RenderUseTime => _timer.Elapsed;
        public int SampleCount => _sampleCount;

        public event Action<RenderBlock>? CompleteBlock;

        public Renderer(Scene scene, Camera camera, Integrator integrator, int sampleCount, int maxTask = -1)
        {
            _scene = scene;
            _camera = camera;
            _integrator = integrator;
            _renderTarget = new ColorBuffer(camera.ScreenX, camera.ScreenY, 3);
            _sampleCount = sampleCount;
            _maxTask = maxTask;
            _rnd = new ThreadLocal<Random>(() => new Random(), true);
            _token = new CancellationTokenSource();
            _gen = new(RenderTarget.Width, RenderTarget.Height, 32);
            _timer = new Stopwatch();
            _nowCount = 0;
            _isRendering = false;
            _isSuccess = false;
        }

        public Task Start()
        {
            return Task.Factory.StartNew(Render, _token.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void Render()
        {
            if (_isRendering)
            {
                return;
            }
            _isRendering = true;
            Logger.Info("[Offline.Renderer] -> Rendering...");
            _timer.Restart();
            var option = GetOption();
            ParallelLoopResult result = Parallel.For(0, _gen.BlockCount, option, RenderTask);
            _timer.Stop();
            _isSuccess = result.IsCompleted;
        }

        private void RenderTask(int range, ParallelLoopState state)
        {
            if (!_gen.Next(out RenderBlock block))
            {
                return;
            }
            Random rand = _rnd.Value!;
            int offsetX = block.OffsetX;
            int offsetY = block.OffsetY;
            int sizeX = block.SizeX;
            int sizeY = block.SizeY;
            try
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int y = 0; y < sizeY; y++)
                    {
                        for (int t = 0; t < _sampleCount; t++)
                        {
                            int pointX = x + offsetX;
                            int pointY = y + offsetY;
                            Vector2 samplePoint = new Vector2(pointX, pointY) + rand.NextVec2();
                            Ray3F ray = _camera.SampleRay(samplePoint);
                            var color = _integrator.Li(ray, _scene, rand);
                            if (!color.IsValid)
                            {
                                Logger.Warn($"[Offline.Renderer] -> Invalid color:{color}");
                                color = new Color3F(0.0f);
                            }
                            _renderTarget.RefRGB(pointX, pointY) += color / _sampleCount;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e);
                state.Stop();
            }
            Interlocked.Add(ref _nowCount, 1);
            CompleteBlock?.Invoke(block);
        }

        public void Stop() { _token.Cancel(); }

        private ParallelOptions GetOption()
        {
            var result = new ParallelOptions();
            if (_maxTask > 0)
            {
                result.MaxDegreeOfParallelism = _maxTask;
            }
            result.CancellationToken = _token.Token;
            return result;
        }
    }
}
