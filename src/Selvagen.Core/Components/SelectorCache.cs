using System;
using System.Collections.Generic;
using System.Linq;

namespace Selvagen.Core.Components
{
    /// <summary>
    /// Decides whether a selectable component must re-fetch its item list.
    /// Pure logic, lifted out of the Grasshopper component so it can be unit-tested.
    /// </summary>
    public static class CacheDecision
    {
        public static bool NeedsFetch(
            bool hasCachedItems,
            object[] cachedKey,
            object[] currentKey,
            bool forceRefresh)
        {
            if (!hasCachedItems) return true;
            if (cachedKey == null) return true;
            if (!KeysEqual(cachedKey, currentKey)) return true;
            if (forceRefresh) return true;
            return false;
        }

        private static bool KeysEqual(object[] a, object[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!Equals(a[i], b[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Reconciles a persisted selection ID against the current item list.
    /// Returns the persisted ID if it still exists, otherwise null.
    /// Never auto-picks a different item — silent re-selection would surprise users.
    /// </summary>
    public static class Reconcile
    {
        public static string SelectId<T>(IEnumerable<T> items, string persistedId, Func<T, string> getId)
        {
            if (string.IsNullOrEmpty(persistedId)) return null;
            if (items == null) return null;
            return items.Any(x => getId(x) == persistedId) ? persistedId : null;
        }
    }
}
