namespace OperatorFramework
{
    public abstract class CRDBase : IMetadata<V1ObjectMeta>
    {
		protected CRDBase()
		{
            if (CRDConfiguration._configurations.TryGetValue(GetType(), out CRDConfiguration value))
            {
				Configuration = value;
            }
			else
            {
				throw new ArgumentNullException($"Configuration for {GetType().FullName} needs to be created before!");
            }
		}

        public CRDConfiguration Configuration { get; private set; }
        public string StatusAnnotationName { get => string.Format($"{Configuration.Group}/{Configuration.Singular}-status"); }

		public string Status => Metadata.Annotations.ContainsKey(StatusAnnotationName) ? Metadata.Annotations[StatusAnnotationName] : null;
		public string ApiVersion { get; set; }
		public string Kind { get; set; }
		public V1ObjectMeta Metadata { get; set; }
	}
}
