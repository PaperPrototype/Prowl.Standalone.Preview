// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;
using static Prowl.Runtime.GraphicsBackend.VertexFormat;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// A simple instanced renderable for drawing multiple instances of a mesh.
/// Useful for drawing many copies of the same object efficiently (trees, grass, particles, etc.)
/// </summary>
public class InstancedMeshRenderable : IInstancedRenderable
{
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly int _layerIndex;
    private readonly PropertyState _sharedProperties;
    private readonly AABB _bounds;

    // Manual instancing management
    private readonly GraphicsBuffer _instanceBuffer;
    private readonly GraphicsVertexArray _instancedVAO;
    private readonly int _instanceCount;

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
        _instanceCount = instanceData.Length;
        _layerIndex = layerIndex;
        _sharedProperties = sharedProperties ?? new PropertyState();

        // Create instance buffer and VAO
        if (instanceData.Length > 0 && mesh != null)
        {
            // Upload mesh
            mesh.Upload();

            // Create instance buffer
            _instanceBuffer = Graphics.Device.CreateBuffer(BufferType.VertexBuffer, instanceData, dynamic: false);

            // Get mesh vertex format
            var meshFormat = Mesh.GetVertexLayout(mesh);

            // Define instance data format
            var instanceFormat = new VertexFormat(
            [
                // mat4 takes 4 attribute slots (one per row)
                new((VertexSemantic)8, VertexType.Float, 4, divisor: 1),  // ModelRow0
                new((VertexSemantic)9, VertexType.Float, 4, divisor: 1),  // ModelRow1
                new((VertexSemantic)10, VertexType.Float, 4, divisor: 1), // ModelRow2
                new((VertexSemantic)11, VertexType.Float, 4, divisor: 1), // ModelRow3
                new((VertexSemantic)12, VertexType.Float, 4, divisor: 1), // Color (RGBA)
                new((VertexSemantic)13, VertexType.Float, 4, divisor: 1), // CustomData
            ]);

            // Create instanced VAO
            _instancedVAO = Graphics.Device.CreateVertexArray(
                meshFormat,
                mesh.VertexBuffer,
                mesh.IndexBuffer,
                instanceFormat,
                _instanceBuffer
            );
        }

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

    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh drawData, out Double4x4 model)
    {
        // For instanced rendering, this shouldn't be called
        // But we provide fallback data just in case
        properties = _sharedProperties;
        drawData = _mesh;
        model = Double4x4.Identity;
    }

    public void GetCullingData(out bool isRenderable, out AABB bounds)
    {
        isRenderable = _instanceCount > 0 && _mesh != null && _material != null && _instancedVAO != null;
        bounds = _bounds;
    }

    public void GetInstanceData(ViewerData viewer, out PropertyState properties, out GraphicsVertexArray vao, out int instanceCount, out int indexCount, out bool useIndex32)
    {
        properties = _sharedProperties;
        vao = _instancedVAO;
        instanceCount = _instanceCount;
        indexCount = _mesh != null ? _mesh.IndexCount : 0;
        useIndex32 = _mesh != null && _mesh.IndexFormat == IndexFormat.UInt32;
    }
}
