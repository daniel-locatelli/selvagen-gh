using System;

namespace Selvagen.Core.Api
{
    /// <summary>
    /// Helpers for building PostgREST query clauses with URL-encoded values.
    /// Centralizes escaping so callers can't forget it (see DeleteCustomPropertiesAsync,
    /// which already escaped; this brings every other query builder in line).
    /// </summary>
    public static class Postgrest
    {
        /// <summary>Builds <c>column=eq.{encoded value}</c>.</summary>
        public static string Eq(string column, string value)
            => $"{column}=eq.{Uri.EscapeDataString(value ?? string.Empty)}";

        /// <summary>Builds <c>column=in.(v1,v2,...)</c> with each value encoded.</summary>
        public static string InList(string column, string[] values)
        {
            var encoded = string.Join(",", Array.ConvertAll(values ?? Array.Empty<string>(), Uri.EscapeDataString));
            return $"{column}=in.({encoded})";
        }
    }
}
