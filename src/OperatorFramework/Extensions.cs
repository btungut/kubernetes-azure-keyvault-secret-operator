using System.Net;

namespace OperatorFramework
{
    internal static class Extensions
    {
        public static async Task<ApiResult<TReturn>> InvokeAsync<TReturn>(this IKubernetes client, Func<IKubernetes, Task<TReturn>> func)
        {
            try
            {
                var clientResult = await func(client).ConfigureAwait(false);
                return new ApiResult<TReturn>(clientResult);
            }
            catch (HttpOperationException hoex)
            {
                return new ApiResult<TReturn>(hoex, hoex.Response.StatusCode);
            }
            catch (Exception e)
            {
                return new ApiResult<TReturn>(e, HttpStatusCode.InternalServerError);
            }
        }
    }
}
