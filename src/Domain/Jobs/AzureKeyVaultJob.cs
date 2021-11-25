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
                    
                    var secretType = GetSecretType(copyTo.SecretType);
                    if(!secretType.HasValue)
                    {
                        _logger.Warning("Type field {field} for secret {resource} couldn't be resolved! This secret is skipping...", copyTo.SecretType, resource);
                        continue;
                    }

                    try
                    {
                        await SyncSecret(crd, resource, secretType.Value, item.Name, kvSecret.Data);
                    }
                    catch (NotImplementedException e)
                    {
                        _logger.Error(e, "Secret couldn't be synced {resource}. It uses not supported features, please check new versions", resource);
                    }
                    catch(Exception e)
                    {
                        _logger.Error(e, "Secret couldn't be synced {resource}. Unexpected error, skipping...", resource);
                    }
                }
            }
        }

        private async Task SyncSecret(AzureKeyVault crd, Resource resource, SecretTypes secretType, string kvKey, string kvValue)
        {
            var secretGetResult = await _client.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));

            if (secretGetResult.IsSucceeded)
            {
                _logger.Debug("Secret is found {resource} and will be replaced", resource);

                V1Secret secret = secretGetResult.Data;
                FillV1Secret(secret, crd, kvKey, kvValue, secretType);

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
                FillV1Secret(secret, crd, kvKey, kvValue, secretType);

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

        private void FillV1Secret(V1Secret secret, AzureKeyVault crd, string key, string value, SecretTypes secretType)
        {
            secret.SetLabel(Constants.SecretLabelKey, Constants.SecretLabelValue);
            secret.SetLabel("ownerId", crd.Name());
            secret.SetAnnotation(Constants.SecretUpdatedAnnotation, DateTime.UtcNow.ToString());
            secret.SetAnnotation(Constants.SecretSyncVersionAnnotation, crd.Spec.SyncVersion.ToString());

            if(secretType == SecretTypes.Opaque)
            {
                secret.Data = CreateSecretDictionary(key, value);
                secret.Type = "Opaque";
            }
            else if(secretType == SecretTypes.DockerConfigJson)
            {
                secret.Data = CreateSecretDictionary(".dockerconfigjson", value);
                secret.Type = "kubernetes.io/dockerconfigjson";
            }
            else
            {
                throw new NotImplementedException($"{secretType} is not supported yet.");
            }
        }

        private IDictionary<string, byte[]> CreateSecretDictionary(string key, string value)
        {
            return new Dictionary<string, byte[]> 
            {
                { key, Encoding.UTF8.GetBytes(value) } 
            };
        }

        static Dictionary<string, SecretTypes> _secretTypeMatchings = new Dictionary<string, SecretTypes>(StringComparer.OrdinalIgnoreCase)
        {
            { "opaque", SecretTypes.Opaque },
            { "kubernetes.io/service-account-token", SecretTypes.ServiceAccountToken },
            { "kubernetes.io/dockercfg", SecretTypes.DockerCfg },
            { "kubernetes.io/dockerconfigjson", SecretTypes.DockerConfigJson },
            { "kubernetes.io/basic-auth", SecretTypes.BasicAuth },
            { "kubernetes.io/ssh-auth", SecretTypes.SshAuth },
            { "kubernetes.io/tls", SecretTypes.TLS },
            { "bootstrap.kubernetes.io/token", SecretTypes.Token },
        };
        private SecretTypes? GetSecretType(string type)
        {
            if (_secretTypeMatchings.TryGetValue(type, out var result))
                return result;

            return null;
        }

        enum SecretTypes
        {
            Opaque,
            DockerConfigJson,
            DockerCfg,
            ServiceAccountToken,
            BasicAuth,
            SshAuth,
            TLS,
            Token
        }
    }
}
