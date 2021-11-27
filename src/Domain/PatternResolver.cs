using System.Text.RegularExpressions;

namespace Operator.Domain
{
    internal class PatternResolver
    {
        private static ILogger _logger = LoggerFactory.GetLogger<PatternResolver>();

        public static IReadOnlyList<string> ResolveNamespaces(IEnumerable<string> namespaces, string[] namespacePatterns)
        {
            var result = new List<string>();

            foreach (var pattern in namespacePatterns)
            {
                foreach (var ns in namespaces)
                {
                    try
                    {
                        if (Regex.IsMatch(ns, pattern))
                        {
                            result.Add(ns);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Pattern {pattern} is failed for {ns}. Iteration is skipped.", pattern, ns);
                    }
                }
            }

            return result;
        }

        public static IEnumerable<Resource> ResolveManagedSecrets(string[] clusterNamespaces, AzureKeyVault.AzureKeyVaultSpec.ManagedSecretsDefinition managedSecretsDefinition)
        {
            return PatternResolver
                .ResolveNamespaces(clusterNamespaces, managedSecretsDefinition.Namespaces)
                .Select(m => new Resource(m, managedSecretsDefinition.Name));

        }

        public static IEnumerable<Resource> ResolveManagedSecrets(string[] clusterNamespaces, IEnumerable<AzureKeyVault.AzureKeyVaultSpec.ManagedSecretsDefinition> managedSecretsDefinitionList)
        {
            return managedSecretsDefinitionList
                .Select(m => ResolveManagedSecrets(clusterNamespaces, m))
                .SelectMany(x => x);
        }
    }
}
