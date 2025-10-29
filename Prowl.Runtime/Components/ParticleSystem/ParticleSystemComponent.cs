// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Runtime.ParticleSystem.Modules;
using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime.ParticleSystem;

/// <summary>
/// A GPU-instanced particle system component.
/// Particles are rendered using instanced rendering for optimal performance.
/// </summary>
public class ParticleSystemComponent : MonoBehaviour, IInstancedRenderable
{
    #region Configuration

    public Material Material;
    public int MaxParticles = 1000;
    public float Duration = 5.0f;
    public bool Looping = true;
    public bool PlayOnEnable = true;
    public bool Prewarm = false;
    public SimulationSpace SimulationSpace = SimulationSpace.Local;

    #endregion

    #region Modules

    public InitialModule Initial = new() { Enabled = true };
    public EmissionModule Emission = new() { Enabled = true };
    public SizeOverLifetimeModule SizeOverLifetime = new();
    public ColorOverLifetimeModule ColorOverLifetime = new();
    public RotationOverLifetimeModule RotationOverLifetime = new();
    public VelocityOverLifetimeModule VelocityOverLifetime = new();
    public CollisionModule Collision = new();
    public UVModule UV = new();

    #endregion

    #region State

    private List<Particle> _particles = new();
    private Random _random = new();
    private float _time = 0;
    private bool _isPlaying = false;
    private PropertyState _properties = new();

    // GPU instancing data
    private Mesh _quadMesh;
    private InstanceData[] _instanceDataCache = Array.Empty<InstanceData>();
    private bool _instanceDataDirty = true;

    #endregion

    #region Lifecycle

    public override void OnEnable()
    {
        base.OnEnable();

        // Create quad mesh for particle rendering if not already created
        if (_quadMesh == null)
        {
            CreateQuadMesh();
        }

        if (PlayOnEnable && !_isPlaying)
        {
            Play();
        }
    }

    public override void Update()
    {
        if (!_isPlaying)
            return;

        float deltaTime = Time.DeltaTimeF;

        // Update time
        _time += deltaTime;

        // Check if we should stop (non-looping systems)
        if (!Looping && _time >= Duration)
        {
            _time = Duration;
            _isPlaying = false;
        }

        // Emit new particles
        int emitCount = Emission.CalculateEmitCount(deltaTime, _time / Duration, _random);
        for (int i = 0; i < emitCount && _particles.Count < MaxParticles; i++)
        {
            EmitParticle();
        }

        // Update existing particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];

            // Update lifetime
            particle.Lifetime -= deltaTime;
            particle.TotalTime += deltaTime;

            if (particle.Lifetime <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            // Update particle through modules
            Initial.OnParticleUpdate(ref particle, deltaTime);
            SizeOverLifetime.OnParticleUpdate(ref particle, deltaTime);
            ColorOverLifetime.OnParticleUpdate(ref particle, deltaTime);
            RotationOverLifetime.OnParticleUpdate(ref particle, deltaTime);
            VelocityOverLifetime.OnParticleUpdate(ref particle, deltaTime);
            UV.OnParticleUpdate(ref particle, deltaTime);

            // Update position
            particle.Position += particle.Velocity * deltaTime;

            _particles[i] = particle;
        }

        // Update collisions (bulk operation for efficiency)
        if (Collision.Enabled)
        {
            Collision.UpdateCollisions(_particles, GameObject.Scene?.Physics, deltaTime, Transform, SimulationSpace);
        }

        _instanceDataDirty = true;

        // Push to render queue
        if (_particles.Count > 0 && Material.IsValid())
        {
            _properties.Clear();
            _properties.SetInt("_ObjectID", InstanceID);
            GameObject.Scene.PushRenderable(this);
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Stop();

        // Clean up resources
        _quadMesh?.Dispose();
        _quadMesh = null;
    }

    #endregion

    #region Particle System Control

    public void Play()
    {
        _isPlaying = true;
        _time = 0;
        Emission.Reset();

        if (Prewarm && Looping)
        {
            PrewarmSystem();
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _time = 0;
        _particles.Clear();
        _instanceDataDirty = true;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Clear()
    {
        _particles.Clear();
        _instanceDataDirty = true;
    }

    public bool IsPlaying => _isPlaying;
    public int ParticleCount => _particles.Count;

    #endregion

    #region Particle Emission

    private void EmitParticle()
    {
        // Get spawn position and direction from emission shape
        Float3 spawnPosition = Float3.Zero;
        Float3 spawnDirection = new Float3(0, 1, 0);

        if (Emission.Enabled)
        {
            Emission.GetShapePositionAndDirection(_random, out spawnPosition, out spawnDirection);
        }

        var particle = new Particle
        {
            Position = spawnPosition,
            Velocity = spawnDirection * 1.0f, // Default velocity, will be modified by modules
            Rotation = 0,
            RotationalSpeed = 0,
            StartSize = 1,
            Size = 1,
            StartColor = Color.White,
            Color = Color.White,
            StartLifetime = 1,
            Lifetime = 1,
            RandomSeed = (uint)_random.Next(),
            UVFrame = 0,
            TotalTime = 0
        };

        // Initialize through modules
        Initial.OnParticleSpawn(ref particle, _random);
        RotationOverLifetime.OnParticleSpawn(ref particle, _random);
        UV.OnParticleSpawn(ref particle, _random);

        // Transform to world space if needed
        if (SimulationSpace == SimulationSpace.World)
        {
            particle.Position = (Float3)Transform.Position + particle.Position;
        }

        _particles.Add(particle);
    }

    private Float3 RandomDirection()
    {
        // Random direction on unit sphere
        float theta = (float)(_random.NextDouble() * Math.PI * 2);
        float phi = (float)(Math.Acos(2.0 * _random.NextDouble() - 1.0));

        return new Float3(
            (float)(Math.Sin(phi) * Math.Cos(theta)),
            (float)(Math.Sin(phi) * Math.Sin(theta)),
            (float)Math.Cos(phi)
        );
    }

    #endregion

    #region Prewarm

    private void PrewarmSystem()
    {
        if (!Looping || Duration <= 0)
            return;

        // Simulate the system for one duration cycle
        float prewarmTime = Duration;
        float step = 0.05f; // 50ms timesteps
        float elapsed = 0;

        while (elapsed < prewarmTime)
        {
            float deltaTime = Math.Min(step, prewarmTime - elapsed);

            // Emit particles
            int emitCount = Emission.CalculateEmitCount(deltaTime, elapsed / Duration, _random);
            for (int i = 0; i < emitCount && _particles.Count < MaxParticles; i++)
            {
                EmitParticle();
            }

            // Update particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];
                particle.Lifetime -= deltaTime;
                particle.TotalTime += deltaTime;

                if (particle.Lifetime <= 0)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                Initial.OnParticleUpdate(ref particle, deltaTime);
                SizeOverLifetime.OnParticleUpdate(ref particle, deltaTime);
                ColorOverLifetime.OnParticleUpdate(ref particle, deltaTime);
                RotationOverLifetime.OnParticleUpdate(ref particle, deltaTime);
                VelocityOverLifetime.OnParticleUpdate(ref particle, deltaTime);
                UV.OnParticleUpdate(ref particle, deltaTime);

                particle.Position += particle.Velocity * deltaTime;
                _particles[i] = particle;
            }

            // Update collisions during prewarm
            if (Collision.Enabled)
            {
                Collision.UpdateCollisions(_particles, GameObject.Scene?.Physics, deltaTime, Transform, SimulationSpace);
            }

            elapsed += deltaTime;
        }

        _instanceDataDirty = true;
    }

    #endregion

    #region Rendering (IInstancedRenderable)

    public Material GetMaterial() => Material;
    public int GetLayer() => GameObject.LayerIndex;

    public void GetRenderingData(ViewerData viewer, out PropertyState properties, out Mesh drawData, out Double4x4 model)
    {
        // Fallback for non-instanced rendering (shouldn't be called for IInstancedRenderable)
        properties = _properties;
        drawData = _quadMesh;
        model = Transform.LocalToWorldMatrix;
    }

    public void GetCullingData(out bool isRenderable, out AABB bounds)
    {
        isRenderable = _particles.Count > 0 && Material.IsValid() && _quadMesh != null;

        if (_particles.Count > 0)
        {
            // Calculate bounds from all particles
            Float3 min = new Float3(float.MaxValue);
            Float3 max = new Float3(float.MinValue);

            foreach (var particle in _particles)
            {
                Float3 pos = particle.Position;
                float size = particle.Size;

                // Transform to world space if in local space
                if (SimulationSpace == SimulationSpace.Local)
                {
                    var worldPos = Transform.LocalToWorldMatrix * new Double4((Double3)pos, 1.0);
                    pos = new Float3((float)worldPos.X, (float)worldPos.Y, (float)worldPos.Z);
                }

                min = new Float3(
                    Math.Min(min.X, pos.X - size),
                    Math.Min(min.Y, pos.Y - size),
                    Math.Min(min.Z, pos.Z - size)
                );

                max = new Float3(
                    Math.Max(max.X, pos.X + size),
                    Math.Max(max.Y, pos.Y + size),
                    Math.Max(max.Z, pos.Z + size)
                );
            }

            bounds = new AABB((Double3)min, (Double3)max);
        }
        else
        {
            bounds = new AABB(Double3.Zero, Double3.Zero);
        }
    }

    public void GetInstanceData(ViewerData viewer, out PropertyState properties, out Mesh mesh, out InstanceData[] instanceData)
    {
        properties = _properties;
        mesh = _quadMesh;

        // Update instance data cache if needed
        if (_instanceDataDirty || _instanceDataCache.Length != _particles.Count)
        {
            UpdateInstanceData();
        }

        instanceData = _instanceDataCache;
    }

    #endregion

    #region GPU Instancing

    private void CreateQuadMesh()
    {
        // Create a simple quad mesh for particle rendering
        // Vertices are in local space, centered at origin
        Float3[] vertices = new Float3[]
        {
            new Float3(-0.5f, -0.5f, 0),
            new Float3( 0.5f, -0.5f, 0),
            new Float3( 0.5f,  0.5f, 0),
            new Float3(-0.5f,  0.5f, 0),
        };

        Float2[] uvs = new Float2[]
        {
            new Float2(0, 0),
            new Float2(1, 0),
            new Float2(1, 1),
            new Float2(0, 1),
        };

        uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };

        _quadMesh = new Mesh();
        _quadMesh.Vertices = vertices;
        _quadMesh.UV = uvs;
        _quadMesh.Indices = indices;
        _quadMesh.RecalculateBounds();
        _quadMesh.Upload();
    }

    private void UpdateInstanceData()
    {
        // Resize cache if needed
        if (_instanceDataCache.Length != _particles.Count)
        {
            _instanceDataCache = new InstanceData[_particles.Count];
        }

        // Fill instance data from particles
        for (int i = 0; i < _particles.Count; i++)
        {
            var particle = _particles[i];

            // Create transform matrix for this particle
            Float3 position = particle.Position;
            float rotation = particle.Rotation;
            float size = particle.Size;

            // Transform to world space if in local simulation space
            if (SimulationSpace == SimulationSpace.Local)
            {
                var worldPos = Transform.LocalToWorldMatrix * new Double4((Double3)position, 1.0);
                position = new Float3((float)worldPos.X, (float)worldPos.Y, (float)worldPos.Z);
            }

            // Build transformation matrix: Translation * Rotation * Scale
            // For billboarding, we'd want to face the camera, but for now use Z-axis rotation
            Float4x4 translation = Float4x4.CreateTranslation(position);

            // Create rotation matrix around Z axis manually
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            Float4x4 rotationMat = new Float4x4(
                new Float4(cos, sin, 0, 0),
                new Float4(-sin, cos, 0, 0),
                new Float4(0, 0, 1, 0),
                new Float4(0, 0, 0, 1)
            );

            Float4x4 scale = Float4x4.CreateScale(size);
            Float4x4 transform = translation * rotationMat * scale;

            // Get UV tile info if UV module is enabled
            Float4 uvTileInfo = UV.Enabled ? UV.GetUVTileInfo(particle) : new Float4(0, 0, 1, 1);

            // Store in instance data (convert Color to Float4)
            // CustomData: X=normalized lifetime, Y=UV offsetX, Z=UV offsetY, W=UV scale
            _instanceDataCache[i] = new InstanceData(
                transform,
                InstanceData.ColorToFloat4(particle.Color),
                new Float4(particle.NormalizedLifetime, uvTileInfo.X, uvTileInfo.Y, uvTileInfo.Z) // Lifetime + UV info
            );
        }

        _instanceDataDirty = false;
    }

    #endregion
}

public enum SimulationSpace
{
    Local,
    World
}
