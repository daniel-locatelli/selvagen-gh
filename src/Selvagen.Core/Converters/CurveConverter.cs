using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Converts Rhino Curves to CurveSet JSON format.
    /// Tessellates NURBS to polylines and applies Z-up → Y-up coordinate swap.
    /// </summary>
    public static class CurveConverter
    {
        /// <summary>
        /// Convert a collection of Rhino curves to a CurveSet model.
        /// </summary>
        /// <param name="curves">Rhino curves (Z-up coordinate system).</param>
        /// <param name="tolerance">Tessellation tolerance for NURBS → polyline. Default uses Rhino document tolerance.</param>
        /// <returns>CurveSet in Y-up coordinate system.</returns>
        public static CurveSet ToCurveSet(IEnumerable<Curve> curves, double tolerance = 0.01)
        {
            if (curves == null)
                throw new ArgumentNullException(nameof(curves));

            var curveDataList = new List<CurveData>();
            int index = 0;

            foreach (var curve in curves)
            {
                if (curve == null) continue;

                var polyline = curve.ToPolyline(tolerance, 0.1, 0, 0)?.ToPolyline();
                if (polyline == null) continue;

                var points = new double[polyline.Count * 3];
                for (int i = 0; i < polyline.Count; i++)
                {
                    var pt = polyline[i];
                    points[i * 3] = pt.X;
                    points[i * 3 + 1] = pt.Z;      // Y_three = Z_rhino
                    points[i * 3 + 2] = -pt.Y;     // Z_three = -Y_rhino
                }

                curveDataList.Add(new CurveData
                {
                    Id = $"curve-{index}",
                    Points = points,
                    Closed = curve.IsClosed,
                });

                index++;
            }

            return new CurveSet { Curves = curveDataList.ToArray() };
        }
    }
}
