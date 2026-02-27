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
    }
}
