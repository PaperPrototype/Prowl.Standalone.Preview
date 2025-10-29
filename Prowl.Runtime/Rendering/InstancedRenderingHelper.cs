// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.OpenGL;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using static Prowl.Runtime.GraphicsBackend.VertexFormat;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Helper class for managing GPU-instanced rendering resources.
/// Handles instance buffer creation and VAO setup for instanced draws.
/// </summary>
public class InstancedRenderingHelper
{
    private GraphicsBuffer _instanceBuffer;
    private GraphicsVertexArray _instancedVAO;
    private int _lastInstanceCount;
    private int _bufferCapacity;
    private Mesh _lastMesh; // Track which mesh this VAO was created for

    /// <summary>
    /// Creates or updates the instance buffer and VAO for instanced rendering.
    /// </summary>
    public GraphicsVertexArray SetupInstancing(Mesh mesh, InstanceData[] instanceData)
    {
        // Ensure mesh is uploaded first
        mesh.Upload();

        // Validate mesh has required data
        if (mesh.VertexBuffer == null || mesh.VertexArrayObject == null)
        {
            throw new System.InvalidOperationException("Mesh must be uploaded and have valid buffers before creating instanced VAO");
        }

        // Create or update instance buffer with capacity management
        if (_instanceBuffer == null || instanceData.Length > _bufferCapacity)
        {
            // Need to create/resize buffer - allocate with 50% extra capacity for growth
            _bufferCapacity = (int)(instanceData.Length * 1.5f);

            // Create array with capacity (pad with empty data)
            var bufferData = new InstanceData[_bufferCapacity];
            System.Array.Copy(instanceData, 0, bufferData, 0, instanceData.Length);

            _instanceBuffer?.Dispose();
            _instanceBuffer = Graphics.Device.CreateBuffer(BufferType.VertexBuffer, bufferData, dynamic: true);
            _lastInstanceCount = instanceData.Length;

            // Dispose old VAO since we need to recreate it with new buffer
            _instancedVAO?.Dispose();
            _instancedVAO = null;
        }
        else if (_lastInstanceCount != instanceData.Length)
        {
            // Count changed but buffer has capacity - just update the data
            Graphics.Device.SetBuffer(_instanceBuffer, instanceData, dynamic: true);
            _lastInstanceCount = instanceData.Length;
        }
        else
        {
            // Same count - update existing buffer
            Graphics.Device.SetBuffer(_instanceBuffer, instanceData, dynamic: true);
        }

        // Create instanced VAO if needed, or if mesh changed
        if (_instancedVAO == null || _lastMesh != mesh)
        {
            // Dispose old VAO if mesh changed
            if (_lastMesh != mesh && _instancedVAO != null)
            {
                _instancedVAO.Dispose();
                _instancedVAO = null;
            }

            _lastMesh = mesh;

            // Get mesh vertex format and buffers
            var meshFormat = Mesh.GetVertexLayout(mesh);
            var vertexBuffer = mesh.VertexBuffer;
            var indexBuffer = mesh.IndexBuffer;

            // Define instance data format
            // We use semantics 8+ for instance attributes to avoid conflicts with vertex attributes
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
            _instancedVAO = new GLInstancedVertexArray(
                meshFormat,
                vertexBuffer,
                indexBuffer,
                instanceFormat,
                _instanceBuffer
            );
        }

        return _instancedVAO;
    }

    public void Dispose()
    {
        _instanceBuffer?.Dispose();
        _instancedVAO?.Dispose();
    }
}
