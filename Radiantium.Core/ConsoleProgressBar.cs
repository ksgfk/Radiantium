namespace Radiantium.Core
{
    public class ConsoleProgressBar
    {
        readonly int _total;
        readonly int _width;
        readonly char _complete;
        readonly char _nowProgress;
        readonly char _incomplete;
        int _current;
        DateTime _start;

        public ConsoleProgressBar(int total, int width = -1, char complete = '=', char progress = '>', char incomplete = ' ')
        {
            if (width == -1)
            {
                width = Math.Max(32, Console.WindowWidth - 32);
            }
            _total = total;
            _width = width;
            _complete = complete;
            _nowProgress = progress;
            _incomplete = incomplete;
        }

        public void Start()
        {
            _start = DateTime.Now;
        }

        public void Increase()
        {
            lock (this)
            {
                _current++;
            }
        }

        public void Draw()
        {
            lock (this)
            {
                DateTime now = DateTime.Now;
                float progress = (float)_current / _total;
                int pos = (int)(_width * progress);
                var (left, top) = Console.GetCursorPosition();
                Console.SetCursorPosition(0, top);
                Console.Write('[');
                for (int i = 0; i < _width; i++)
                {
                    if (i < pos)
                    {
                        Console.Write(_complete);
                    }
                    else if (i == pos)
                    {
                        Console.Write(_nowProgress);
                    }
                    else
                    {
                        Console.Write(_incomplete);
                    }
                }
                Console.Write($"] {progress * 100:0.00}% {(now - _start).TotalSeconds:0.00}s");
                Console.Out.Flush();
            }
        }

        public void Stop()
        {
            Console.WriteLine();
        }
    }
}
