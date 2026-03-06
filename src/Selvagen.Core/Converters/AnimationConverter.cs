using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Result of converting a sequence of Rhino meshes into animation data.
    /// </summary>
    public class AnimationConversionResult
    {
        /// <summary>
        /// The base mesh (full BufferGeometry from the first frame).
        /// Upload this to the meshes table to get a base_asset_id.
        /// </summary>
        public BufferGeometry BaseMesh { get; set; }

        /// <summary>
        /// Per-frame data. Frame[0] corresponds to the first mesh in the input list.
        /// </summary>
        public AnimationFrameData[] Frames { get; set; }

        /// <summary>
        /// True if all frames share the same vertex count as the base mesh.
        /// When false, some frames contain full BufferGeometry instead of position-only.
        /// </summary>
        public bool TopologyConsistent { get; set; }
    }

    /// <summary>
    /// Converts a sequence of Rhino meshes into animation frame data.
    /// Uses position-only frames when topology is consistent, falling back
    /// to full BufferGeometry per frame when vertex count changes.
    /// </summary>
    public static class AnimationConverter
    {
        /// <summary>
        /// Convert a list of Rhino meshes into animation data.
        /// The first mesh becomes the base (full BufferGeometry with indices and normals).
        /// Subsequent meshes store only their position arrays if they share the same vertex count.
        /// </summary>
        public static AnimationConversionResult Convert(IList<Mesh> meshes)
        {
            if (meshes == null || meshes.Count == 0)
                throw new ArgumentException("At least one mesh is required.", nameof(meshes));

            // Prepare the base mesh from frame 0
            var baseMesh = meshes[0];
            if (baseMesh == null)
                throw new ArgumentException("Frame 0 mesh is null.", nameof(meshes));

            baseMesh.Normals.ComputeNormals();
            baseMesh.Compact();
            int baseVertexCount = baseMesh.Vertices.Count;

            var baseGeometry = MeshConverter.ToBufferGeometry(baseMesh);

            // Convert all frames
            var frames = new AnimationFrameData[meshes.Count];
            bool allConsistent = true;

            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                if (mesh == null)
                    throw new ArgumentException($"Frame {i} mesh is null.", nameof(meshes));

                mesh.Normals.ComputeNormals();
                mesh.Compact();

                if (mesh.Vertices.Count == baseVertexCount)
                {
                    // Same topology: store position array only
                    frames[i] = new AnimationFrameData
                    {
                        Format = "positions",
                        Positions = ExtractPositions(mesh),
                    };
                }
                else
                {
                    // Topology changed: store full BufferGeometry
                    allConsistent = false;
                    frames[i] = new AnimationFrameData
                    {
                        Format = "buffer_geometry",
                        Geometry = MeshConverter.ToBufferGeometry(mesh),
                    };
                }
            }

            return new AnimationConversionResult
            {
                BaseMesh = baseGeometry,
                Frames = frames,
                TopologyConsistent = allConsistent,
            };
        }

        /// <summary>
        /// Extract Y-up position array from a Rhino mesh.
        /// </summary>
        private static double[] ExtractPositions(Mesh mesh)
        {
            var vertices = mesh.Vertices;
            var positions = new double[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
                CoordinateHelper.WriteYUp(vertices[i], positions, i * 3);

            return positions;
        }
    }
}
