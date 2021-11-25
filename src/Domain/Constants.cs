namespace Operator.Domain
{
    internal class Constants
    {
        public const string SecretUpdatedAnnotation = $"{CrdName}/updated";
        public const string SecretSyncVersionAnnotation = $"{CrdName}/syncVersion";
        public const string SecretLabelKey = "owner";
        public const string SecretLabelValue = CrdName;

        public const string CrdApiVersion = "v1";
        public const string CrdApiGroup = "btungut.io";
        public const string CrdSingular = $"azurekeyvault";
        public const string CrdPlural = $"azurekeyvaults";
        public const string CrdName = $"{CrdPlural}.{CrdApiGroup}";
    }
}
