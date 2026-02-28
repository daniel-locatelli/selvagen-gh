using Rhino.Geometry;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Coordinate system conversion utilities.
    /// Rhino: Z-up, right-handed → Three.js: Y-up, right-handed.
    /// Transform: (X, Y, Z)_rhino → (X, Z, -Y)_three
    /// </summary>
    public static class CoordinateHelper
    {
        /// <summary>
        /// Convert a Rhino Z-up point to a Three.js Y-up double array [x, y, z].
        /// </summary>
        public static double[] ToYUp(Point3d pt)
        {
            return new[] { pt.X, pt.Z, -pt.Y };
        }

        /// <summary>
        /// Convert a Rhino Z-up vector to a Three.js Y-up double array [x, y, z].
        /// </summary>
        public static double[] ToYUp(Vector3d vec)
        {
            return new[] { vec.X, vec.Z, -vec.Y };
        }

        /// <summary>
        /// Write a Rhino Z-up point (single-precision) into a flat buffer as Y-up at the given offset.
        /// Avoids per-vertex array allocation in hot loops.
        /// </summary>
        public static void WriteYUp(Point3f pt, double[] buffer, int offset)
        {
            buffer[offset]     = pt.X;
            buffer[offset + 1] = pt.Z;
            buffer[offset + 2] = -pt.Y;
        }

        /// <summary>
        /// Write a Rhino Z-up vector (single-precision) into a flat buffer as Y-up at the given offset.
        /// Avoids per-vertex array allocation in hot loops.
        /// </summary>
        public static void WriteYUp(Vector3f vec, double[] buffer, int offset)
        {
            buffer[offset]     = vec.X;
            buffer[offset + 1] = vec.Z;
            buffer[offset + 2] = -vec.Y;
        }

        /// <summary>
        /// Write a Rhino Z-up point into a flat buffer as Y-up at the given offset.
        /// </summary>
        public static void WriteYUp(Point3d pt, double[] buffer, int offset)
        {
            buffer[offset]     = pt.X;
            buffer[offset + 1] = pt.Z;
            buffer[offset + 2] = -pt.Y;
        }
    }
}
