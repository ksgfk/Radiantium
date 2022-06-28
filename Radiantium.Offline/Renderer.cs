using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Renderer
    {
        public abstract ColorBuffer RenderTarget { get; }
        public abstract bool IsCompleted { get; }
        public abstract bool IsSuccess { get; }
        public abstract int AllTaskCount { get; }
        public abstract int CompletedTaskCount { get; }
        public abstract TimeSpan ElapsedTime { get; }

        public abstract Task Start();

        public abstract void Wait();

        public abstract void Stop();
    }
}
