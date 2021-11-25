namespace OperatorFramework
{
    public class CRDConfiguration
    {
        internal static ConcurrentDictionary<Type, CRDConfiguration> _configurations = new ConcurrentDictionary<Type, CRDConfiguration>();

        private CRDConfiguration(TimeSpan reconciliationFrequency, string group, string version, string plural, string singular)
        {
            ReconciliationFrequency = reconciliationFrequency;
            Group = group;
            Version = version;
            Plural = plural;
            Singular = singular;
        }

        public static CRDConfiguration Create<T>(TimeSpan reconciliationFrequency, string group, string version, string plural, string singular) where T : CRDBase
        {
            var configuration = new CRDConfiguration(reconciliationFrequency, group, version, plural, singular);
            _configurations.TryAdd(typeof(T), configuration);
            return configuration;
        }

        public TimeSpan ReconciliationFrequency { get; private set; }
        public string Group { get; private set; }
        public string Version { get; private set; }
        public string Plural { get; private set; }
        public string Singular { get; private set; }
    }
}
