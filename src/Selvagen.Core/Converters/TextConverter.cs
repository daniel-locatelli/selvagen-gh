using System;
using System.Collections.Generic;
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
    }
}
