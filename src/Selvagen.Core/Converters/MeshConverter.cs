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

            // --- Build position + normal arrays (Z-up → Y-up: X,Z,-Y) ---
            var positionArray = new double[vertices.Count * 3];
            var normalArray = new double[normals.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                positionArray[i * 3] = v.X;
                positionArray[i * 3 + 1] = v.Z;      // Y_three = Z_rhino
                positionArray[i * 3 + 2] = -v.Y;     // Z_three = -Y_rhino
            }

            for (int i = 0; i < normals.Count; i++)
            {
                var n = normals[i];
                normalArray[i * 3] = n.X;
                normalArray[i * 3 + 1] = n.Z;
                normalArray[i * 3 + 2] = -n.Y;
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
                    },
                    Index = new BufferGeometryIndex
                    {
                        Type = indices.Count > 65535 ? "Uint32Array" : "Uint16Array",
                        Array = indices.ToArray(),
                    },
                },
            };
        }
    }
}
