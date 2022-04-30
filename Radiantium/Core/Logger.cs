namespace Radiantium.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static Action<string, LogLevel>? _proxyLog;

        public static Action<string, LogLevel>? SetLogProxy(Action<string, LogLevel> proxy)
        {
            var t = _proxyLog;
            _proxyLog = proxy;
            return t;
        }

        private static void Log(string msg, LogLevel level)
        {
            if (_proxyLog == null)
            {
                Console.ForegroundColor = level switch
                {
                    LogLevel.Debug => ConsoleColor.Green,
                    LogLevel.Info => ConsoleColor.White,
                    LogLevel.Warning => ConsoleColor.Yellow,
                    LogLevel.Error => ConsoleColor.Red,
                    _ => ConsoleColor.White
                };
                Console.WriteLine(msg);
            }
            else
            {
                _proxyLog(msg, level);
            }
        }

        public static void Debug(string msg) { Log(msg, LogLevel.Debug); }

        public static void Debug(string fmt, params object[] objs) { Log(string.Format(fmt, objs), LogLevel.Debug); }

        public static void Info(string msg) { Log(msg, LogLevel.Info); }

        public static void Info(string fmt, params object[] objs) { Log(string.Format(fmt, objs), LogLevel.Info); }

        public static void Warn(string msg) { Log(msg, LogLevel.Warning); }

        public static void Warn(string fmt, params object[] objs) { Log(string.Format(fmt, objs), LogLevel.Warning); }

        public static void Error(string msg) { Log(msg, LogLevel.Error); }

        public static void Error(string fmt, params object[] objs) { Log(string.Format(fmt, objs), LogLevel.Error); }

        public static void Exception(Exception e) { Log(e.ToString(), LogLevel.Error); }
    }
}