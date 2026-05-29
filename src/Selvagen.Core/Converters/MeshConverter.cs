using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Converts Rhino Mesh geometry to Three.js BufferGeometry JSON.
    /// Handles Z-up → Y-up coordinate swap and quad triangulation.
    /// </summary>
    public static class MeshConverter
    {
        /// <summary>
        /// Convert a Rhino Mesh to a Three.js BufferGeometry model.
        /// </summary>
        /// <param name="mesh">The Rhino mesh (Z-up coordinate system).</param>
        /// <returns>BufferGeometry in Y-up coordinate system.</returns>
        public static BufferGeometry ToBufferGeometry(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            mesh.Normals.ComputeNormals();
            mesh.Compact();

            var vertices = mesh.Vertices;
            var normals = mesh.Normals;
            var faces = mesh.Faces;

            // --- Build position + normal arrays (Z-up → Y-up) ---
            var positionArray = new double[vertices.Count * 3];
            var normalArray = new double[normals.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
                CoordinateHelper.WriteYUp(vertices[i], positionArray, i * 3);

            for (int i = 0; i < normals.Count; i++)
                CoordinateHelper.WriteYUp(normals[i], normalArray, i * 3);

            // --- Build vertex color array (RGB normalized 0-1) ---
            BufferAttribute colorAttribute = null;
            var vertexColors = mesh.VertexColors;
            if (vertexColors != null && vertexColors.Count == vertices.Count)
            {
                var colorArray = new double[vertices.Count * 3];
                for (int i = 0; i < vertexColors.Count; i++)
                {
                    var c = vertexColors[i];
                    colorArray[i * 3]     = c.R / 255.0;
                    colorArray[i * 3 + 1] = c.G / 255.0;
                    colorArray[i * 3 + 2] = c.B / 255.0;
                }
                colorAttribute = new BufferAttribute
                {
                    ItemSize = 3,
                    Type = "Float32Array",
                    Array = colorArray,
                    Normalized = false,
                };
            }

            // --- Build index array (triangulate quads) ---
            var indices = new List<int>();
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                // First triangle
                indices.Add(face.A);
                indices.Add(face.B);
                indices.Add(face.C);

                // Quad → second triangle
                if (face.IsQuad)
                {
                    indices.Add(face.A);
                    indices.Add(face.C);
                    indices.Add(face.D);
                }
            }

            return new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes
                    {
                        Position = new BufferAttribute
                        {
                            ItemSize = 3,
                            Type = "Float32Array",
                            Array = positionArray,
                            Normalized = false,
                        },
                        Normal = new BufferAttribute
                        {
                            ItemSize = 3,
                            Type = "Float32Array",
                            Array = normalArray,
                            Normalized = false,
                        },
                        Color = colorAttribute,
                    },
                    Index = new BufferGeometryIndex
                    {
                        Type = vertices.Count > 65535 ? "Uint32Array" : "Uint16Array",
                        Array = indices.ToArray(),
                    },
                },
            };
        }

        /// <summary>
        /// Convert a Three.js BufferGeometry model back to a Rhino Mesh.
        /// Handles Y-up → Z-up coordinate swap and optional vertex colors.
        /// </summary>
        public static Mesh FromBufferGeometry(BufferGeometry bg)
        {
            BufferGeometryValidator.ValidateForDecode(bg);

            var mesh = new Mesh();
            var posArr = bg.Data.Attributes.Position.Array;
            int vertCount = posArr.Length / 3;

            for (int i = 0; i < vertCount; i++)
                mesh.Vertices.Add(CoordinateHelper.FromYUp(posArr, i * 3));

            var idxArr = bg.Data.Index?.Array;
            if (idxArr != null)
            {
                for (int i = 0; i + 2 < idxArr.Length; i += 3)
                    mesh.Faces.AddFace(idxArr[i], idxArr[i + 1], idxArr[i + 2]);
            }

            var normAttr = bg.Data.Attributes.Normal;
            if (normAttr?.Array != null && normAttr.Array.Length == vertCount * 3)
            {
                var normArr = normAttr.Array;
                for (int i = 0; i < vertCount; i++)
                    mesh.Normals.Add(CoordinateHelper.VectorFromYUp(
                        normArr[i * 3], normArr[i * 3 + 1], normArr[i * 3 + 2]));
            }
            else
            {
                mesh.Normals.ComputeNormals();
            }

            var colorAttr = bg.Data.Attributes.Color;
            if (colorAttr?.Array != null && colorAttr.Array.Length == vertCount * 3)
            {
                var colArr = colorAttr.Array;
                for (int i = 0; i < vertCount; i++)
                {
                    int r = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3] * 255.0)));
                    int g = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3 + 1] * 255.0)));
                    int b = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3 + 2] * 255.0)));
                    mesh.VertexColors.Add(r, g, b);
                }
            }

            return mesh;
        }
    }
}
