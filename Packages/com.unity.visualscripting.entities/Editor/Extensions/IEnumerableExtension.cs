using System.Collections.Generic;

namespace UnityEditor.VisualScripting.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class IEnumerableExtension
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
        {
            return new HashSet<T>(source, comparer);
        }
    }
}
