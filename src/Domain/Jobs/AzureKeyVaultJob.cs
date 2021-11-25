namespace Operator.Domain.Jobs
{
    internal class AzureKeyVaultJob : Job<AzureKeyVault>
    {
        private readonly ILogger _logger = LoggerFactory.GetLogger<AzureKeyVaultJob>();
        private readonly IKubernetes _client = Program.KubernetesClient;

        public override async Task RunAsync(AzureKeyVault crd)
        {
            var secretResource = new Resource(crd.Spec.ServicePrincipal.SecretNamespace, crd.Spec.ServicePrincipal.SecretName);
            _logger.Debug("Secret for service principal at {resource} is fetching...", secretResource);

            var spSecret = await ServicePrincipalSecretContainer.GetAsync(secretResource.Namespace, secretResource.Name);
            if (spSecret == null)
            {
                _logger.Error(
                    "AzureKeyVault {name} is failed. Secret for its service principal at {resource} is not found!",
                    crd.Name(), secretResource);
                return;
            }

            var kvClient = AzureKeyVaultClient.GetOrCreate(
                crd.Spec.Name,
                spSecret.StringData[crd.Spec.ServicePrincipal.TenantIdField],
                spSecret.StringData[crd.Spec.ServicePrincipal.ClientIdField],
                spSecret.StringData[crd.Spec.ServicePrincipal.ClientSecretField]);

            foreach (var item in crd.Spec.Objects)
            {
                _logger.Debug("Azure is calling to get secret {name}", item.Name);
                var kvSecret = await kvClient.GetSecretAsync(item.Name);
                
                if (!kvSecret.IsSucceeded)
                {
                    var list = item.CopyTo.Select(c => new Resource(c.Namespace, c.SecretName));
                    _logger.Warning("Azure response is not succeeded, secret creation is skipping for {@list}", list);
                    continue;
                }

                foreach (var copyTo in item.CopyTo)
                {
                    var resource = new Resource(copyTo.Namespace, copyTo.SecretName);
                    await SyncSecret(crd, resource, item.Name, kvSecret.Data);
                }
            }
        }

        private async Task SyncSecret(AzureKeyVault crd, Resource resource, string kvKey, string kvValue)
        {
            var secretGetResult = await _client.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));

            if (secretGetResult.IsSucceeded)
            {
                _logger.Debug("Secret is found {resource} and will be replaced", resource);

                V1Secret secret = secretGetResult.Data;
                FillV1Secret(secret, crd, new Dictionary<string, string> { { kvKey, kvValue } });

                var replaceResult = await _client.InvokeAsync(c => c.ReplaceNamespacedSecretAsync(secret, resource.Name, resource.Namespace));
                if (!replaceResult.IsSucceeded)
                {
                    string requestContent = replaceResult.Exception.GetRequestContentIfPossible();
                    string responseContent = replaceResult.Exception.GetResponseContentIfPossible();

                    _logger.Error(replaceResult.Exception, "Secret couldn't be replaced {resource} {requestContent} {responseContent}", resource, requestContent, responseContent);
                }
                else
                {
                    _logger.Information("Secret is replaced {resource} syncVersion:{syncVersion}", resource, crd.Spec.SyncVersion);
                }
            }
            else if (secretGetResult.Status == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Debug("Secret is not found {resource} and will be created", resource);

                V1Secret secret = new V1Secret
                {
                    Metadata = new V1ObjectMeta(name: resource.Name, namespaceProperty: resource.Namespace)
                };
                FillV1Secret(secret, crd, new Dictionary<string, string> { { kvKey, kvValue } });

                var createResult = await _client.InvokeAsync(c => c.CreateNamespacedSecretAsync(secret, resource.Namespace));
                if (!createResult.IsSucceeded)
                {
                    string requestContent = createResult.Exception.GetRequestContentIfPossible();
                    string responseContent = createResult.Exception.GetResponseContentIfPossible();

                    _logger.Error(createResult.Exception, "Secret couldn't be replaced {resource} {requestContent} {responseContent}", resource, requestContent, responseContent);
                }
                else
                {
                    _logger.Information("Secret is created {resource} syncVersion:{syncVersion}", resource, crd.Spec.SyncVersion);
                }
            }
            else
            {
                _logger.Error("get secret is failed, unexpected api result {status}", secretGetResult.Status);
            }
        }

        private void FillV1Secret(V1Secret secret, AzureKeyVault crd, IDictionary<string, string> stringData, string secretType = "Opaque")
        {
            secret.Data = stringData.ToDictionary(k => k.Key, v => Encoding.UTF8.GetBytes(v.Value));
            secret.Type = secretType;
            secret.SetLabel(Constants.SecretLabelKey, Constants.SecretLabelValue);
            secret.SetLabel("ownerId", crd.Name());
            secret.SetAnnotation(Constants.SecretUpdatedAnnotation, DateTime.UtcNow.ToString());
            secret.SetAnnotation(Constants.SecretSyncVersionAnnotation, crd.Spec.SyncVersion.ToString());
        }
    }
}
