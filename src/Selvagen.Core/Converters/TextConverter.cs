using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Converts Rhino TextDots (or point+text pairs) to Text3DSet JSON format.
    /// Applies Z-up → Y-up coordinate swap.
    /// </summary>
    public static class TextConverter
    {
        /// <summary>
        /// Convert Rhino TextDots to a Text3DSet model.
        /// </summary>
        public static Text3DSet FromTextDots(IEnumerable<TextDot> dots)
        {
            if (dots == null)
                throw new ArgumentNullException(nameof(dots));

            var labels = new List<LabelData>();
            int index = 0;

            foreach (var dot in dots)
            {
                if (dot == null) continue;

                labels.Add(new LabelData
                {
                    Id = $"label-{index}",
                    Text = dot.Text ?? "",
                    Position = CoordinateHelper.ToYUp(dot.Point),
                });

                index++;
            }

            return new Text3DSet { Labels = labels.ToArray() };
        }

        /// <summary>
        /// Convert parallel arrays of points and text strings to a Text3DSet model.
        /// Useful for custom Grasshopper component inputs.
        /// </summary>
        public static Text3DSet FromPointsAndTexts(IList<Point3d> points, IList<string> texts)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (points.Count != texts.Count)
                throw new ArgumentException("points and texts must have the same length");

            var labels = new LabelData[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                labels[i] = new LabelData
                {
                    Id = $"label-{i}",
                    Text = texts[i] ?? "",
                    Position = CoordinateHelper.ToYUp(points[i]),
                };
            }

            return new Text3DSet { Labels = labels };
        }

        /// <summary>
        /// Convert parallel arrays of planes, text strings, and optional per-label colors to a Text3DSet model.
        /// Each plane provides both position (origin) and orientation (basis → Y-up Euler XYZ).
        /// </summary>
        /// <param name="planes">Rhino planes (Z-up). Origin → position, basis → rotation.</param>
        /// <param name="texts">Label text strings. Must match planes in count.</param>
        /// <param name="colors">Optional per-label colors. If shorter than planes, the last color repeats.</param>
        public static Text3DSet FromPlanesAndTexts(
            IList<Plane> planes,
            IList<string> texts,
            IList<Color> colors = null)
        {
            if (planes == null) throw new ArgumentNullException(nameof(planes));
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (planes.Count != texts.Count)
                throw new ArgumentException("planes and texts must have the same length");

            var labels = new LabelData[planes.Count];
            for (int i = 0; i < planes.Count; i++)
            {
                var label = new LabelData
                {
                    Id = $"label-{i}",
                    Text = texts[i] ?? "",
                    Position = CoordinateHelper.ToYUp(planes[i].Origin),
                    Rotation = CoordinateHelper.PlaneToYUpEuler(planes[i]),
                };

                if (colors != null && colors.Count > 0)
                {
                    var c = colors[Math.Min(i, colors.Count - 1)];
                    label.Color = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
                }

                labels[i] = label;
            }

            return new Text3DSet { Labels = labels };
        }
    }
}
