using System.Linq;
using System.Collections.Generic;

namespace AWS.Logger.Core
{
    /// <summary>
    /// Helper methods for the generic IEnumerables.
    /// </summary>
    public static class IEnumerableExtensions
    {
        public static IEnumerable<List<T>> ChunkByIndex<T>(this IEnumerable<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList());
        }
    }
}
