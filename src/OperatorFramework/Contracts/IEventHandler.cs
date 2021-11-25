namespace OperatorFramework.Contracts
{
    internal interface IEventHandler
    {
        Task OnReconciliation(IKubernetes client);
    }
}
