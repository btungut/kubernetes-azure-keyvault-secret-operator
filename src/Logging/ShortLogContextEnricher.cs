using Serilog.Core;
using Serilog.Events;

namespace Operator.Logging
{
    internal class ShortLogContextEnricher : ILogEventEnricher
    {
        public const string PropertyName = "ShortSource";
        private readonly string _name;

        public ShortLogContextEnricher(Type type)
        {
            _name = type.FullName.Replace(type.Assembly.GetName().Name, string.Empty).Trim('.');

            int index = _name.IndexOf('`');
            if (index > 0)
                _name = _name.Substring(0, index);

            index = _name.IndexOf('\'');
            if (index > 0)
                _name = _name.Substring(0, index);
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var property = propertyFactory.CreateProperty(PropertyName, _name, false);
            logEvent.AddOrUpdateProperty(new LogEventProperty(PropertyName, property.Value));
        }
    }
}
