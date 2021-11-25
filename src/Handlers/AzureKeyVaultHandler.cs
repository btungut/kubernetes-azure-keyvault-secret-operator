namespace Operator.Handlers
{
    public class AzureKeyVaultHandler : ICRDEventHandler<AzureKeyVault>
    {
        private static ILogger _logger = LoggerFactory.GetLogger<AzureKeyVaultHandler>();

        private static Dictionary<Resource, AzureKeyVault> _bag = new Dictionary<Resource, AzureKeyVault>();
        private static AzureKeyVaultJob _job = new AzureKeyVaultJob();
        private static JobRuner<AzureKeyVault> _jobRunner = new JobRuner<AzureKeyVault>(_job);

        private static SemaphoreSlim _reconciliationSemaphore = new SemaphoreSlim(1, 1);


        public async Task OnAdded(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);

            await _reconciliationSemaphore.WaitAsync();
            try
            {
                _bag[GetKey(crd)] = crd;
                _jobRunner.Enqueue(crd);
            }
            catch (Exception e)
            {
                _logger.Error(e, "OnAdded is failed for {key}", key);
            }
            finally
            {
                _reconciliationSemaphore.Release();
            }
        }

        public async Task OnUpdated(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);
            bool isSyncVersionChanged = false;

            await _reconciliationSemaphore.WaitAsync();
            try
            {
                if (_bag.TryGetValue(key, out AzureKeyVault currentCrd) && currentCrd.Spec.SyncVersion != crd.Spec.SyncVersion)
                {
                    isSyncVersionChanged = true;
                }

                _bag[key] = crd;

                if (isSyncVersionChanged)
                {
                    //Flush caches and process it DIRETLY instead of to enqueue.
                    ServicePrincipalSecretContainer.Flush();
                    await _job.RunAsync(crd);
                }
                else
                {
                    _jobRunner.Enqueue(crd);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "OnUpdated is failed for {key} isSyncVersionChanged:{isSyncVersionChanged}", key, isSyncVersionChanged);
            }
            finally
            {
                _reconciliationSemaphore.Release();
            }
        }

        public async Task OnDeleted(IKubernetes client, AzureKeyVault crd)
        {
            var key = GetKey(crd);

            await _reconciliationSemaphore.WaitAsync();
            try
            {
                _bag.Remove(key);
                _logger.Debug("AzureKeyVault {resource} is removed from internal cache", key);

                await FinalizeSecretsAsync(client, crd);
            }
            catch (Exception e)
            {
                _logger.Error(e, "OnDeleted is failed for {key}", key);
            }
            finally
            {
                _reconciliationSemaphore.Release();
            }
        }

        public async Task OnReconciliation(IKubernetes client)
        {
            await _reconciliationSemaphore.WaitAsync();
            try
            {
                _logger.Information("OnReconciliation is starting");
                await ReconcileManagedSecrets(client);
                await ReconcileDanglingSecrets(client);
                _logger.Information("OnReconciliation is finished");
            }
            catch (Exception e)
            {
                _logger.Error(e, "OnReconciliation is failed");
            }
            finally
            {
                _reconciliationSemaphore.Release();
            }
        }

        private async Task ReconcileDanglingSecrets(IKubernetes client)
        {
            var clusterSecrets = new List<Resource>();
            var namespaces = await client.ListNamespaceAsync();
            foreach (var v1Namespace in namespaces.Items)
            {
                var ns = v1Namespace.Name();
                var secrets = await client.ListNamespacedSecretAsync(ns, labelSelector: $"{Constants.SecretLabelKey}={Constants.SecretLabelValue}");
                clusterSecrets.AddRange(secrets.Items.Select(s => new Resource(s.Namespace(), s.Name())));
            }

            var secretsNeedsToBeValid = _bag.Values
                .SelectMany(b => b.Spec.Objects.SelectMany(o => o.CopyTo))
                .Select(s => new Resource(s.Namespace, s.SecretName))
                .ToArray();

            var danglingSecrets = clusterSecrets.Except(secretsNeedsToBeValid).ToArray();
            _logger.Information("Total {count} dangling secrets found {list}", danglingSecrets.Length, danglingSecrets);
            int succeededCount = 0;
            foreach (var secret in danglingSecrets)
            {
                var apiResult = await client.InvokeAsync(c => c.DeleteNamespacedSecretAsync(secret.Name, secret.Namespace));
                if(apiResult.IsSucceeded)
                {
                    succeededCount++;
                    _logger.Debug("Dangling secret {resource} is deleted", secret);
                }
                else
                {
                    _logger.Warning("Danling secret {resource} couldn't be deleted", secret);
                }
            }

            if(succeededCount > 0)
                _logger.Information("Danling secrets are deleted {succeeded}/{total}", succeededCount, danglingSecrets.Length);
        }

        private async Task FinalizeSecretsAsync(IKubernetes client, AzureKeyVault crd)
        {
            var secretsNeedsToBeFinalized = crd.Spec.Objects.SelectMany(o => o.CopyTo);
            _logger.Information("Secret finalization is starting for {@resource}", secretsNeedsToBeFinalized.Select(o => new Resource(o.Namespace, o.SecretName)));

            foreach (var secret in secretsNeedsToBeFinalized)
            {
                var secretResource = new Resource(secret.Namespace, secret.SecretName);
                var apiResult = await client.InvokeAsync(c => c.DeleteNamespacedSecretAsync(secretResource.Name, secretResource.Namespace));

                if (apiResult.IsSucceeded)
                {
                    _logger.Information("Secret is deleted successfully {resource}", secretResource);
                }
                else
                {
                    _logger.Warning(apiResult.Exception, "Secret couldn't be deleted {resource}", secretResource);
                }
            }
        }

        private async Task ReconcileManagedSecrets(IKubernetes client)
        {
            //TODO : dangling secret'ları bul ve sil
            var crds = _bag.ToArray();

            foreach (var crd in crds)
            {
                var secretsNeedsToBeValid = crd.Value.Spec.Objects.SelectMany(o => o.CopyTo);
                foreach (var secret in secretsNeedsToBeValid)
                {
                    var secretResource = new Resource(secret.Namespace, secret.SecretName);
                    var apiResult = await client.InvokeAsync(c => c.ReadNamespacedSecretAsync(secretResource.Name, secretResource.Namespace));

                    var secretSyncVersion = apiResult.Data.GetAnnotation(Constants.SecretSyncVersionAnnotation);
                    if (apiResult.IsSucceeded && secretSyncVersion != null && Convert.ToInt32(secretSyncVersion) == crd.Value.Spec.SyncVersion)
                    {
                        _logger.Information("OnReconciliation : {resource} syncVersion:{version} is exist and syncVersions are same, no need to take action.", secretResource, secretSyncVersion);
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
                    else if (apiResult.Status == System.Net.HttpStatusCode.NotFound && _bag.ContainsKey(crd.Key))
                    {
                        _logger.Warning(
                            "OnReconciliation : Secret {secretResource} for {crdResource} is not found, it will be processed.",
                            crd.Key, secretResource);
                    }
                    else
                    {
                        _logger.Error(
                            apiResult.Exception, "OnReconciliation : Unexpected case {secretResource} {crdResource}",
                            crd.Key, secretResource);
                    }

                    ServicePrincipalSecretContainer.Flush();
                    _jobRunner.Enqueue(crd.Value);
                    break;
                }
            }
            #region long version
            //foreach (var keyValuePair in crds)
            //{
            //    var crd = keyValuePair.Value;
            //    bool isCrdCompleted = false;

            //    foreach (var obj in crd.Spec.Objects)
            //    {
            //        if (isCrdCompleted)
            //            break;

            //        foreach (var copyTo in obj.CopyTo)
            //        {
            //            if (isCrdCompleted)
            //                break;

            //            var resource = new Resource(copyTo.Namespace, copyTo.SecretName);
            //            var apiResult = await client.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));

            //            if (apiResult.IsSucceeded)
            //            {
            //                var currentSyncVersion = Convert.ToInt32(apiResult.Data.GetAnnotation(Constants.SecretSyncVersionAnnotation));
            //                if (currentSyncVersion == crd.Spec.SyncVersion)
            //                {
            //                    _logger.Debug("OnReconciliation : {resource} is exist and syncVersion is same, skipping.", resource);
            //                }
            //                else
            //                {
            //                    _logger.Information("syncVersion is changed from {old} to {new}, all secrets will be renewed.", crd.Spec.SyncVersion, currentSyncVersion);
            //                    ServicePrincipalSecretContainer.Flush();
            //                    _jobRunner.Enqueue(crd);

            //                    //This CRD is enqueued for renewal, its other Objects/CopyTo elements does not need to be processed.
            //                    isCrdCompleted = true;
            //                    break;
            //                }
            //            }
            //            else if (apiResult.Status == System.Net.HttpStatusCode.NotFound)
            //            {
            //                //Be sure that the entity is not deleted after we start this loop.
            //                if (_bag.ContainsKey(keyValuePair.Key))
            //                {
            //                    _logger.Warning("OnReconciliation : {resource} is not found. It is being created...", resource);
            //                    _jobRunner.Enqueue(crd);

            //                    //This CRD is enqueued for renewal, its other Objects/CopyTo elements does not need to be processed.
            //                    isCrdCompleted = true;
            //                    break;
            //                }
            //            }
            //            else
            //            {
            //                _logger.Error(apiResult.Exception, "OnReconciliation : unexpected result for {resource} from kubernetes", resource);
            //            }
            //        }
            //    }
            //}
            #endregion
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
