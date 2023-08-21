using System.Diagnostics;

namespace Operator.Handlers
{
    public class AzureKeyVaultHandler : ICRDEventHandler<AzureKeyVault>
    {
        private static ILogger _logger = LoggerFactory.GetLogger<AzureKeyVaultHandler>();

        internal static ObjectContext<Resource, AzureKeyVault> Context = new ObjectContext<Resource, AzureKeyVault>();
        private static AzureKeyVaultJob _job = new AzureKeyVaultJob();
        private static JobRuner<AzureKeyVault> _jobRunner = new JobRuner<AzureKeyVault>(_job);

        public async Task OnAdded(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);
            await Context.SetAsync(key, crd);
            _jobRunner.Enqueue(crd);
        }

        public async Task OnUpdated(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);
            bool isSyncVersionChanged = false;

            await Context.ExclusiveAsync(key, (oldCrd) =>
            {
                isSyncVersionChanged = oldCrd.Spec.SyncVersion != crd.Spec.SyncVersion;
                return Task.CompletedTask;
            });

            if (isSyncVersionChanged)
            {
                ServicePrincipalSecretContainer.Flush();
            }

            await Context.SetAsync(key, crd);
            _jobRunner.Enqueue(crd);
        }

        public async Task OnDeleted(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);

            await Context.ExclusiveAsync(key, async (_) =>
            {
                await FinalizeSecretsAsync(client, crd);
            });

            await Context.RemoveAsync(key);
        }

        public async Task OnReconciliation(IKubernetes client)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var bag = await Context.GetSnapshotAsync();

            _logger.Information("OnReconciliation is starting");

            var clusterNamespaces = (await client.ListNamespaceAsync()).Items.Select(x => x.Metadata.Name).ToArray();
            await ReconcileManagedSecrets(bag, client, clusterNamespaces);
            await ReconcileDanglingSecrets(bag, client, clusterNamespaces);

            stopwatch.Stop();
            _logger.Information("OnReconciliation is finished in {elasped} ms", stopwatch.ElapsedMilliseconds);
        }

        private async Task ReconcileDanglingSecrets(KeyValuePair<Resource, AzureKeyVault>[] currentBag, IKubernetes client, string[] clusterNamespaces)
        {
            Stopwatch stopwatch = (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug)) ? Stopwatch.StartNew() : null;

            var clusterSecrets = new List<Resource>();

            foreach (var ns in clusterNamespaces)
            {
                var secrets = await client.ListNamespacedSecretAsync(ns, labelSelector: $"{Constants.SecretLabelKey}={Constants.SecretLabelValue}");
                clusterSecrets.AddRange(secrets.Items.Select(s => new Resource(s.Namespace(), s.Name())));
            }

            var managedSecrets = new List<Resource>();
            foreach (var azureKeyVault in currentBag)
            {
                var resolved = PatternResolver.ResolveManagedSecrets(clusterNamespaces, azureKeyVault.Value.Spec.ManagedSecrets);
                managedSecrets.AddRange(resolved);
            }


            var danglingSecrets = clusterSecrets.Except(managedSecrets).ToArray();
            _logger.Information("Total {count} dangling secrets found {list}", danglingSecrets.Length, danglingSecrets);
            int succeededCount = 0;
            foreach (var secret in danglingSecrets)
            {
                var apiResult = await client.InvokeAsync(c => c.DeleteNamespacedSecretAsync(secret.Name, secret.Namespace));
                if (apiResult.IsSucceeded)
                {
                    succeededCount++;
                    _logger.Debug("Dangling secret {resource} is deleted", secret);
                }
                else
                {
                    _logger.Warning("Danling secret {resource} couldn't be deleted", secret);
                }
            }

            if (succeededCount > 0)
                _logger.Information("Danling secrets are deleted {succeeded}/{total}", succeededCount, danglingSecrets.Length);

            if(stopwatch != null)
            {
                stopwatch.Stop();
                _logger.Debug("ReconcileDanglingSecrets finished in {elasped} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task FinalizeSecretsAsync(IKubernetes client, AzureKeyVault crd)
        {
            var clusterNamespaces = (await client.ListNamespaceAsync()).Items.Select(x => x.Metadata.Name).ToArray();
            var managedSecrets = PatternResolver.ResolveManagedSecrets(clusterNamespaces, crd.Spec.ManagedSecrets);
            _logger.Information("Secret finalization is starting for {@resource}", managedSecrets);

            foreach (var secret in managedSecrets)
            {
                var apiResult = await client.InvokeAsync(c => c.DeleteNamespacedSecretAsync(secret.Name, secret.Namespace));

                if (apiResult.IsSucceeded)
                {
                    _logger.Information("Secret is deleted successfully {resource}", secret);
                }
                else if (apiResult.Status != System.Net.HttpStatusCode.NotFound)
                {
                    _logger.Warning(apiResult.Exception, "Secret couldn't be deleted {resource}", secret);
                }
            }
        }

        private async Task ReconcileManagedSecrets(KeyValuePair<Resource, AzureKeyVault>[] currentBag, IKubernetes client, string[] clusterNamespaces)
        {
            Stopwatch stopwatch = (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug)) ? Stopwatch.StartNew() : null;

            foreach (var crd in currentBag)
            {
                var secretsNeedsToBeValid = PatternResolver.ResolveManagedSecrets(clusterNamespaces, crd.Value.Spec.ManagedSecrets);
                foreach (var secret in secretsNeedsToBeValid)
                {
                    var forceUpdateFreq = Program.AppConfiguration.ForceUpdateFrequency;
                    if (!(forceUpdateFreq.HasValue && _jobRunner.LastExecutedAt.HasValue && (DateTime.UtcNow - _jobRunner.LastExecutedAt) >= forceUpdateFreq))
                    {
                        var apiResult = await client.InvokeAsync(c => c.ReadNamespacedSecretAsync(secret.Name, secret.Namespace));

                        var secretSyncVersion = apiResult.Data?.GetAnnotation(Constants.SecretSyncVersionAnnotation);
                        if (apiResult.IsSucceeded && secretSyncVersion != null && Convert.ToInt32(secretSyncVersion) == crd.Value.Spec.SyncVersion)
                        {
                            _logger.Information("OnReconciliation : {resource} syncVersion:{version} is exist and syncVersions are same, no need to take action.", secret, secretSyncVersion);
                            continue;
                        }

                        //Secret is found but syncVersion is null (maybe manually deleted)
                        if (apiResult.IsSucceeded && secretSyncVersion == null)
                        {
                            _logger.Warning("OnReconciliation : syncVersion of Secret is null, it will be processed.");
                        }
                        //Secret is found but syncVersion is changed
                        else if (apiResult.IsSucceeded && secretSyncVersion != null)
                        {
                            _logger.Warning(
                                "OnReconciliation : Expected {crdVersion} and actual {secretVersion} is not same, it will be processed.",
                                crd.Value.Spec.SyncVersion, secretSyncVersion ?? "(null)");
                        }
                        //Secret is not found and we still responsible to manage it.
                        else if (apiResult.Status == System.Net.HttpStatusCode.NotFound && Context.IsExist(crd.Key))
                        {
                            _logger.Warning(
                                "OnReconciliation : Secret {secretResource} for {crdResource} is not found, it will be processed.",
                                crd.Key, secret);
                        }
                        else
                        {
                            _logger.Error(
                                apiResult.Exception, "OnReconciliation : Unexpected case {secretResource} {crdResource}",
                                crd.Key, secret);
                        }
                    }
                    else
                    {
                        _logger.Information("ForceUpdateFrequency ({freq} seconds) is passed. Secrets will be processed...", forceUpdateFreq.Value.TotalSeconds);
                    }
                    

                    ServicePrincipalSecretContainer.Flush();
                    _jobRunner.Enqueue(crd.Value);
                    break;
                }
            }

            if (stopwatch != null)
            {
                stopwatch.Stop();
                _logger.Debug("ReconcileManagedSecrets finished in {elasped} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        public Task OnBookmarked(IKubernetes client, AzureKeyVault crd)
        {
            _logger.Debug("OnBookmarked {@crd}", crd);
            return Task.CompletedTask;
        }

        public Task OnError(IKubernetes client, AzureKeyVault crd)
        {
            _logger.Debug("OnError {@crd}", crd);
            return Task.CompletedTask;
        }



        private Resource GetKey(AzureKeyVault crd) => new Resource(crd.Namespace(), crd.Name());
    }
}
