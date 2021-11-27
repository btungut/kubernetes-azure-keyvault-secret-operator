using System.Diagnostics;

namespace OperatorFramework
{
    internal class CRDWatcher<T> where T : CRDBase
    {
        public IKubernetes Client { get; private set; }

        private readonly ICRDEventHandler<T> _eventHandler;
        private readonly CRDConfiguration _configuration;
        private Watcher<T> _watcher;
        private static readonly ILogger _logger = LoggerFactory.GetLogger<CRDWatcher<T>>();

        public CRDWatcher(Func<ICRDEventHandler<T>> eventHandlerCreator, IKubernetes client, CRDConfiguration configuration)
        {
            Client = client;
            _configuration = configuration;
            _eventHandler = eventHandlerCreator();
        }

        public async Task HandleAsync(CancellationToken cancellationToken)
        {
            _logger.Information("CRD watcher for {name} is starting. Operator may wait a while if there is no object in cluster.", $"{_configuration.Plural}.{_configuration.Group}");
            var response = await Client.ListClusterCustomObjectWithHttpMessagesAsync(_configuration.Group, _configuration.Version, _configuration.Plural, watch: true);
            _watcher = response.Watch<T, object>(async (_eventType, _crd) => await OnChange(_eventType, _crd).ConfigureAwait(false), OnError, OnClosed);

            await Task.Factory.StartNew(async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    TimeSpan delay = _configuration.ReconciliationFrequency.Subtract(stopwatch.Elapsed);

                    if (delay.TotalSeconds >= 1.0)
                        await Task.Delay(delay).ConfigureAwait(false);
                    else
                        _logger.Warning("ATTENTION! Last reconciliation {elapsed} took more than its frequency {frequency}. Please check the resources you assigned to operator. Low CPU, low frequency value or high secret load may lead this problem.", stopwatch.Elapsed, _configuration.ReconciliationFrequency);

                    try
                    {
                        stopwatch.Restart();
                        await _eventHandler.OnReconciliation(Client).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        LoggerFactory.GetLogger(_eventHandler.GetType())
                            .Error(e, "OnReconciliation is failed");
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }

                }
            }, TaskCreationOptions.LongRunning).Unwrap();
        }

        private async Task OnChange(WatchEventType eventType, T crd)
        {
            try
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                        await _eventHandler?.OnAdded(Client, crd);
                        break;
                    case WatchEventType.Modified:
                        await _eventHandler?.OnUpdated(Client, crd);
                        break;
                    case WatchEventType.Deleted:
                        await _eventHandler?.OnDeleted(Client, crd);
                        break;
                    case WatchEventType.Error:
                        await _eventHandler?.OnError(Client, crd);
                        break;
                    case WatchEventType.Bookmark:
                        await _eventHandler?.OnBookmarked(Client, crd);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                LoggerFactory.GetLogger(_eventHandler.GetType())
                    .Error(e, "{event} is failed", eventType.ToString());
            }
        }

        private void OnError(Exception exception)
        {
            _logger.Fatal(exception, "Watcher has been failed, operator will be restarted.");
            Environment.Exit(-1);
        }

        private void OnClosed()
        {
            _logger.Fatal("Watcher has been closed, operator will be restarted.");
            Environment.Exit(-1);
        }

        public static async Task<CRDWatcher<T>> CreateAsync(Func<ICRDEventHandler<T>> eventHandlerCreator, CRDConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration), "CRDConfiguration needs to be valid.");

            if (eventHandlerCreator == null)
                throw new ArgumentNullException(nameof(eventHandlerCreator));

            await ValidateAsync(Program.KubernetesClient, configuration);
            return new CRDWatcher<T>(eventHandlerCreator, Program.KubernetesClient, configuration);
        }

        private static async Task ValidateAsync(IKubernetes client, CRDConfiguration configuration)
        {
            string crdName = $"{configuration.Plural}.{configuration.Group}";
            _logger.Information("Checking CRD {name}", crdName);

            var apiResult = await client.InvokeAsync(c => c.ReadCustomResourceDefinitionAsync(crdName));
            if (!apiResult.IsSucceeded)
            {
                _logger.Error(apiResult.Exception, "CRD is not found!");

                string requestContent = apiResult.Exception.GetRequestContentIfPossible();
                string responseContent = apiResult.Exception.GetResponseContentIfPossible();
                _logger.Error("Operator validation is failed! Please check the installation steps again. {request} {response}", requestContent, responseContent);
                throw apiResult.Exception;
            }
        }
    }
}
