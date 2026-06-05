using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    public static class LabelConverter
    {
        private static readonly string[] AnchorXValues = { "left", "center", "right" };
        private static readonly string[] AnchorYValues = { "bottom", "middle", "top" };

        public static (string anchorX, string anchorY) JustificationToAnchors(int justification)
        {
            justification = Math.Max(0, Math.Min(8, justification));
            int col = justification % 3;
            int row = justification / 3;
            return (AnchorXValues[col], AnchorYValues[row]);
        }

        public static int AnchorsToJustification(string anchorX, string anchorY)
        {
            int col = 1;
            if (anchorX == "left") col = 0;
            else if (anchorX == "right") col = 2;

            int row = 1;
            if (anchorY == "bottom") row = 0;
            else if (anchorY == "top") row = 2;

            return row * 3 + col;
        }

        public static LabelSet ToLabelSetFromDots(IEnumerable<TextDot> dots)
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

            return new LabelSet { Labels = labels.ToArray() };
        }

        public static LabelSet ToLabelSetFromPoints(IList<Point3d> points, IList<string> texts)
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

            return new LabelSet { Labels = labels };
        }

        public static LabelSet ToLabelSet(
            IList<Plane> planes,
            IList<string> texts,
            IList<Color> colors = null,
            IList<int> justifications = null,
            IList<double> fontSizes = null)
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

                if (justifications != null && justifications.Count > 0)
                {
                    int j = justifications[Math.Min(i, justifications.Count - 1)];
                    var (ax, ay) = JustificationToAnchors(j);
                    label.AnchorX = ax;
                    label.AnchorY = ay;
                }

                if (fontSizes != null && fontSizes.Count > 0)
                {
                    double size = fontSizes[Math.Min(i, fontSizes.Count - 1)];
                    if (size > 0)
                        label.FontSize = size;
                }

                labels[i] = label;
            }

            return new LabelSet { Labels = labels };
        }

        public static void FromLabelSet(LabelSet ls,
            out List<Plane> planes,
            out List<string> texts,
            out List<Color> colors,
            out List<double> fontSizes,
            out List<int> justifications)
        {
            if (ls == null)
                throw new ArgumentNullException(nameof(ls));

            planes = new List<Plane>();
            texts = new List<string>();
            colors = new List<Color>();
            fontSizes = new List<double>();
            justifications = new List<int>();

            foreach (var label in ls.Labels)
            {
                if (label == null) continue;

                var origin = CoordinateHelper.FromYUp(
                    label.Position[0], label.Position[1], label.Position[2]);

                Plane plane;
                if (label.Rotation != null && label.Rotation.Length == 3)
                    plane = CoordinateHelper.FromYUpEuler(label.Rotation, origin);
                else
                    plane = new Plane(origin, Vector3d.XAxis, Vector3d.YAxis);

                planes.Add(plane);
                texts.Add(label.Text ?? "");

                if (!string.IsNullOrEmpty(label.Color) && label.Color.StartsWith("#"))
                    colors.Add(ColorTranslator.FromHtml(label.Color));
                else
                    colors.Add(Color.Black);

                fontSizes.Add(label.FontSize ?? 0.0);
                justifications.Add(AnchorsToJustification(label.AnchorX, label.AnchorY));
            }
        }
    }
}
