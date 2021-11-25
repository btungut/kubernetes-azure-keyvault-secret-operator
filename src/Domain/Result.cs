namespace Operator.Domain
{
    internal class Result<T>
    {
        public Result(T data)
        {
            Data = data;
            IsSucceeded = true;
            Exception = null;
        }

        public Result(Exception exception)
        {
            Data = default;
            IsSucceeded = false;
            Exception = exception;
        }

        public T Data { get; private set; }
        public bool IsSucceeded { get; private set; }
        public Exception Exception { get; private set; }
    }
}
