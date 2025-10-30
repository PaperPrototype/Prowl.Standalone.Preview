// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// A simple instanced renderable for drawing multiple instances of a mesh.
/// Useful for drawing many copies of the same object efficiently (trees, grass, particles, etc.)
/// Uses the mesh's cached instance VAO for optimal performance.
/// </summary>
public class InstancedMeshRenderable : IRenderable
{
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly int _layerIndex;
    private readonly PropertyState _sharedProperties;
    private readonly AABB _bounds;
    private readonly InstanceData[] _instanceData;

    public InstancedMeshRenderable(
        Mesh mesh,
        Material material,
        InstanceData[] instanceData,
        int layerIndex = 0,
        PropertyState? sharedProperties = null,
        AABB? bounds = null)
    {
        _mesh = mesh;
        _material = material;
        _instanceData = instanceData;
        _layerIndex = layerIndex;
        _sharedProperties = sharedProperties ?? new PropertyState();

        // Calculate bounds if not provided
        if (bounds.HasValue)
        {
            _bounds = bounds.Value;
        }
        else if (instanceData.Length > 0 && mesh != null)
        {
            // Calculate bounds from all instances
            AABB meshBounds = mesh.bounds;
            Double3 min = new Double3(double.MaxValue);
            Double3 max = new Double3(double.MinValue);

            foreach (var instance in instanceData)
            {
                AABB instanceBounds = meshBounds.TransformBy((Double4x4)instance.GetMatrix());
                min = new Double3(
                    System.Math.Min(min.X, instanceBounds.Min.X),
                    System.Math.Min(min.Y, instanceBounds.Min.Y),
                    System.Math.Min(min.Z, instanceBounds.Min.Z)
                );
                max = new Double3(
                    System.Math.Max(max.X, instanceBounds.Max.X),
                    System.Math.Max(max.Y, instanceBounds.Max.Y),
                    System.Math.Max(max.Z, instanceBounds.Max.Z)
                );
            }

            _bounds = new AABB(min, max);
        }
        else
        {
            _bounds = new AABB(Double3.Zero, Double3.Zero);
        }
    }

    public Material GetMaterial() => _material;
    public int GetLayer() => _layerIndex;

    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh mesh, out Double4x4 model, out int instanceCount)
    {
        properties = _sharedProperties;
        mesh = _mesh;
        model = Double4x4.Identity; // Not used for instanced rendering
        instanceCount = _instanceData.Length; // > 1 triggers instanced rendering
    }

    public void GetCullingData(out bool isRenderable, out AABB bounds)
    {
        isRenderable = _instanceData.Length > 0 && _mesh != null && _material != null;
        bounds = _bounds;
    }

    public void GetInstancedVAO(ViewerData viewer, out GraphicsVertexArray vao)
    {
        // Use mesh's cached instance VAO (creates it on first use, reuses thereafter)
        vao = _mesh.GetOrCreateInstanceVAO(_instanceData, _instanceData.Length);
    }
}
