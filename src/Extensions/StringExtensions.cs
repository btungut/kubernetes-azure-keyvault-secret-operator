namespace Operator.Extensions
{
    internal static class StringExtensions
    {
        public static string GetDefault(this string value, string defaultValue)
        {
            if(string.IsNullOrWhiteSpace(value))
                return defaultValue;
            else
                return value;
        }
    }
}
