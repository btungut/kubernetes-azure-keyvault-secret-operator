using System.Net;

namespace OperatorFramework
{
    public class ApiResult<T>
    {
        public ApiResult(T data)
        {
            Data = data;
            IsSucceeded = true;
            Exception = null;
            Status = HttpStatusCode.OK;
        }

        public ApiResult(Exception exception, HttpStatusCode status)
        {
            Data = default;
            IsSucceeded = false;
            Exception = exception;
            Status = status;
        }

        public T Data { get; private set; }
        public bool IsSucceeded { get; private set; }
        public Exception Exception { get; private set; }
        public HttpStatusCode Status { get; private set; }
    }
}
