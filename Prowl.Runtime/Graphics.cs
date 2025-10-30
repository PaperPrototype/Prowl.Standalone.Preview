// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.OpenGL;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime;

public class MeshRenderable : IRenderable
{
    private Mesh _mesh;
    private Material _material;
    private Double4x4 _transform;
    private int _layerIndex;
    private PropertyState _properties;

    public MeshRenderable(Mesh mesh, Material material, Double4x4 matrix, int layerIndex, PropertyState? propertyBlock = null)
    {
        _mesh = mesh;
        _material = material;
        _transform = matrix;
        _layerIndex = layerIndex;
        _properties = propertyBlock ?? new();
    }

    public Material GetMaterial() => _material;
    public int GetLayer() => _layerIndex;

    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh drawData, out Double4x4 model)
    {
        drawData = _mesh;
        properties = _properties;
        model = _transform;
    }

    public void GetCullingData(out bool isRenderable, out AABB bounds)
    {
        isRenderable = true;
        //bounds = Bounds.CreateFromMinMax(new Vector3(999999), new Vector3(999999));
        bounds = _mesh.bounds.TransformBy(_transform);
    }
}

public static class Graphics
{
    public static GraphicsDevice Device { get; internal set; }

#warning TODO: Move these to a separate class "GraphicsCapabilities" and add more, Their Assigned by GLDevice which is very ugly
    public static int MaxTextureSize { get; internal set; }
    public static int MaxCubeMapTextureSize { get; internal set; }
    public static int MaxArrayTextureLayers { get; internal set; }
    public static int MaxFramebufferColorAttachments { get; internal set; }

    public static Double2 ScreenSize => new(Window.InternalWindow.FramebufferSize.X, Window.InternalWindow.FramebufferSize.Y);
    public static IntRect ScreenRect => new(0, 0, Window.InternalWindow.FramebufferSize.X, Window.InternalWindow.FramebufferSize.Y);

    private static Shader? s_blitShader;
    private static Material? s_blitMaterial;
    public static Material BlitMaterial
    {
        get
        {
            if (s_blitShader.IsNotValid())
                s_blitShader = Shader.LoadDefault(DefaultShader.Blit);

            if (s_blitMaterial.IsNotValid())
                s_blitMaterial = new Material(s_blitShader);

            return s_blitMaterial;
        }
    }

    public static void Blit(Texture2D source, Material? mat = null, int pass = 0)
    {
        mat ??= BlitMaterial;
        mat.SetTexture("_MainTex", source);
        Blit(mat, pass);
    }
    public static void Blit(RenderTexture source, RenderTexture target, Material? mat = null, int pass = 0, bool clearDepth = false, bool clearColor = false, Color color = default)
    {
        mat ??= BlitMaterial;
        mat.SetTexture("_MainTex", source.MainTexture);
        Blit(target, mat, pass, clearDepth, clearColor, color);
    }
    public static void Blit(Texture2D source, RenderTexture target, Material? mat = null, int pass = 0, bool clearDepth = false, bool clearColor = false, Color color = default)
    {
        mat ??= BlitMaterial;
        mat.SetTexture("_MainTex", source);
        Blit(target, mat, pass, clearDepth, clearColor, color);
    }
    public static void Blit(RenderTexture target, Material? mat = null, int pass = 0, bool clearDepth = false, bool clearColor = false, Color color = default)
    {
        mat ??= BlitMaterial;
        if (target.IsValid())
        {
            Graphics.Device.BindFramebuffer(target.frameBuffer);
        }
        else
        {
            Graphics.Device.UnbindFramebuffer();
            Graphics.Device.Viewport(0, 0, (uint)Window.InternalWindow.FramebufferSize.X, (uint)Window.InternalWindow.FramebufferSize.Y);
        }
        if (clearDepth || clearColor)
        {
            ClearFlags clear = 0;
            if (clearDepth) clear |= ClearFlags.Depth;
            if (clearColor) clear |= ClearFlags.Color;
            Device.Clear((float)color.R, (float)color.G, (float)color.B, (float)color.A, clear | ClearFlags.Stencil);
        }
        Blit(mat, pass);
    }
    public static void Blit(Material? mat = null, int pass = 0)
    {
        mat ??= BlitMaterial;
        DrawMeshNow(Mesh.GetFullscreenQuad(), mat, pass);
    }


    /// <summary>
    /// Immediately draws a mesh without queuing. Used internally by the render pipeline.
    /// For queued rendering, use DrawMesh() instead.
    /// </summary>
    public static void DrawMeshNow(Mesh mesh, Material mat, int passIndex = 0)
    {
        if (mesh.VertexCount <= 0) return;

        // Mesh data can vary between meshes, so we need to let the shader know which attributes are in use
        mat.SetKeyword("HAS_NORMALS", mesh.HasNormals);
        mat.SetKeyword("HAS_TANGENTS", mesh.HasTangents);
        mat.SetKeyword("HAS_UV", mesh.HasUV);
        mat.SetKeyword("HAS_UV2", mesh.HasUV2);
        mat.SetKeyword("HAS_COLORS", mesh.HasColors || mesh.HasColors32);
        mat.SetKeyword("HAS_BONEINDICES", mesh.HasBoneIndices);
        mat.SetKeyword("HAS_BONEWEIGHTS", mesh.HasBoneWeights);
        mat.SetKeyword("SKINNED", mesh.HasBoneIndices && mesh.HasBoneWeights);

        Rendering.Shaders.ShaderPass pass = mat.Shader.GetPass(passIndex);

        if (!pass.TryGetVariantProgram(mat._localKeywords, out GraphicsProgram? variant))
            throw new Exception($"Failed to set shader pass {pass.Name}. No variant found for the current keyword state.");

        Device.SetState(pass.State);

        PropertyState.Apply(mat._properties, variant);

        mesh.Upload();

        unsafe
        {
            Device.BindVertexArray(mesh.VertexArrayObject);
            Device.DrawIndexed(mesh.MeshTopology, (uint)mesh.IndexCount, mesh.IndexFormat == IndexFormat.UInt32, null);
            Device.BindVertexArray(null);
        }
    }

    // ============================================================================
    // QUEUED RENDERING API - Unity-style Graphics.DrawMesh/DrawMeshInstanced
    // ============================================================================

    /// <summary>
    /// Queues a single mesh to be rendered by pushing it to the scene's render queue.
    /// The mesh will be rendered during the next frame with the specified material and transform.
    /// </summary>
    /// <param name="scene">Scene to push the renderable to</param>
    /// <param name="mesh">Mesh to render</param>
    /// <param name="transform">World transform matrix</param>
    /// <param name="material">Material to render with</param>
    /// <param name="layer">Layer index for culling and sorting (default: 0)</param>
    /// <param name="properties">Optional per-object property overrides</param>
    public static void DrawMesh(Scene scene, Mesh mesh, Double4x4 transform, Material material, int layer = 0, PropertyState? properties = null)
    {
        if (scene == null || mesh == null || material == null) return;

        var renderable = new MeshRenderable(mesh, material, transform, layer, properties);
        scene.PushRenderable(renderable);
    }

    /// <summary>
    /// Queues multiple instances of a mesh to be rendered with GPU instancing.
    /// Automatically handles batching for large instance counts (>1023 instances).
    /// </summary>
    /// <param name="scene">Scene to push the renderable to</param>
    /// <param name="mesh">Mesh to render</param>
    /// <param name="transforms">Array of world transforms (one per instance)</param>
    /// <param name="material">Material to render with</param>
    /// <param name="layer">Layer index for culling and sorting (default: 0)</param>
    /// <param name="properties">Optional shared properties for all instances</param>
    /// <param name="maxBatchSize">Maximum instances per batch (default: 1023)</param>
    public static void DrawMeshInstanced(Scene scene, Mesh mesh, Float4x4[] transforms, Material material, int layer = 0, PropertyState? properties = null, int maxBatchSize = 1023)
    {
        if (scene == null || mesh == null || material == null || transforms == null || transforms.Length == 0) return;

        // Automatic batching for >1023 instances by default
        int remainingInstances = transforms.Length;
        int offset = 0;

        while (remainingInstances > 0)
        {
            int batchSize = System.Math.Min(remainingInstances, maxBatchSize);

            // Create instance data for this batch
            var instanceData = new Rendering.InstanceData[batchSize];
            for (int i = 0; i < batchSize; i++)
            {
                instanceData[i] = new Rendering.InstanceData(transforms[offset + i]);
            }

            // Push batch to scene
            var renderable = new Rendering.InstancedMeshRenderable(mesh, material, instanceData, layer, properties);
            scene.PushRenderable(renderable);

            remainingInstances -= batchSize;
            offset += batchSize;
        }
    }

    /// <summary>
    /// Queues multiple instances with per-instance colors.
    /// Automatically handles batching for large instance counts (>1023 instances).
    /// </summary>
    public static void DrawMeshInstanced(Scene scene, Mesh mesh, Float4x4[] transforms, Material material, Float4[] colors, int layer = 0, PropertyState? properties = null, int maxBatchSize = 1023)
    {
        if (scene == null || mesh == null || material == null || transforms == null || transforms.Length == 0) return;

        // Automatic batching for >1023 instances by default
        int remainingInstances = transforms.Length;
        int offset = 0;

        while (remainingInstances > 0)
        {
            int batchSize = System.Math.Min(remainingInstances, maxBatchSize);

            // Create instance data for this batch with colors
            var instanceData = new Rendering.InstanceData[batchSize];
            for (int i = 0; i < batchSize; i++)
            {
                int idx = offset + i;
                Float4 color = idx < colors.Length ? colors[idx] : new Float4(1, 1, 1, 1);
                instanceData[i] = new Rendering.InstanceData(transforms[idx], color);
            }

            // Push batch to scene
            var renderable = new Rendering.InstancedMeshRenderable(mesh, material, instanceData, layer, properties);
            scene.PushRenderable(renderable);

            remainingInstances -= batchSize;
            offset += batchSize;
        }
    }

    /// <summary>
    /// Queues multiple instances with optional per-instance colors and custom data.
    /// This is the most flexible overload for custom per-instance data (UV offsets, lifetimes, etc.)
    /// Automatically handles batching for large instance counts.
    /// </summary>
    /// <param name="scene">Scene to push the renderable to</param>
    /// <param name="mesh">Mesh to render</param>
    /// <param name="transforms">Array of world transforms (one per instance)</param>
    /// <param name="material">Material to render with</param>
    /// <param name="colors">Optional per-instance colors (RGBA). If null, defaults to white.</param>
    /// <param name="customData">Optional per-instance custom data (4 floats). Useful for UV offsets, lifetimes, etc.</param>
    /// <param name="layer">Layer index for culling and sorting (default: 0)</param>
    /// <param name="properties">Optional shared properties for all instances</param>
    /// <param name="maxBatchSize">Maximum instances per batch (default: 1023)</param>
    public static void DrawMeshInstanced(
        Scene scene,
        Mesh mesh,
        Float4x4[] transforms,
        Material material,
        Float4[]? colors = null,
        Float4[]? customData = null,
        int layer = 0,
        PropertyState? properties = null,
        int maxBatchSize = 1023)
    {
        if (scene == null || mesh == null || material == null || transforms == null || transforms.Length == 0) return;

        // Automatic batching for >maxBatchSize instances
        int remainingInstances = transforms.Length;
        int offset = 0;

        while (remainingInstances > 0)
        {
            int batchSize = System.Math.Min(remainingInstances, maxBatchSize);

            // Build InstanceData from separate arrays
            var instanceData = new Rendering.InstanceData[batchSize];
            for (int i = 0; i < batchSize; i++)
            {
                int idx = offset + i;
                Float4 color = colors != null && idx < colors.Length ? colors[idx] : new Float4(1, 1, 1, 1);
                Float4 custom = customData != null && idx < customData.Length ? customData[idx] : Float4.Zero;
                instanceData[i] = new Rendering.InstanceData(transforms[idx], color, custom);
            }

            // Push batch to scene
            var renderable = new Rendering.InstancedMeshRenderable(mesh, material, instanceData, layer, properties);
            scene.PushRenderable(renderable);

            remainingInstances -= batchSize;
            offset += batchSize;
        }
    }

    /// <summary>
    /// Legacy method - kept for compatibility with existing code.
    /// Prefer using the Float4x4[] overload for cleaner API.
    /// </summary>
    public static void DrawMeshInstanced(Scene scene, Mesh mesh, Material mat, Rendering.InstanceData[] instanceData, int layer = 0, PropertyState? properties = null)
    {
        if (mesh == null || mat == null || instanceData.Length == 0) return;

        var renderable = new Rendering.InstancedMeshRenderable(mesh, mat, instanceData, layer, properties);
        scene.PushRenderable(renderable);
    }

    public static void Initialize()
    {
        Device = new GLDevice();
        Device.Initialize(true);
    }

    public static void StartFrame()
    {
        Device.UnbindFramebuffer();
        Device.Viewport(0, 0, (uint)Window.InternalWindow.FramebufferSize.X, (uint)Window.InternalWindow.FramebufferSize.Y);
        Device.SetState(new(), true);

        Device.BindVertexArray(null);
        Device.Clear(0, 0, 0, 1, ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil);

        ShadowAtlas.TryInitialize();
        ShadowAtlas.Clear();
    }

    public static void EndFrame()
    {
        RenderTexture.UpdatePool();
    }

    public static void Dispose()
    {
        Device.Dispose();
    }
}
