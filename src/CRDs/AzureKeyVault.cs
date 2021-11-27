namespace Operator.CRDs
{
    public class AzureKeyVault : CRDBase
    {
        public AzureKeyVaultSpec Spec { get; set; }

        public class AzureKeyVaultSpec
        {
            public int SyncVersion { get; set; }
            public AzureKeyVaultRefDefinition AzureKeyVaultRef { get; set; }

            public ServicePrincipalRefDefinition ServicePrincipalRef { get; set; }

            public List<ManagedSecretsDefinition> ManagedSecrets { get; set; }

            public class ServicePrincipalRefDefinition
            {
                public string SecretName { get; set; }
                public string SecretNamespace { get; set; }
                public string ClientIdField { get; set; }
                public string ClientSecretField { get; set; }
                public string TenantIdField { get; set; }
            }

            public class ManagedSecretsDefinition
            {
                public string Name { get; set; }
                public string[] Namespaces { get; set; }
                public string Type { get; set; }
                public IDictionary<string, string> Data { get; set; }
                public IDictionary<string, string> Labels { get; set; }
            }

            public class AzureKeyVaultRefDefinition
            {
                public string Name { get; set; }
                public string ResourceGroup { get; set; }
            }
        }
    }
}
