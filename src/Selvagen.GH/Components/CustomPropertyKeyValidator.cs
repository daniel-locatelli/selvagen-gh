using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Pure helper for snake_case key validation + suggestion.
    /// Used by SelvagenUploadCustomPropertyComponent to fail-fast on bad keys
    /// before any network call.
    /// </summary>
    public static class CustomPropertyKeyValidator
    {
        // Regex must mirror the DB CHECK constraint exactly.
        private static readonly Regex SnakeCaseRegex =
            new Regex(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        public const int MaxKeyLength = 200;

        public static bool IsValid(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key.Length > MaxKeyLength) return false;
            return SnakeCaseRegex.IsMatch(key);
        }

        /// <summary>
        /// Build a snake_case suggestion for an invalid input.
        /// Pure transformation, no I/O. Result is always a valid snake_case key
        /// (or "prop_" if nothing salvageable).
        /// </summary>
        public static string Suggest(string raw)
        {
            if (raw == null) return "prop_";
            var s = raw.Trim().ToLowerInvariant();

            // 1. Replace any char not in [a-z0-9_] with '_'
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            // 2. Collapse runs of '_'
            var collapsed = Regex.Replace(sb.ToString(), "_+", "_");
            // 3. Strip leading '_'
            collapsed = collapsed.TrimStart('_');
            // 4. Prepend "prop_" if empty or starts with digit
            if (collapsed.Length == 0 || (collapsed[0] >= '0' && collapsed[0] <= '9'))
                collapsed = "prop_" + collapsed;
            // 5. Truncate
            if (collapsed.Length > MaxKeyLength)
                collapsed = collapsed.Substring(0, MaxKeyLength);
            return collapsed;
        }

        /// <summary>
        /// Produce suggestions for a list of invalid keys, appending _2, _3, ...
        /// to subsequent identical suggestions so error reports stay distinguishable.
        /// </summary>
        public static List<string> SuggestBatch(IList<string> rawKeys)
        {
            var seen = new Dictionary<string, int>();
            var result = new List<string>(rawKeys.Count);
            foreach (var raw in rawKeys)
            {
                var s = Suggest(raw);
                if (seen.TryGetValue(s, out var n))
                {
                    seen[s] = n + 1;
                    result.Add(s + "_" + (n + 1));
                }
                else
                {
                    seen[s] = 1;
                    result.Add(s);
                }
            }
            return result;
        }
    }
}
