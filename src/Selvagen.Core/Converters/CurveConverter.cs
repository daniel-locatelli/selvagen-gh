using System;
using System.Collections.Generic;
using System.Drawing;
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
        /// <summary>Angle tolerance in radians for NURBS → polyline tessellation.</summary>
        private const double AngleTolerance = 0.1;

        /// <summary>
        /// Convert a collection of Rhino curves to a CurveSet model.
        /// </summary>
        /// <param name="curves">Rhino curves (Z-up coordinate system).</param>
        /// <param name="colors">Optional per-curve colors. If shorter than curves, the last color repeats.</param>
        /// <param name="linewidths">Optional per-curve line widths. If shorter than curves, the last width repeats.</param>
        /// <param name="tolerance">Distance tolerance for NURBS → polyline tessellation.</param>
        /// <returns>CurveSet in Y-up coordinate system.</returns>
        public static CurveSet ToCurveSet(
            IEnumerable<Curve> curves,
            IList<Color> colors = null,
            IList<double> linewidths = null,
            double tolerance = 0.01)
        {
            if (curves == null)
                throw new ArgumentNullException(nameof(curves));

            var curveDataList = new List<CurveData>();
            int index = 0;

            foreach (var curve in curves)
            {
                if (curve == null) continue;

                var polyline = curve.ToPolyline(tolerance, AngleTolerance, 0, 0)?.ToPolyline();
                if (polyline == null) continue;

                var points = new double[polyline.Count * 3];
                for (int i = 0; i < polyline.Count; i++)
                    CoordinateHelper.WriteYUp(polyline[i], points, i * 3);

                var data = new CurveData
                {
                    Id = $"curve-{index}",
                    Points = points,
                    Closed = curve.IsClosed,
                };

                if (colors != null && colors.Count > 0)
                {
                    var c = colors[Math.Min(index, colors.Count - 1)];
                    data.Color = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
                }

                if (linewidths != null && linewidths.Count > 0)
                    data.Linewidth = linewidths[Math.Min(index, linewidths.Count - 1)];

                curveDataList.Add(data);
                index++;
            }

            return new CurveSet { Curves = curveDataList.ToArray() };
        }
    }
}
