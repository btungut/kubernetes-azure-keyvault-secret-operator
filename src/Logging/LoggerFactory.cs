namespace Operator.Logging
{
    internal static class LoggerFactory
    {
        private static ConcurrentDictionary<Type, ILogger> _loggers = new ConcurrentDictionary<Type, ILogger>();

        public static ILogger GetLogger<T>() => GetLogger(typeof(T));
        public static ILogger GetLogger(Type type)
        {
            return _loggers.GetOrAdd(type, (_) => Serilog.Log.Logger.ForContext(new ShortLogContextEnricher(type)));
        }
    }
}
