// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Rendering.Shaders;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime.Rendering;

public struct RenderingData
{
    public bool DisplayGizmo;
    public Double4x4 GridMatrix;
    public Color GridColor;
    public Double3 GridSizes;
}

/// <summary>
/// Interface for all renderable objects in the scene.
/// Supports both single-instance and GPU-instanced rendering through a unified API.
/// </summary>
public interface IRenderable
{
    public Material GetMaterial();
    public int GetLayer();

    /// <summary>
    /// Gets the rendering data for this renderable.
    /// </summary>
    /// <param name="viewer">Camera viewing data for culling/LOD</param>
    /// <param name="properties">Shader properties (per-object or shared for instances)</param>
    /// <param name="mesh">Mesh to render</param>
    /// <param name="model">Model matrix (only used when instanceCount == 1)</param>
    /// <param name="instanceCount">Number of instances to render (1 = single instance, >1 = GPU instancing)</param>
    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh mesh, out Double4x4 model, out int instanceCount);

    /// <summary>
    /// Gets the instanced VAO for GPU instancing (only called when instanceCount > 1).
    /// The VAO should be configured with the mesh's vertex data and the instance buffer.
    /// </summary>
    /// <param name="viewer">Camera viewing data for culling/LOD</param>
    /// <param name="vao">The VAO configured with mesh + instance buffer</param>
    public void GetInstancedVAO(ViewerData viewer, out GraphicsVertexArray vao);

    public void GetCullingData(out bool isRenderable, out AABB bounds);
}

public enum LightType
{
    Directional,
    Spot,
    Point,
    //Area
}

public interface IRenderableLight
{
    public int GetLightID();
    public int GetLayer();
    public LightType GetLightType();
    public Double3 GetLightPosition();
    public Double3 GetLightDirection();
    public bool DoCastShadows();

    /// <summary>
    /// Renders the light's contribution to the scene.
    /// Similar to ImageEffect.OnRenderImage, lights control their own drawing.
    /// </summary>
    /// <param name="gBuffer">GBuffer containing scene geometry data</param>
    /// <param name="destination">Destination render texture to draw light contribution to</param>
    /// <param name="css">Camera snapshot containing view/projection matrices and other camera data</param>
    public void OnRenderLight(RenderTexture gBuffer, RenderTexture destination, RenderPipeline.CameraSnapshot css);
}

public abstract class RenderPipeline : EngineObject
{
    public const bool CAMERA_RELATIVE = false;

    public struct CameraSnapshot(Camera camera)
    {
        public Scene Scene = camera.Scene;

        public Double3 CameraPosition = camera.Transform.Position;
        public Double3 CameraRight = camera.Transform.Right;
        public Double3 CameraUp = camera.Transform.Up;
        public Double3 CameraForward = camera.Transform.Forward;
        public LayerMask CullingMask = camera.CullingMask;
        public CameraClearFlags ClearFlags = camera.ClearFlags;
        public double NearClipPlane = camera.NearClipPlane;
        public double FarClipPlane = camera.FarClipPlane;
        public uint PixelWidth = camera.PixelWidth;
        public uint PixelHeight = camera.PixelHeight;
        public double Aspect = camera.Aspect;
        public Double4x4 OriginView = camera.OriginViewMatrix;
        public Double4x4 View = CAMERA_RELATIVE ? camera.OriginViewMatrix : camera.ViewMatrix;
        public Double4x4 ViewInverse = (CAMERA_RELATIVE ? camera.OriginViewMatrix : camera.ViewMatrix).Invert();
        public Double4x4 Projection = camera.ProjectionMatrix;
        public Double4x4 PreviousViewProj = camera.PreviousViewProjectionMatrix;
        public Frustum WorldFrustum = Frustum.FromMatrix(camera.ProjectionMatrix * camera.ViewMatrix);
        public DepthTextureMode DepthTextureMode = camera.DepthTextureMode; // Flags, Can be None, Normals, MotionVectors
    }

    public HashSet<int> ActiveObjectIds { get => s_activeObjectIds; set => s_activeObjectIds = value; }

    private Dictionary<int, Double4x4> s_prevModelMatrices = [];
    private HashSet<int> s_activeObjectIds = [];
    private const int CLEANUP_INTERVAL_FRAMES = 120; // Clean up every 120 frames
    private int s_framesSinceLastCleanup = 0;

    private void CleanupUnusedModelMatrices()
    {
        // Increment frame counter
        s_framesSinceLastCleanup++;

        // Only perform cleanup at specified interval
        if (s_framesSinceLastCleanup < CLEANUP_INTERVAL_FRAMES)
            return;

        s_framesSinceLastCleanup = 0;

        // Remove all matrices that weren't used in this frame
        var unusedKeys = s_prevModelMatrices.Keys
            .Where(key => !ActiveObjectIds.Contains(key))
            .ToList();

        foreach (int key in unusedKeys)
            s_prevModelMatrices.Remove(key);

        // Clear the active IDs set for next frame
        ActiveObjectIds.Clear();
    }

    private void TrackModelMatrix(int objectId, Double4x4 currentModel)
    {
        // Mark this object ID as active this frame
        ActiveObjectIds.Add(objectId);

        // Store current model matrix for next frame
        if (s_prevModelMatrices.TryGetValue(objectId, out Double4x4 prevModel))
            PropertyState.SetGlobalMatrix("prowl_PrevObjectToWorld", prevModel);
        else
            PropertyState.SetGlobalMatrix("prowl_PrevObjectToWorld", currentModel); // First frame, use current matrix

        s_prevModelMatrices[objectId] = currentModel;
    }

    public virtual void Render(Camera camera, in RenderingData data)
    {
        // Clean up unused matrices after rendering
        CleanupUnusedModelMatrices();
    }

    public HashSet<int> CullRenderables(IReadOnlyList<IRenderable> renderables, Frustum? worldFrustum, LayerMask cullingMask)
    {
        HashSet<int> culledRenderableIndices = [];
        for (int renderIndex = 0; renderIndex < renderables.Count; renderIndex++)
        {
            IRenderable renderable = renderables[renderIndex];

            if (worldFrustum != null && CullRenderable(renderable, worldFrustum.Value))
            {
                culledRenderableIndices.Add(renderIndex);
                continue;
            }

            if (cullingMask.HasLayer(renderable.GetLayer()) == false)
            {
                culledRenderableIndices.Add(renderIndex);
                continue;
            }
        }
        return culledRenderableIndices;
    }

    public bool CullRenderable(IRenderable renderable, Frustum cameraFrustum)
    {
        renderable.GetCullingData(out bool isRenderable, out AABB bounds);

        return !isRenderable || !cameraFrustum.Intersects(bounds);
    }

    public void SetupGlobalUniforms(CameraSnapshot css)
    {
        // Set View Rect
        //buffer.SetViewports((int)(camera.Viewrect.x * target.Width), (int)(camera.Viewrect.y * target.Height), (int)(camera.Viewrect.width * target.Width), (int)(camera.Viewrect.height * target.Height), 0, 1000);

        GlobalUniforms.SetPrevViewProj(css.PreviousViewProj);

        // Setup Default Uniforms for this frame
        // Camera
        GlobalUniforms.SetWorldSpaceCameraPos(CAMERA_RELATIVE ? Double3.Zero : css.CameraPosition);
        GlobalUniforms.SetProjectionParams(new Double4(1.0f, css.NearClipPlane, css.FarClipPlane, 1.0f / css.FarClipPlane));
        GlobalUniforms.SetScreenParams(new Double4(css.PixelWidth, css.PixelHeight, 1.0f + 1.0f / css.PixelWidth, 1.0f + 1.0f / css.PixelHeight));

        // Time
        GlobalUniforms.SetTime(new Double4(Time.TimeSinceStartup * 0.5f, Time.TimeSinceStartup, Time.TimeSinceStartup * 2, Time.FrameCount));
        GlobalUniforms.SetSinTime(new Double4(Maths.Sin(Time.TimeSinceStartup / 8), Maths.Sin(Time.TimeSinceStartup / 4), Maths.Sin(Time.TimeSinceStartup / 2), Maths.Sin(Time.TimeSinceStartup)));
        GlobalUniforms.SetCosTime(new Double4(Maths.Cos(Time.TimeSinceStartup / 8), Maths.Cos(Time.TimeSinceStartup / 4), Maths.Cos(Time.TimeSinceStartup / 2), Maths.Cos(Time.TimeSinceStartup)));
        GlobalUniforms.SetDeltaTime(new Double4(Time.DeltaTime, 1.0f / Time.DeltaTime, Time.SmoothDeltaTime, 1.0f / Time.SmoothDeltaTime));

        // Upload the global uniform buffer
        GlobalUniforms.Upload();
    }

    public void AssignCameraMatrices(Double4x4 view, Double4x4 projection)
    {
        GlobalUniforms.SetMatrixV(view);
        GlobalUniforms.SetMatrixIV(view.Invert());
        GlobalUniforms.SetMatrixP(projection);
        GlobalUniforms.SetMatrixVP(projection * view);

        // Upload the global uniform buffer
        GlobalUniforms.Upload();
    }

    /// <summary>
    /// Represents a render batch: a group of objects sharing the same material, mesh, and shader pass.
    /// Batching reduces GPU state changes by binding material uniforms once for all objects in the batch.
    /// </summary>
    private struct RenderBatch
    {
        public Material Material;      // Shared material for all objects in this batch
        public Mesh Mesh;              // Shared mesh for all objects in this batch
        public int PassIndex;          // Shader pass index
        public ulong MaterialHash;     // Hash of material uniforms (for sorting/grouping)
        public List<int> RenderableIndices;  // Indices of objects in this batch
    }

    /// <summary>
    /// Renders all given objects with optimized batching. Objects are grouped by (material, mesh, pass)
    /// to minimize GPU state changes. This achieves:
    /// - Material uniforms bound once per batch (instead of per object)
    /// - Mesh data uploaded once per batch
    /// - Shader variant selected once per batch
    /// - Per-object uniforms still bound individually
    ///
    /// Performance: 100 objects with same material = 1 material bind (vs 100 without batching)
    /// </summary>
    public void DrawRenderables(IReadOnlyList<IRenderable> renderables, string shaderTag, string tagValue, ViewerData viewer, HashSet<int> culledRenderableIndices, bool updatePreviousMatrices)
    {
        bool hasRenderOrder = !string.IsNullOrWhiteSpace(shaderTag);

        // ========== PHASE 1: Build Batches ==========
        // Group renderables by (material hash, shader pass, mesh) for efficient rendering
        List<RenderBatch> batches = new();
        Dictionary<(ulong, int, Mesh), int> batchLookup = new();

        for (int renderIndex = 0; renderIndex < renderables.Count; renderIndex++)
        {
            // Skip culled objects
            if (culledRenderableIndices?.Contains(renderIndex) ?? false)
                continue;

            IRenderable renderable = renderables[renderIndex];

            Material material = renderable.GetMaterial();
            if (material.Shader.IsNotValid()) continue;

            // Get rendering data to determine if this is instanced or single-instance rendering
            renderable.GetRenderingData(viewer, out PropertyState _, out Mesh mesh, out Double4x4 _, out int instanceCount);
            if (mesh == null || mesh.VertexCount <= 0) continue;

            // Handle instanced renderables separately (instanceCount > 1)
            if (instanceCount > 1)
            {
                DrawInstancedRenderable(renderable, shaderTag, tagValue, viewer, hasRenderOrder);
                continue;
            }

            // Get material hash for batching - materials with identical uniforms will batch together
            ulong materialHash = material.GetStateHash();

            // Find ALL shader passes matching the requested tag (e.g., "Opaque", "Transparent", "ShadowCaster")
            // Multi-pass rendering: materials can have multiple passes with the same tag (e.g., terrain with many texture layers)
            int passIndex = -1;
            foreach (ShaderPass pass in material.Shader.Passes)
            {
                passIndex++;

                if (hasRenderOrder && !pass.HasTag(shaderTag, tagValue))
                    continue;

                // Found matching pass - add to appropriate batch
                // Batch key: (material hash, pass index, mesh) ensures each pass gets its own batch
                var batchKey = (materialHash, passIndex, mesh);
                if (batchLookup.TryGetValue(batchKey, out int batchIndex))
                {
                    // Batch already exists - add this object to it
                    batches[batchIndex].RenderableIndices.Add(renderIndex);
                }
                else
                {
                    // Create new batch for this unique material+pass+mesh combination
                    RenderBatch newBatch = new()
                    {
                        Material = material,
                        Mesh = mesh,
                        PassIndex = passIndex,
                        MaterialHash = materialHash,
                        RenderableIndices = new() { renderIndex }
                    };
                    batchLookup[batchKey] = batches.Count;
                    batches.Add(newBatch);
                }

                // Continue to next pass - materials can have multiple passes with the same tag
                // They will execute in order they appear in the shader file (Pass 0 → Pass 1 → Pass 2, etc.)
            }
        }

        // ========== PHASE 2: Draw Batches ==========
        // For each batch, bind state once then draw all objects in that batch
        foreach (RenderBatch batch in batches)
        {
            Material material = batch.Material;
            Mesh mesh = batch.Mesh;
            int passIndex = batch.PassIndex;

            // Configure shader keywords based on mesh attributes (normals, UVs, skinning, etc.)
            // Since all objects in the batch share the same mesh, this is done once per batch
            material.SetKeyword("HAS_NORMALS", mesh.HasNormals);
            material.SetKeyword("HAS_TANGENTS", mesh.HasTangents);
            material.SetKeyword("HAS_UV", mesh.HasUV);
            material.SetKeyword("HAS_UV2", mesh.HasUV2);
            material.SetKeyword("HAS_COLORS", mesh.HasColors || mesh.HasColors32);
            material.SetKeyword("HAS_BONEINDICES", mesh.HasBoneIndices);
            material.SetKeyword("HAS_BONEWEIGHTS", mesh.HasBoneWeights);
            material.SetKeyword("SKINNED", mesh.HasBoneIndices && mesh.HasBoneWeights);

            // Get shader pass and compiled variant for current keyword state
            ShaderPass pass = material.Shader.GetPass(passIndex);
            if (!pass.TryGetVariantProgram(material._localKeywords, out GraphicsProgram? variantNullable) || variantNullable == null)
                continue;

            GraphicsProgram variant = variantNullable;

            // Bind GlobalUniforms buffer (contains camera matrices, time, lighting data, etc.)
            // This is done per-batch because each shader variant is a separate GPU program object,
            // and uniform buffer bindings are per-program in OpenGL.
            //
            // TODO: Could be optimized with glBindBufferBase() for global binding points (OpenGL >=4.2)
            // Researched: We're limited to OpenGL <=4.1 for macOS support, which doesn't support
            // persistent uniform buffer bindings across programs. Current approach is correct for <=4.1.
            GraphicsBuffer? globalBuffer = GlobalUniforms.GetBuffer();
            if (globalBuffer != null)
            {
                Graphics.Device.BindUniformBuffer(variant, "GlobalUniforms", globalBuffer, 0);
            }

            // Apply global properties (lighting, fog, shadow maps, etc.)
            // Must be done per-batch because different shader variants may need different globals
            GraphicsProgram.UniformCache cache = variant.uniformCache;
            int texSlot = 0;
            PropertyState.ApplyGlobals(variant, cache, ref texSlot);

            // *** BATCHING OPTIMIZATION: Bind material uniforms ONCE for entire batch ***
            // All objects in this batch share the same material state
            PropertyState.ApplyMaterialUniforms(material._properties, variant, ref texSlot);

            // Set render state (depth test, blend mode, cull mode, etc.) once per batch
            Graphics.Device.SetState(pass.State);

            // Upload mesh data to GPU once per batch (shared by all objects)
            mesh.Upload();

            // ========== PHASE 3: Draw Objects in Batch ==========
            // Material/mesh state is already bound - only per-object uniforms change
            foreach (int renderIndex in batch.RenderableIndices)
            {
                IRenderable renderable = renderables[renderIndex];

                // Get per-object data (transform, instance properties)
                // Note: mesh and instanceCount are discarded (we already have them from the batch)
                renderable.GetRenderingData(viewer, out PropertyState properties, out Mesh _, out Double4x4 model, out int _);

                // Track model matrix for motion vectors (used in temporal effects like TAA)
                int instanceId = properties.GetInt("_ObjectID");
                if (updatePreviousMatrices && instanceId != 0)
                    TrackModelMatrix(instanceId, model);

                // Camera-relative rendering: subtract camera position to improve depth precision
                if (CAMERA_RELATIVE)
                    model.Translation -= new Double4(viewer.Position, 0.0);

                // Apply instance-specific uniforms (tint colors, bone matrices, etc.)
                // Texture slot counter continues from where material textures left off
                int instanceTexSlot = texSlot;
                PropertyState.ApplyInstanceUniforms(properties, variant, ref instanceTexSlot);

                // Directly bind per-object transform uniforms after all other uniforms to gaurantee they are set correctly
                var fModel = (Float4x4)model;
                Graphics.Device.SetUniformMatrix(variant, "prowl_ObjectToWorld", false, fModel);
                Graphics.Device.SetUniformMatrix(variant, "prowl_WorldToObject", false, fModel.Invert());

                // Execute draw call (mesh VAO already uploaded, just bind and draw)
                unsafe
                {
                    Graphics.Device.BindVertexArray(mesh.VertexArrayObject);
                    Graphics.Device.DrawIndexed(mesh.MeshTopology, (uint)mesh.IndexCount, mesh.IndexFormat == IndexFormat.UInt32, null);
                    Graphics.Device.BindVertexArray(null);
                }
            }
        }
    }

    /// <summary>
    /// Draws an instanced renderable with GPU instancing.
    /// Uses DrawIndexedInstanced to draw multiple instances in a single draw call.
    /// Renderables manage their own instance buffers through the Mesh's cached VAO system.
    /// </summary>
    private void DrawInstancedRenderable(IRenderable renderable, string shaderTag, string tagValue, ViewerData viewer, bool hasRenderOrder)
    {
        // Get rendering data (mesh, properties, instance count)
        renderable.GetRenderingData(viewer, out PropertyState sharedProperties, out Mesh mesh, out Double4x4 _, out int instanceCount);

        if (mesh == null || instanceCount <= 0)
            return;

        // Get instanced VAO (cached by mesh)
        renderable.GetInstancedVAO(viewer, out GraphicsVertexArray vao);

        if (vao == null)
            return;

        Material material = renderable.GetMaterial();
        if (material.Shader.IsNotValid())
            return;

        // Get index data from mesh
        int indexCount = mesh.IndexCount;
        bool useIndex32 = mesh.IndexFormat == IndexFormat.UInt32;

        // Enable GPU instancing keyword
        material.SetKeyword("GPU_INSTANCING", true);

        // Find matching shader passes
        int passIndex = -1;
        foreach (Shaders.ShaderPass pass in material.Shader.Passes)
        {
            passIndex++;

            if (hasRenderOrder && !pass.HasTag(shaderTag, tagValue))
                continue;

            // Get shader variant
            if (!pass.TryGetVariantProgram(material._localKeywords, out GraphicsProgram? variantNullable) || variantNullable == null)
                continue;

            GraphicsProgram variant = variantNullable;

            // Bind GlobalUniforms buffer
            GraphicsBuffer? globalBuffer = GlobalUniforms.GetBuffer();
            if (globalBuffer != null)
            {
                Graphics.Device.BindUniformBuffer(variant, "GlobalUniforms", globalBuffer, 0);
            }

            // Apply global properties
            GraphicsProgram.UniformCache cache = variant.uniformCache;
            int texSlot = 0;
            PropertyState.ApplyGlobals(variant, cache, ref texSlot);

            // Apply material uniforms
            PropertyState.ApplyMaterialUniforms(material._properties, variant, ref texSlot);

            // Apply shared instance properties
            int instanceTexSlot = texSlot;
            PropertyState.ApplyInstanceUniforms(sharedProperties, variant, ref instanceTexSlot);

            // Set render state
            Graphics.Device.SetState(pass.State);

            // Draw with TRUE GPU instancing!
            unsafe
            {
                Graphics.Device.BindVertexArray(vao);
                Graphics.Device.DrawIndexedInstanced(
                    Topology.Triangles,
                    (uint)indexCount,
                    (uint)instanceCount,
                    useIndex32
                );
                Graphics.Device.BindVertexArray(null);
            }
        }

        material.SetKeyword("GPU_INSTANCING", false);
    }
}
