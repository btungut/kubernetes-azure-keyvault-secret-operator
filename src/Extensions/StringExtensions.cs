using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
