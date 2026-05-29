using System;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Validates the shape of a <see cref="BufferGeometry"/> received from the server
    /// before it is decoded into a Rhino mesh. Rhino-free so it is unit-testable
    /// headless. Throws a descriptive <see cref="ArgumentException"/> instead of
    /// letting a malformed payload surface as a NullReferenceException downstream.
    /// </summary>
    public static class BufferGeometryValidator
    {
        public static void ValidateForDecode(BufferGeometry bg)
        {
            if (bg == null) throw new ArgumentNullException(nameof(bg));
            if (bg.Data == null)
                throw new ArgumentException("BufferGeometry.data is missing.", nameof(bg));
            if (bg.Data.Attributes == null)
                throw new ArgumentException("BufferGeometry.data.attributes is missing.", nameof(bg));
            var pos = bg.Data.Attributes.Position;
            if (pos?.Array == null)
                throw new ArgumentException("BufferGeometry position attribute is missing.", nameof(bg));
            if (pos.Array.Length % 3 != 0)
                throw new ArgumentException(
                    $"BufferGeometry position array length ({pos.Array.Length}) is not a multiple of 3.", nameof(bg));
        }
    }
}
