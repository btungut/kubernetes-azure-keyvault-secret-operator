using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Operator.CRDs
{
    public class AzureKeyVault : CRDBase
    {
        public AzureKeyVaultSpec Spec { get; set; }

        public class AzureKeyVaultSpec
        {
            public string Name { get; set; }
            public string ResourceGroup { get; set; }
            public int SyncVersion { get; set; }

            public ServicePrincipalConfiguration ServicePrincipal { get; set; }

            public List<ToBeFetchedObjects> Objects { get; set; }

            public class ServicePrincipalConfiguration
            {
                public string SecretName { get; set; }
                public string SecretNamespace { get; set; }
                public string ClientIdField { get; set; }
                public string ClientSecretField { get; set; }
                public string TenantIdField { get; set; }
            }

            public class ToBeFetchedObjects
            {
                public string Name { get; set; }
                public ObjectType Type { get; set; }

                public List<CopyToNamespace> CopyTo { get; set; }


                public enum ObjectType
                {
                    Key,
                    Secret,
                    Certificate
                }

                public class CopyToNamespace
                {
                    public string Namespace { get; set; }
                    public string SecretName { get; set; }

                    //optional, default : Opaque
                    public string Type { get; set; } = "opaque";
                }
            }
        }
    }
}
