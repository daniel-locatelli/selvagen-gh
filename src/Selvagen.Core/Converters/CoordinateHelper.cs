using System;
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

        /// <summary>
        /// Convert a Rhino Z-up plane's orientation to a Three.js Y-up Euler XYZ rotation in radians.
        /// The plane's basis vectors (XAxis, YAxis, ZAxis) are converted axis-by-axis to Y-up and
        /// stacked as the columns of a rotation matrix, then decomposed using the Three.js default
        /// 'XYZ' order (R = Rx · Ry · Rz).
        /// </summary>
        public static double[] PlaneToYUpEuler(Plane plane)
        {
            var cx = ToYUp(plane.XAxis);
            var cy = ToYUp(plane.YAxis);
            var cz = ToYUp(plane.ZAxis);

            // Row-major 3x3: m[row,col], with cx/cy/cz forming the three columns.
            double m11 = cx[0], m12 = cy[0], m13 = cz[0];
            double m21 = cx[1], m22 = cy[1], m23 = cz[1];
            double m31 = cx[2], m32 = cy[2], m33 = cz[2];

            double clamped = m13 < -1.0 ? -1.0 : m13 > 1.0 ? 1.0 : m13;
            double y = Math.Asin(clamped);
            double x, z;
            if (Math.Abs(m13) < 0.9999999)
            {
                x = Math.Atan2(-m23, m33);
                z = Math.Atan2(-m12, m11);
            }
            else
            {
                x = Math.Atan2(m32, m22);
                z = 0.0;
            }

            return new[] { x, y, z };
        }
    }
}
