namespace Operator.Extensions
{
    internal static class ExceptionExtensions
    {
        public static string GetResponseContentIfPossible(this Exception exception)
        {
            if (exception != null && exception is HttpOperationException)
            {
                var hoex = (HttpOperationException)exception;
                if (hoex.Response == null || string.IsNullOrEmpty(hoex.Response.Content))
                    return string.Empty;

                return hoex.Response.Content;
            }

            return string.Empty;
        }

        public static string GetRequestContentIfPossible(this Exception exception)
        {
            if (exception != null && exception is HttpOperationException)
            {
                var hoex = (HttpOperationException)exception;
                if (hoex.Request == null || string.IsNullOrEmpty(hoex.Request.Content))
                    return string.Empty;

                return hoex.Request.Content;
            }

            return string.Empty;
        }
    }
}
