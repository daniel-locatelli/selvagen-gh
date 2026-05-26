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

        /// <summary>
        /// Convert a Three.js Y-up coordinate to a Rhino Z-up Point3d.
        /// Inverse of ToYUp: (x, y, z)_three → (x, -z, y)_rhino
        /// </summary>
        public static Point3d FromYUp(double x, double y, double z)
        {
            return new Point3d(x, -z, y);
        }

        /// <summary>
        /// Read a Y-up point from a flat buffer at the given offset and return a Z-up Point3d.
        /// </summary>
        public static Point3d FromYUp(double[] buffer, int offset)
        {
            return new Point3d(buffer[offset], -buffer[offset + 2], buffer[offset + 1]);
        }

        /// <summary>
        /// Convert a Three.js Y-up coordinate to a Rhino Z-up Vector3d.
        /// </summary>
        public static Vector3d VectorFromYUp(double x, double y, double z)
        {
            return new Vector3d(x, -z, y);
        }

        /// <summary>
        /// Reconstruct a Rhino Plane from Three.js Y-up Euler XYZ angles (radians).
        /// Inverse of PlaneToYUpEuler.
        /// </summary>
        public static Plane FromYUpEuler(double[] euler, Point3d origin)
        {
            double ex = euler[0], ey = euler[1], ez = euler[2];
            double cx = Math.Cos(ex), sx = Math.Sin(ex);
            double cy = Math.Cos(ey), sy = Math.Sin(ey);
            double cz = Math.Cos(ez), sz = Math.Sin(ez);

            // R = Rx(ex) · Ry(ey) · Rz(ez)  — Three.js default XYZ intrinsic order
            // Column 0 (X-axis in Y-up)
            double c0x = cy * cz;
            double c0y = cx * sz + sx * sy * cz;
            double c0z = sx * sz - cx * sy * cz;

            // Column 1 (Y-axis in Y-up)
            double c1x = -cy * sz;
            double c1y = cx * cz - sx * sy * sz;
            double c1z = sx * cz + cx * sy * sz;

            // Convert each column from Y-up to Z-up: (x, y, z) → (x, -z, y)
            var xAxis = new Vector3d(c0x, -c0z, c0y);
            var yAxis = new Vector3d(c1x, -c1z, c1y);

            return new Plane(origin, xAxis, yAxis);
        }
    }
}
