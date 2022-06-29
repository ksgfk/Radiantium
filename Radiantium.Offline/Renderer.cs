using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Renderer
    {
        /// <summary>
        /// 渲染目标
        /// </summary>
        public abstract ColorBuffer RenderTarget { get; }

        /// <summary>
        /// 渲染是否完成
        /// </summary>
        public abstract bool IsCompleted { get; }

        /// <summary>
        /// 渲染是否成功
        /// </summary>
        public abstract bool IsSuccess { get; }

        /// <summary>
        /// 渲染任务总数
        /// </summary>
        public abstract int AllTaskCount { get; }

        /// <summary>
        /// 已完成的渲染任务
        /// </summary>
        public abstract int CompletedTaskCount { get; }

        /// <summary>
        /// 从开始渲染已经经过的时间
        /// </summary>
        public abstract TimeSpan ElapsedTime { get; }

        /// <summary>
        /// 开始渲染
        /// </summary>
        /// <returns>渲染任务</returns>
        public abstract Task Start();

        /// <summary>
        /// 让当前线程等待渲染完成
        /// </summary>
        public abstract void Wait();

        /// <summary>
        /// 停止渲染
        /// </summary>
        public abstract void Stop();
    }
}
