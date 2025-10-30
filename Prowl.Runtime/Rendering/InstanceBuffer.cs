// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using Prowl.Vector;

using static Prowl.Runtime.GraphicsBackend.VertexFormat;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Internal buffer management for instanced rendering.
/// Automatically pools and resizes instance buffers per mesh.
/// </summary>
internal class InstanceBuffer : IDisposable
{
    private readonly Mesh _mesh;
    private readonly VertexFormat? _customLayout;
    private GraphicsVertexArray? _vao;
    private GraphicsBuffer? _instanceBuffer;  // Combined buffer for matrix + custom data
    private int _currentCapacity;
    private VertexFormat? _combinedInstanceFormat;

    public GraphicsVertexArray? VAO => _vao;

    public InstanceBuffer(Mesh mesh, VertexFormat? customLayout = null)
    {
        _mesh = mesh;
        _customLayout = customLayout;
        _currentCapacity = 1023; // Start with max batch size for OpenGL compatibility

        Initialize();
    }

    private void Initialize()
    {
        // Ensure mesh is uploaded to GPU
        _mesh.Upload();

        // Build combined instance format (matrix + custom data)
        List<Element> instanceElements = new()
        {
            new Element((VertexSemantic)8, VertexType.Float, 4, divisor: 1),  // ModelRow0
            new Element((VertexSemantic)9, VertexType.Float, 4, divisor: 1),  // ModelRow1
            new Element((VertexSemantic)10, VertexType.Float, 4, divisor: 1), // ModelRow2
            new Element((VertexSemantic)11, VertexType.Float, 4, divisor: 1), // ModelRow3
        };

        // Add custom data elements if provided
        if (_customLayout != null)
        {
            foreach (var element in _customLayout.Elements)
            {
                instanceElements.Add(element);
            }
        }

        _combinedInstanceFormat = new VertexFormat(instanceElements.ToArray());

        // Create single instance buffer for all instance data (matrix + custom)
        int instanceDataSize = _combinedInstanceFormat.Size * _currentCapacity;
        _instanceBuffer = Graphics.Device.CreateBuffer(
            BufferType.VertexBuffer,
            new byte[instanceDataSize],
            dynamic: true
        );

        // Create VAO with combined instance format
        _vao = Graphics.Device.CreateVertexArray(
            Mesh.GetVertexLayout(_mesh),
            _mesh.VertexBuffer,
            _mesh.IndexBuffer,
            _combinedInstanceFormat,
            _instanceBuffer
        );
    }

    /// <summary>
    /// Uploads transform matrices to the instance buffer.
    /// </summary>
    public void UploadMatrices(Float4x4[] matrices, int offset, int count)
    {
        // Resize buffer if needed
        if (count > _currentCapacity)
        {
            ResizeBuffer(count);
        }

        // Convert matrices to flat float array
        float[] matrixData = new float[count * 16];
        for (int i = 0; i < count; i++)
        {
            Float4x4 mat = matrices[offset + i];
            // Store as column-major (OpenGL default)
            int idx = i * 16;
            matrixData[idx + 0] = mat.c0.X;
            matrixData[idx + 1] = mat.c0.Y;
            matrixData[idx + 2] = mat.c0.Z;
            matrixData[idx + 3] = mat.c0.W;

            matrixData[idx + 4] = mat.c1.X;
            matrixData[idx + 5] = mat.c1.Y;
            matrixData[idx + 6] = mat.c1.Z;
            matrixData[idx + 7] = mat.c1.W;

            matrixData[idx + 8] = mat.c2.X;
            matrixData[idx + 9] = mat.c2.Y;
            matrixData[idx + 10] = mat.c2.Z;
            matrixData[idx + 11] = mat.c2.W;

            matrixData[idx + 12] = mat.c3.X;
            matrixData[idx + 13] = mat.c3.Y;
            matrixData[idx + 14] = mat.c3.Z;
            matrixData[idx + 15] = mat.c3.W;
        }

        // Upload to GPU
        if (_instanceBuffer != null)
            Graphics.Device.SetBuffer(_instanceBuffer, matrixData, dynamic: true);
    }

    /// <summary>
    /// Uploads matrices and custom instance data.
    /// NOTE: Data must be interleaved per-instance (matrix first, then custom data for each instance).
    /// </summary>
    public void UploadMatricesAndCustomData(Float4x4[] matrices, float[] customData, VertexFormat layout, int count)
    {
        // TODO: Implement interleaved upload for matrix + custom data
        // For now, just upload matrices
        UploadMatrices(matrices, 0, count);
    }

    private void ResizeBuffer(int newCapacity)
    {
        _currentCapacity = newCapacity;

        // Resize combined instance buffer
        if (_instanceBuffer != null && _combinedInstanceFormat != null)
        {
            _instanceBuffer.Dispose();
            int instanceDataSize = _combinedInstanceFormat.Size * _currentCapacity;
            _instanceBuffer = Graphics.Device.CreateBuffer(
                BufferType.VertexBuffer,
                new byte[instanceDataSize],
                dynamic: true
            );
        }

        // Recreate VAO with new buffer
        Initialize();
    }

    public void Dispose()
    {
        _vao?.Dispose();
        _instanceBuffer?.Dispose();
    }
}
