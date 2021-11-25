using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Operator.Domain
{
    internal class AzureKeyVaultClient
    {
        private static ConcurrentDictionary<string, AzureKeyVaultClient> _clients = new ConcurrentDictionary<string, AzureKeyVaultClient>();
        private static readonly ILogger _logger = LoggerFactory.GetLogger<AzureKeyVaultClient>();

        private readonly Uri _uri;
        private readonly SecretClient _secretClient;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly IAsyncPolicy _wrappedPolicy;

        private AzureKeyVaultClient(string keyVaultName, string tenantId, string clientId, string clientSecret)
        {
            _uri = new Uri($"https://{keyVaultName}.vault.azure.net");
            var cred = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var opt = new SecretClientOptions();
            opt.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);
            _secretClient = new SecretClient(_uri, cred, opt);

            _retryPolicy = Policy
                .Handle<Azure.RequestFailedException>(o => o.Status >= 500)
                .OrInner<Azure.RequestFailedException>(o => o.Status >= 500)
                .WaitAndRetryAsync(3, (attempt) => TimeSpan.FromSeconds(attempt), (e, ts, attempt, context) =>
                {
                    _logger.Error(e, "Azure KeyVault operation is failed with {attempt} times", attempt);
                });

            _circuitBreakerPolicy = Policy
                .Handle<AuthenticationFailedException>()
                .Or<AuthenticationRequiredException>()
                .Or<CredentialUnavailableException>()
                .Or<Azure.RequestFailedException>()
                .OrInner<AuthenticationFailedException>()
                .OrInner<AuthenticationRequiredException>()
                .OrInner<CredentialUnavailableException>()
                .OrInner<Azure.RequestFailedException>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(15),
                    (e, ts, ctx) =>
                    {
                        _logger.Error(e, "Authentication/Request failed for {uri} this KeyVaultClient is breaking for {ts}", _uri.AbsolutePath, ts);
                    },
                    (ctx) =>
                    {
                        _logger.Information("Authentication/Request succeeded for {uri} after breaking", _uri.AbsolutePath);
                    },
                    () =>
                    {

                    });

            _wrappedPolicy = Policy.WrapAsync(_circuitBreakerPolicy, _retryPolicy);
        }

        public static AzureKeyVaultClient GetOrCreate(string keyVaultName, string tenantId, string clientId, string clientSecret)
        {
            string key = string.Join('/', tenantId, keyVaultName, clientId);
            _logger.Debug("GetOrCreate for {key}", key);
            return _clients.GetOrAdd(key, (_) => new AzureKeyVaultClient(keyVaultName, tenantId, clientId, clientSecret));
        }

        public async Task<Result<string>> GetSecretAsync(string secretName)
        {
            try
            {
                return await _wrappedPolicy.ExecuteAsync(async () =>
                {
                    var response = await _secretClient.GetSecretAsync(secretName);
                    return new Result<string>(response.Value.Value);
                });
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException e)
            {
                _logger.Error("Circuit breaker is in open state");
                return new Result<string>(e);
            }
            catch (Exception e)
            {
                _logger.Error(e, "GetSecretAsync : Azure response is not succeeded for {name}", secretName);
                return new Result<string>(e);
            }
        }
    }
}
