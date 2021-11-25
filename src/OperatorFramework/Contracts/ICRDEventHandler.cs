namespace OperatorFramework.Contracts
{
    internal interface ICRDEventHandler<T> : IEventHandler
        where T : CRDBase
    {
        Task OnAdded(IKubernetes client, T crd);

        Task OnDeleted(IKubernetes client, T crd);

        Task OnUpdated(IKubernetes client, T crd);

        Task OnBookmarked(IKubernetes client, T crd);

        Task OnError(IKubernetes client, T crd);
    }
}
