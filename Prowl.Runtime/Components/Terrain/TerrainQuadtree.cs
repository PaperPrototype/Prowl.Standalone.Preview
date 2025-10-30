// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Prowl.Vector;

namespace Prowl.Runtime.Terrain;

/// <summary>
/// Manages the quadtree LOD system for terrain rendering.
/// Determines which chunks to render based on camera distance.
/// </summary>
public class TerrainQuadtree
{
    public TerrainChunk Root;
    public int MaxLODLevel;
    public double ChunkSize;
    public List<TerrainChunk> VisibleChunks = new();

    public TerrainQuadtree(Double3 origin, double terrainSize, int maxLOD)
    {
        MaxLODLevel = maxLOD;
        ChunkSize = terrainSize;
        Root = new TerrainChunk(origin, terrainSize, 0);
    }

    /// <summary>
    /// Updates the quadtree based on camera position.
    /// Determines which chunks should be visible.
    /// Only recalculates when camera has moved significantly.
    /// </summary>
    public void Update(Double3 cameraPosition)
    {
        // Clear visibility and update tree
        VisibleChunks.Clear();
        UpdateNode(Root, cameraPosition);
    }

    private void UpdateNode(TerrainChunk chunk, Double3 cameraPosition)
    {
        // Calculate distance from camera to chunk center
        Double3 chunkCenter = chunk.Position + new Double3(chunk.Size * 0.5, 0, chunk.Size * 0.5);
        double distanceToCamera = Double3.Distance(cameraPosition, chunkCenter);

        var size = chunk.Size * 1.5;

        // Simple subdivision rule: subdivide if camera is closer than chunk size
        if (distanceToCamera < size && chunk.LODLevel < MaxLODLevel)
        {
            // Subdivide and recurse into children
            if (chunk.Children == null)
                chunk.Subdivide();

            foreach (var child in chunk.Children)
            {
                UpdateNode(child, cameraPosition);
            }
        }
        else
        {
            // Should not subdivide - check if we should merge existing children
            if (chunk.Children != null)
            {
                // Merge threshold is 1.5x chunk size to add hysteresis
                if (distanceToCamera > size)
                {
                    chunk.Merge();
                }
            }

            // This is a leaf node at appropriate LOD - mark as visible
            chunk.IsVisible = true;
            VisibleChunks.Add(chunk);
        }
    }

    public void DrawGizmos(Double3 offset)
    {
        Root.DrawGizmos(offset);
    }

    /// <summary>
    /// Gets all visible leaf chunks for rendering.
    /// </summary>
    public List<TerrainChunk> GetVisibleChunks()
    {
        return VisibleChunks;
    }
}
