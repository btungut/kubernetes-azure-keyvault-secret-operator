namespace Operator.Domain
{
    internal class ServicePrincipalSecretContainer
    {
        private static ILogger _logger = LoggerFactory.GetLogger<ServicePrincipalSecretContainer>();
        private static readonly Dictionary<Resource, V1Secret> _secrets = new Dictionary<Resource, V1Secret>();
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan _maxAge = TimeSpan.FromMinutes(5);

        private static DateTime _lastFlushed = DateTime.MinValue;

        public static async Task<V1Secret> GetAsync(string @namespace, string name)
        {
            //TODO : çok iyi test et yeni using kulanımı var
            //TODO : cache test
            CacheControl();

            var resource = GetKey(@namespace, name);
            V1Secret result;
            if (_secrets.TryGetValue(resource, out result))
                return result;

            await _semaphore.WaitAsync();
            using (new SemaphoreSlimDisposable(_semaphore))
            {
                if (_secrets.TryGetValue(resource, out result))
                {
                    return result;
                }

                var apiResult = await Program.KubernetesClient.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));
                if (apiResult.Status == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.Error("Secret is not found at {resource}", resource);
                    return null;
                }
                else if (!apiResult.IsSucceeded)
                {
                    _logger.Error(apiResult.Exception, "Secret couldn't be fetched! {resource}", resource);
                    return null;
                }

                _logger.Debug("Secret is found {resource}", resource);
                result = apiResult.Data;
                result.StringData = new Dictionary<string, string>();
                foreach (var item in result.Data)
                {
                    result.StringData[item.Key] = Encoding.UTF8.GetString(item.Value);
                }

                _secrets.Add(resource, result);
                return result;
            }
        }

        private static Resource GetKey(string @namespace, string name) => new Resource(@namespace, name);

        private static void CacheControl()
        {
            if (DateTime.UtcNow >= _lastFlushed.Add(_maxAge))
            {
                Flush();
            }
        }

        internal static void Flush()
        {
            _secrets.Clear();
            _lastFlushed = DateTime.UtcNow;
        }
    }
}
