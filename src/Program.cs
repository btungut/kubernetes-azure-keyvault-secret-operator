
using Microsoft.Extensions.Configuration;
using Serilog.Formatting.Compact;
using System.Diagnostics;

namespace Operator
{
    //TODO: status
    public class Program
    {
        internal static AppConfiguration AppConfiguration;
        internal static IKubernetes KubernetesClient;

        private static ILogger _logger;
        private static CancellationTokenSource _ctx = new CancellationTokenSource();

        public static async Task Main(string[] args)
        {
            Bootstrap();
            ConfigureLogging();

            _logger.Information("Process is started");
            _logger.Information("Configuration is {@config}", AppConfiguration);

            ConfigureKubernetesClient();

            await RunOperatorAsync(_ctx.Token).ConfigureAwait(false);

            _logger.Warning("Process is exiting...");
        }

        private static async Task RunOperatorAsync(CancellationToken cancellationToken)
        {
            var crdConfiguration = CRDConfiguration.Create<AzureKeyVault>(
                AppConfiguration.ReconciliationFrequency,
                Constants.CrdApiGroup, 
                Constants.CrdApiVersion, 
                Constants.CrdPlural,
                Constants.CrdSingular);

            var crdWatcher = await CRDWatcher<AzureKeyVault>.CreateAsync(() => new AzureKeyVaultHandler(), crdConfiguration);
            await crdWatcher.HandleAsync(cancellationToken);
        }

        private static void ConfigureKubernetesClient()
        {
            KubernetesClientConfiguration config;
            if (KubernetesClientConfiguration.IsInCluster())
            {
                _logger.Information("InClusterConfig is applied");
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                _logger.Information("Cluster couldn't be detected, ConfigFile is loading...");
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            }
            
            KubernetesClient = new Kubernetes(config);
            KubernetesClient.HttpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        private static void Bootstrap()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            AppConfiguration = config.Get<AppConfiguration>();
            if(Debugger.IsAttached)
            {
                AppConfiguration.LogLevel = Serilog.Events.LogEventLevel.Verbose;
            }

            //Validation
            if (AppConfiguration.ReconciliationFrequency < TimeSpan.FromSeconds(10))
                throw new ArgumentOutOfRangeException(nameof(AppConfiguration.ReconciliationFrequency), "ReconciliationFrequency couldn't be less than 10 seconds.");
        }

        private static void ConfigureLogging()
        {
            const string template = "{Timestamp:o} [{Level:u3}] [{ThreadId}] ({ShortSource}) : {Message:lj}{NewLine}{Exception}";
            var builder = new LoggerConfiguration();

            switch (AppConfiguration.LogLevel)
            {
                case Serilog.Events.LogEventLevel.Verbose:
                    builder = builder.MinimumLevel.Verbose();
                    break;
                case Serilog.Events.LogEventLevel.Debug:
                    builder = builder.MinimumLevel.Debug();
                    break;
                case Serilog.Events.LogEventLevel.Information:
                    builder = builder.MinimumLevel.Information();
                    break;
                case Serilog.Events.LogEventLevel.Warning:
                    builder = builder.MinimumLevel.Warning();
                    break;
                case Serilog.Events.LogEventLevel.Error:
                    builder = builder.MinimumLevel.Error();
                    break;
                case Serilog.Events.LogEventLevel.Fatal:
                    builder = builder.MinimumLevel.Fatal();
                    break;
                default:
                    builder = builder.MinimumLevel.Information();
                    break;
            }

            builder = (AppConfiguration.EnableJsonLogging) ?
                builder.WriteTo.Console(new RenderedCompactJsonFormatter()) :
                builder.WriteTo.Console(outputTemplate: template);

            Log.Logger = builder
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .CreateLogger();
            _logger = LoggerFactory.GetLogger<Program>();
        }
    }
}