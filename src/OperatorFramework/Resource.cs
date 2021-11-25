namespace OperatorFramework
{
    public record Resource
    {
        public Resource(string @namespace, string name)
        {
            Name = name.ToLower();
            Namespace = string.IsNullOrWhiteSpace(@namespace) ? "default" : @namespace.ToLower();
        }

        public string Name { get; private set; }
        public string Namespace { get; private set; }

        public override string ToString() => $"{Namespace}/{Name}";
    }
}
