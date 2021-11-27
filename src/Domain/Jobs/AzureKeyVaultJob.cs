using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Operator.Domain.Jobs
{
    internal class AzureKeyVaultJob : Job<AzureKeyVault>
    {
        private readonly ILogger _logger = LoggerFactory.GetLogger<AzureKeyVaultJob>();
        private readonly IKubernetes _client = Program.KubernetesClient;

        public override async Task RunAsync(AzureKeyVault ___crd)
        {
            Stopwatch stopwatch = (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug)) ? Stopwatch.StartNew() : null;

            var crdKey = new Resource(___crd.Namespace(), ___crd.Name());
            bool isInvoked = false;

            await AzureKeyVaultHandler.Context.ExclusiveAsync(crdKey, async (crd) =>
            {
                isInvoked = true;
                var secretResource = new Resource(crd.Spec.ServicePrincipalRef.SecretNamespace, crd.Spec.ServicePrincipalRef.SecretName);
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
                    crd.Spec.AzureKeyVaultRef.Name,
                    spSecret.StringData[crd.Spec.ServicePrincipalRef.TenantIdField],
                    spSecret.StringData[crd.Spec.ServicePrincipalRef.ClientIdField],
                    spSecret.StringData[crd.Spec.ServicePrincipalRef.ClientSecretField]);

                foreach (var managedSecretDefinition in crd.Spec.ManagedSecrets)
                {
                    try
                    {
                        _logger.Debug("Secret {resource} is starting to be processed", managedSecretDefinition.Name);
                        await ProcessManagedSecret(crd, managedSecretDefinition, kvClient);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Secret {resource} couldn't be processed for {crd}, skipping...", managedSecretDefinition.Name, crd.Metadata.Name);
                    }
                }
            });

            if (!isInvoked)
            {
                _logger.Warning("AzureKeyVault {resource} couldn't be processed. Race-condition has been happened.", crdKey);
            }

            if (stopwatch != null)
            {
                stopwatch.Stop();
                _logger.Debug("RunAsync finished in {elasped} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task ProcessManagedSecret(AzureKeyVault owner, AzureKeyVault.AzureKeyVaultSpec.ManagedSecretsDefinition managedSecretDefinition, AzureKeyVaultClient kvClient)
        {
            Dictionary<string, byte[]> data = await PopulateDataDictionaryAsync(managedSecretDefinition.Data, kvClient);
            if (data == null || data.Count == 0)
            {
                _logger.Error("Secret {resource} is skipping. Its data couldn't be populated. Secret is skipping...", managedSecretDefinition.Name);
                return;
            }

            var clusterNamespaces = (await _client.ListNamespaceAsync()).Items.Select(x => x.Metadata.Name).ToArray();
            var managedSecrets = PatternResolver.ResolveManagedSecrets(clusterNamespaces, managedSecretDefinition);
            _logger.Debug("Total {count} secret will be created for {owner} namespace/name pairings : {pairs}", managedSecrets.Count(), owner.Metadata.Name, managedSecrets);

            foreach (var resource in managedSecrets)
            {
                try
                {
                    _logger.Debug("Secret {resource} is being synced", resource);
                    await CreateOrUpdateSecretAsync(owner, resource, managedSecretDefinition.Type, data, managedSecretDefinition.Labels);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Secret {resource} couldn't be synced. Unexpected error, skipping...", resource);
                }
            }
        }

        private async Task<Dictionary<string, byte[]>> PopulateDataDictionaryAsync(IDictionary<string, string> requestedData, AzureKeyVaultClient kvClient)
        {
            const string pattern = @"{{\s*\.([a-zA-Z0-9._-]*)\s*}}";

            var data = new Dictionary<string, byte[]>();
            foreach (var req in requestedData)
            {
                string value;

                var matches = Regex.Matches(req.Value, pattern);
                if (matches.Count == 0)
                {
                    _logger.Debug("{key} is hardcoded", req.Key);
                    value = req.Value;
                }
                else
                {
                    StringBuilder builder = new StringBuilder(req.Value);
                    foreach (Match match in matches)
                    {
                        var secretName = match.Groups[1].Value;
                        _logger.Debug("Azure KeyVault will be called for {secret}", secretName);

                        var secretValue = await kvClient.GetSecretAsync(secretName);

                        //If any value couldn't be fetched from Azure, skip this secret directly, return null!
                        if (!secretValue.IsSucceeded)
                        {
                            _logger.Warning(secretValue.Exception, "Azure KeyVault request is not succeeded for {secret}", secretName);
                            return null;
                        }

                        builder.Replace(match.Groups[0].Value, secretValue.Data);
                    }

                    value = builder.ToString();
                }

                data.Add(req.Key, Encoding.UTF8.GetBytes(value));
            }

            return data;
        }

        private async Task CreateOrUpdateSecretAsync(AzureKeyVault owner, Resource resource, string secretType, Dictionary<string, byte[]> data, IDictionary<string, string> labels)
        {
            var secretGetResult = await _client.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));

            if (secretGetResult.IsSucceeded)
            {
                _logger.Debug("Secret {resource} is exist and will be updated", resource);

                V1Secret secret = secretGetResult.Data;
                FillV1Secret(owner, secret, secretType, data, labels);

                var replaceResult = await _client.InvokeAsync(c => c.ReplaceNamespacedSecretAsync(secret, resource.Name, resource.Namespace));
                if (!replaceResult.IsSucceeded)
                {
                    string requestContent = replaceResult.Exception.GetRequestContentIfPossible();
                    string responseContent = replaceResult.Exception.GetResponseContentIfPossible();

                    _logger.Error(replaceResult.Exception, "Secret {resource} couldn't be updated {requestContent} {responseContent}", resource, requestContent, responseContent);
                }
                else
                {
                    _logger.Information("Secret {resource} is updated syncVersion:{syncVersion}", resource, owner.Spec.SyncVersion);
                }
            }
            else if (secretGetResult.Status == System.Net.HttpStatusCode.NotFound)
            {
                _logger.Debug("Secret {resource} is not exist and will be created", resource);

                V1Secret secret = new V1Secret
                {
                    Metadata = new V1ObjectMeta(name: resource.Name, namespaceProperty: resource.Namespace)
                };
                FillV1Secret(owner, secret, secretType, data, labels);

                var createResult = await _client.InvokeAsync(c => c.CreateNamespacedSecretAsync(secret, resource.Namespace));
                if (!createResult.IsSucceeded)
                {
                    string requestContent = createResult.Exception.GetRequestContentIfPossible();
                    string responseContent = createResult.Exception.GetResponseContentIfPossible();

                    _logger.Error(createResult.Exception, "Secret {resource} couldn't be created {requestContent} {responseContent}", resource, requestContent, responseContent);
                }
                else
                {
                    _logger.Information("Secret {resource} is created syncVersion:{syncVersion}", resource, owner.Spec.SyncVersion);
                }
            }
            else
            {
                _logger.Error(secretGetResult.Exception, "GetSecret failed with {status} on kubernetes. Skipping...", secretGetResult.Status);
            }
        }


        private void FillV1Secret(AzureKeyVault owner, V1Secret secret, string secretType, Dictionary<string, byte[]> data, IDictionary<string, string> labels)
        {
            secret.Data = data;
            secret.Type = secretType;
            secret.Metadata.Labels = labels;
            secret.SetLabel(Constants.SecretLabelKey, Constants.SecretLabelValue);
            secret.SetLabel("ownerId", owner.Name());
            secret.SetAnnotation(Constants.SecretUpdatedAnnotation, DateTime.UtcNow.ToString());
            secret.SetAnnotation(Constants.SecretSyncVersionAnnotation, owner.Spec.SyncVersion.ToString());
        }

        //private async Task SyncSecret(AzureKeyVault crd, Resource resource, SecretTypes secretType, string kvKey, string kvValue)
        //{
        //    var secretGetResult = await _client.InvokeAsync(c => c.ReadNamespacedSecretAsync(resource.Name, resource.Namespace));

        //    if (secretGetResult.IsSucceeded)
        //    {
        //        _logger.Debug("Secret is found {resource} and will be replaced", resource);

        //        V1Secret secret = secretGetResult.Data;
        //        FillV1Secret(secret, crd, kvKey, kvValue, secretType);

        //        var replaceResult = await _client.InvokeAsync(c => c.ReplaceNamespacedSecretAsync(secret, resource.Name, resource.Namespace));
        //        if (!replaceResult.IsSucceeded)
        //        {
        //            string requestContent = replaceResult.Exception.GetRequestContentIfPossible();
        //            string responseContent = replaceResult.Exception.GetResponseContentIfPossible();

        //            _logger.Error(replaceResult.Exception, "Secret couldn't be replaced {resource} {requestContent} {responseContent}", resource, requestContent, responseContent);
        //        }
        //        else
        //        {
        //            _logger.Information("Secret is replaced {resource} syncVersion:{syncVersion}", resource, crd.Spec.SyncVersion);
        //        }
        //    }
        //    else if (secretGetResult.Status == System.Net.HttpStatusCode.NotFound)
        //    {
        //        _logger.Debug("Secret is not found {resource} and will be created", resource);

        //        V1Secret secret = new V1Secret
        //        {
        //            Metadata = new V1ObjectMeta(name: resource.Name, namespaceProperty: resource.Namespace)
        //        };
        //        FillV1Secret(secret, crd, kvKey, kvValue, secretType);

        //        var createResult = await _client.InvokeAsync(c => c.CreateNamespacedSecretAsync(secret, resource.Namespace));
        //        if (!createResult.IsSucceeded)
        //        {
        //            string requestContent = createResult.Exception.GetRequestContentIfPossible();
        //            string responseContent = createResult.Exception.GetResponseContentIfPossible();

        //            _logger.Error(createResult.Exception, "Secret couldn't be replaced {resource} {requestContent} {responseContent}", resource, requestContent, responseContent);
        //        }
        //        else
        //        {
        //            _logger.Information("Secret is created {resource} syncVersion:{syncVersion}", resource, crd.Spec.SyncVersion);
        //        }
        //    }
        //    else
        //    {
        //        _logger.Error("get secret is failed, unexpected api result {status}", secretGetResult.Status);
        //    }
        //}


        //private void FillV1Secret(V1Secret secret, AzureKeyVault crd, string key, string value, SecretTypes secretType)
        //{
        //    secret.SetLabel(Constants.SecretLabelKey, Constants.SecretLabelValue);
        //    secret.SetLabel("ownerId", crd.Name());
        //    secret.SetAnnotation(Constants.SecretUpdatedAnnotation, DateTime.UtcNow.ToString());
        //    secret.SetAnnotation(Constants.SecretSyncVersionAnnotation, crd.Spec.SyncVersion.ToString());

        //    if (secretType == SecretTypes.Opaque)
        //    {
        //        secret.Data = CreateSecretDictionary(key, value);
        //        secret.Type = "Opaque";
        //    }
        //    else if (secretType == SecretTypes.DockerConfigJson)
        //    {
        //        secret.Data = CreateSecretDictionary(".dockerconfigjson", value);
        //        secret.Type = "kubernetes.io/dockerconfigjson";
        //    }
        //    else
        //    {
        //        throw new NotImplementedException($"{secretType} is not supported yet.");
        //    }
        //}

        //private IDictionary<string, byte[]> CreateSecretDictionary(string key, string value)
        //{
        //    return new Dictionary<string, byte[]>
        //    {
        //        { key, Encoding.UTF8.GetBytes(value) }
        //    };
        //}

        //static Dictionary<string, SecretTypes> _secretTypeMatchings = new Dictionary<string, SecretTypes>(StringComparer.OrdinalIgnoreCase)
        //{
        //    { "opaque", SecretTypes.Opaque },
        //    { "kubernetes.io/service-account-token", SecretTypes.ServiceAccountToken },
        //    { "kubernetes.io/dockercfg", SecretTypes.DockerCfg },
        //    { "kubernetes.io/dockerconfigjson", SecretTypes.DockerConfigJson },
        //    { "kubernetes.io/basic-auth", SecretTypes.BasicAuth },
        //    { "kubernetes.io/ssh-auth", SecretTypes.SshAuth },
        //    { "kubernetes.io/tls", SecretTypes.TLS },
        //    { "bootstrap.kubernetes.io/token", SecretTypes.Token },
        //};
        //private SecretTypes? GetSecretType(string type)
        //{
        //    if (_secretTypeMatchings.TryGetValue(type, out var result))
        //        return result;

        //    return null;
        //}

        //enum SecretTypes
        //{
        //    Opaque,
        //    DockerConfigJson,
        //    DockerCfg,
        //    ServiceAccountToken,
        //    BasicAuth,
        //    SshAuth,
        //    TLS,
        //    Token
        //}
    }
}
