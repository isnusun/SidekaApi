using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Helpers
{
    public static class QueryStringHelper
    {
        public static TResult GetQueryString<TResult>(IQueryCollection queries, String key, TResult defaultValue = default(TResult))
        {
            var results = GetQueryStrings<TResult>(queries, key).ToList();
            if (results.Count == 0)
                return defaultValue;
            return results[0];
        }

        public static IEnumerable<TResult> GetQueryStrings<TResult>(IQueryCollection queries, String key)
        {
            if (queries.ContainsKey(key))
            {
                foreach (var match in queries[key])
                {
                    var type = typeof(TResult);
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                        type = type.GenericTypeArguments[0];

                    if (type.IsEnum)
                    {
                        yield return (TResult)Enum.ToObject(type, Convert.ToUInt64(match));
                    }

                    yield return (TResult)Convert.ChangeType(match, type);
                }
            }
        }
    }
}
