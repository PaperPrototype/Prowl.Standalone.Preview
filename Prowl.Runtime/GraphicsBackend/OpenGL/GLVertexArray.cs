// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Silk.NET.OpenGL;

using static Prowl.Runtime.GraphicsBackend.VertexFormat;

namespace Prowl.Runtime.GraphicsBackend.OpenGL;

public sealed unsafe class GLVertexArray : GraphicsVertexArray
{
    public uint Handle { get; private set; }

    public GLVertexArray(
        VertexFormat format,
        GraphicsBuffer vertices,
        GraphicsBuffer? indices,
        VertexFormat? instanceFormat = null,
        GraphicsBuffer? instanceBuffer = null)
    {
        Handle = GLDevice.GL.GenVertexArray();

        if (Handle == 0)
        {
            throw new System.Exception("Failed to create VAO - glGenVertexArray returned 0");
        }

        GLDevice.GL.BindVertexArray(Handle);

        // Bind vertex buffer and set up per-vertex attributes
        GLDevice.GL.BindBuffer(BufferTargetARB.ArrayBuffer, (vertices as GLBuffer).Handle);
        BindFormat(format);

        // Bind instance buffer and set up per-instance attributes (if provided)
        if (instanceFormat != null && instanceBuffer != null)
        {
            GLDevice.GL.BindBuffer(BufferTargetARB.ArrayBuffer, (instanceBuffer as GLBuffer).Handle);
            BindFormat(instanceFormat);
        }

        // Bind index buffer if present
        if (indices != null)
            GLDevice.GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, (indices as GLBuffer).Handle);

        GLDevice.GL.BindVertexArray(0);
    }

    void BindFormat(VertexFormat format)
    {
        for (int i = 0; i < format.Elements.Length; i++)
        {
            Element element = format.Elements[i];
            uint index = element.Semantic;
            GLDevice.GL.EnableVertexAttribArray(index);
            int offset = element.Offset;
            unsafe
            {
                if (element.Type == VertexType.Float)
                    GLDevice.GL.VertexAttribPointer(index, element.Count, (GLEnum)element.Type, element.Normalized, (uint)format.Size, (void*)offset);
                else
                    GLDevice.GL.VertexAttribIPointer(index, element.Count, (GLEnum)element.Type, (uint)format.Size, (void*)offset);

                // Set divisor for instancing (0 = per-vertex, 1+ = per-instance)
                if (element.Divisor > 0)
                {
                    GLDevice.GL.VertexAttribDivisor(index, (uint)element.Divisor);
                }
            }
        }
    }

    public override bool IsDisposed { get; protected set; }

    public override void Dispose()
    {
        if (IsDisposed)
            return;

        GLDevice.GL.DeleteVertexArray(Handle);
        IsDisposed = true;
    }

    public override string ToString()
    {
        return Handle.ToString();
    }
}
