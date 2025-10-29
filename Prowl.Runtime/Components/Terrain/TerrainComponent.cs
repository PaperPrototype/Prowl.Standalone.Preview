// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime.Terrain;

/// <summary>
/// GPU-instanced terrain component with quadtree LOD.
/// Renders terrain using a single 32x32 mesh instanced many times.
/// Heightmap is sampled in the vertex shader for displacement.
/// </summary>
public class TerrainComponent : MonoBehaviour, IInstancedRenderable
{
    #region Configuration

    public Material Material;
    public Camera TargetCamera;            // Camera for LOD calculations (if null, uses first camera in scene)
    public Texture Heightmap;
    public Texture Splatmap;

    // Terrain textures (4 layers)
    public Texture Layer0Albedo;
    public Texture Layer1Albedo;
    public Texture Layer2Albedo;
    public Texture Layer3Albedo;

    public double TerrainSize = 1024.0;    // World size of terrain
    public float TerrainHeight = 100.0f;   // Maximum height
    public int MaxLODLevel = 4;            // Maximum LOD subdivision levels
    public int MeshResolution = 16;        // Resolution of base mesh (32x32)
    public float TextureTiling = 10.0f;    // Tiling for terrain textures

    #endregion

    #region State

    private TerrainQuadtree _quadtree;
    private Mesh _baseMesh;
    private InstanceData[] _instanceDataCache = Array.Empty<InstanceData>();
    private PropertyState _properties = new();
    private InstancedRenderingHelper _instancedHelper = new();

    #endregion

    #region Lifecycle

    public override void OnEnable()
    {
        base.OnEnable();
        if (_baseMesh == null)
            CreateBaseMesh();
        if (_quadtree == null)
            _quadtree = new TerrainQuadtree(Double3.Zero, TerrainSize, MaxLODLevel);
    }

    public override void Update()
    {
        // Get camera position from target camera or first camera in scene
        Camera camera = TargetCamera;
        if (camera == null || !camera.Enabled)
        {
            // Try to get first active camera from scene
            camera = GameObject.Scene?.ActiveObjects
                .SelectMany(x => x.GetComponentsInChildren<Camera>())
                .FirstOrDefault();
        }

        if (camera == null)
            return;

        Double3 cameraPos = camera.Transform.Position - this.Transform.Position;
        // Project camera position onto terrain plane
        cameraPos.Y = 0;

        // Update quadtree with camera position
        _quadtree.Update(cameraPos);

        // Generate instance data for visible chunks
        UpdateInstanceData();

        // Push to render queue
        if (_instanceDataCache.Length > 0 && Material.IsValid())
        {
            _properties.Clear();
            _properties.SetInt("_ObjectID", InstanceID);

            // Set terrain textures (cast to Texture2D)
            if (Heightmap.IsValid() && Heightmap is Texture2D heightmap2D)
                _properties.SetTexture("_Heightmap", heightmap2D);
            if (Splatmap.IsValid() && Splatmap is Texture2D splatmap2D)
                _properties.SetTexture("_Splatmap", splatmap2D);
            if (Layer0Albedo.IsValid() && Layer0Albedo is Texture2D layer0)
                _properties.SetTexture("_Layer0", layer0);
            if (Layer1Albedo.IsValid() && Layer1Albedo is Texture2D layer1)
                _properties.SetTexture("_Layer1", layer1);
            if (Layer2Albedo.IsValid() && Layer2Albedo is Texture2D layer2)
                _properties.SetTexture("_Layer2", layer2);
            if (Layer3Albedo.IsValid() && Layer3Albedo is Texture2D layer3)
                _properties.SetTexture("_Layer3", layer3);

            _properties.SetFloat("_TerrainSize", (float)TerrainSize);
            _properties.SetFloat("_TerrainHeight", TerrainHeight);
            _properties.SetFloat("_TextureTiling", TextureTiling);

            _properties.SetVector("_TerrainOffset", this.Transform.Position);

            GameObject.Scene.PushRenderable(this);
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _baseMesh?.Dispose();
        _baseMesh = null;
        _instancedHelper?.Dispose();
    }

    public override void DrawGizmos()
    {
        _quadtree.DrawGizmos(this.Transform.Position);
    }

    #endregion

    #region IInstancedRenderable

    public Material GetMaterial() => Material;
    public int GetLayer() => GameObject.LayerIndex;

    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh drawData, out Double4x4 model)
    {
        // Fallback for non-instanced rendering (shouldn't be called for IInstancedRenderable)
        properties = _properties;
        drawData = _baseMesh;
        model = Transform.LocalToWorldMatrix;
    }

    public void GetCullingData(out bool isRenderable, out AABB bounds)
    {
        isRenderable = _instanceDataCache.Length > 0 && Material.IsValid() && _baseMesh != null;

        // Terrain bounds
        bounds = new AABB(new Double3(double.MinValue), new Double3(double.MaxValue));
    }

    public void GetInstanceData(ViewerData viewer, out PropertyState properties, out Mesh mesh, out InstanceData[] instanceData)
    {
        properties = _properties;
        mesh = _baseMesh;
        instanceData = _instanceDataCache;
    }

    #endregion

    #region Mesh Creation

    private void CreateBaseMesh()
    {
        int resolution = MeshResolution;
        int vertexCount = (resolution + 1) * (resolution + 1);
        int indexCount = resolution * resolution * 6;

        Float3[] vertices = new Float3[vertexCount];
        Float2[] uvs = new Float2[vertexCount];
        uint[] indices = new uint[indexCount];

        // Generate vertices and UVs for a unit quad (0 to 1 in XZ)
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = z * (resolution + 1) + x;

                // Position in 0-1 range
                float u = x / (float)resolution;
                float v = z / (float)resolution;

                vertices[index] = new Float3(u, 0, v);
                uvs[index] = new Float2(u, v);
            }
        }

        // Generate indices
        int triIndex = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int vertIndex = z * (resolution + 1) + x;

                // Triangle 1
                indices[triIndex++] = (uint)(vertIndex);
                indices[triIndex++] = (uint)(vertIndex + resolution + 1);
                indices[triIndex++] = (uint)(vertIndex + 1);

                // Triangle 2
                indices[triIndex++] = (uint)(vertIndex + 1);
                indices[triIndex++] = (uint)(vertIndex + resolution + 1);
                indices[triIndex++] = (uint)(vertIndex + resolution + 2);
            }
        }

        _baseMesh = new Mesh();
        _baseMesh.Vertices = vertices;
        _baseMesh.UV = uvs;
        _baseMesh.Indices = indices;
        _baseMesh.RecalculateBounds();
        _baseMesh.Upload();
    }

    #endregion

    #region Instance Data

    private void UpdateInstanceData()
    {
        var visibleChunks = _quadtree.GetVisibleChunks();

        if (_instanceDataCache.Length != visibleChunks.Count)
        {
            _instanceDataCache = new InstanceData[visibleChunks.Count];
        }

        for (int i = 0; i < visibleChunks.Count; i++)
        {
            var chunk = visibleChunks[i];

            // Calculate transform for this chunk
            // Position: chunk position in world space (relative to terrain GameObject)
            // Scale: chunk size
            Float3 position = (Float3)(this.Transform.Position + chunk.Position);
            float scale = (float)chunk.Size;

            // Create transform matrix: Translation * Scale
            // The position is already in world space relative to terrain origin
            Float4x4 transform = Float4x4.CreateTranslation(position) * Float4x4.CreateScale(scale, 1.0f, scale);

            // CustomData: X = LOD level, Y/Z/W unused for now
            Float4 customData = new Float4(chunk.LODLevel, 0, 0, 0);

            _instanceDataCache[i] = new InstanceData(transform, new Float4(1, 1, 1, 1), customData);
        }
    }

    #endregion
}
