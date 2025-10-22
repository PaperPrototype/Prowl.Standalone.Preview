// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Prowl.Echo;
using Prowl.Runtime.GraphicsBackend;
using Prowl.Vector;

using Texture2D = Prowl.Runtime.Resources.Texture2D;

namespace Prowl.Runtime.Rendering;

public partial class PropertyState
{
    [SerializeField] private Dictionary<string, Color> _colors = [];
    [SerializeField] private Dictionary<string, Float2> _vectors2 = [];
    [SerializeField] private Dictionary<string, Float3> _vectors3 = [];
    [SerializeField] private Dictionary<string, Float4> _vectors4 = [];
    [SerializeField] private Dictionary<string, float> _floats = [];
    [SerializeField] private Dictionary<string, int> _ints = [];
    [SerializeField] private Dictionary<string, Float4x4> _matrices = [];
    [SerializeField] private Dictionary<string, Float4x4[]> _matrixArr = [];
    [SerializeField] private Dictionary<string, Texture2D> _textures = [];
    [SerializeField] private Dictionary<string, GraphicsBuffer> _buffers = [];
    [SerializeField] private Dictionary<string, uint> _bufferBindings = [];

    //private Dictionary<string, int> textureSlots = new();

    public PropertyState() { }

    public PropertyState(PropertyState clone)
    {
        _colors = new(clone._colors);
        _vectors2 = new(clone._vectors2);
        _vectors3 = new(clone._vectors3);
        _vectors4 = new(clone._vectors4);
        _floats = new(clone._floats);
        _ints = new(clone._ints);
        _matrices = new(clone._matrices);
        _matrixArr = new(clone._matrixArr);
        _textures = new(clone._textures);
        _buffers = new(clone._buffers);
    }

    public bool IsEmpty => _colors.Count == 0 && _vectors4.Count == 0 && _vectors3.Count == 0 && _vectors2.Count == 0 && _floats.Count == 0 && _ints.Count == 0 && _matrices.Count == 0 && _textures.Count == 0;

    /// <summary>
    /// Computes a hash representing the current state of all properties.
    /// Used for material batching to group objects with identical material properties.
    /// </summary>
    public ulong ComputeHash()
    {
        ulong hash = 14695981039346656037UL; // FNV offset basis

        // Hash all property dictionaries
        foreach (var kvp in _floats.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL; // FNV prime
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _ints.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _vectors2.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _vectors3.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _vectors4.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _colors.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _matrices.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            hash ^= (ulong)kvp.Value.GetHashCode();
            hash *= 1099511628211UL;
        }

        foreach (var kvp in _textures.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            if (kvp.Value != null)
            {
                hash ^= (ulong)kvp.Value.GetHashCode();
                hash *= 1099511628211UL;
            }
        }

        foreach (var kvp in _buffers.OrderBy(x => x.Key))
        {
            hash ^= (ulong)kvp.Key.GetHashCode();
            hash *= 1099511628211UL;
            if (kvp.Value != null)
            {
                hash ^= (ulong)kvp.Value.GetHashCode();
                hash *= 1099511628211UL;
            }
        }

        return hash;
    }

    // Setters
    public void SetColor(string name, Color value) => _colors[name] = value;
    public void SetVector(string name, Double2 value) => _vectors2[name] = (Float2)value;
    public void SetVector(string name, Double3 value) => _vectors3[name] = (Float3)value;
    public void SetVector(string name, Double4 value) => _vectors4[name] = (Float4)value;
    public void SetFloat(string name, float value) => _floats[name] = value;
    public void SetInt(string name, int value) => _ints[name] = value;
    public void SetMatrix(string name, Double4x4 value) => _matrices[name] = (Float4x4)value;
    public void SetMatrices(string name, Double4x4[] value) => _matrixArr[name] = [.. value.Select(x => (Float4x4)x)];
    public void SetTexture(string name, Texture2D value) => _textures[name] = value;
    public void SetBuffer(string name, GraphicsBuffer value, uint bindingPoint = 0)
    {
        _buffers[name] = value;
        _bufferBindings[name] = bindingPoint;
    }

    // Getters
    public Color GetColor(string name) => _colors.TryGetValue(name, out Color value) ? value : Color.White;
    public Double2 GetVector2(string name) => _vectors2.TryGetValue(name, out Float2 value) ? value : Double2.Zero;
    public Double3 GetVector3(string name) => _vectors3.TryGetValue(name, out Float3 value) ? value : Double3.Zero;
    public Double4 GetVector4(string name) => _vectors4.TryGetValue(name, out Float4 value) ? value : Double4.Zero;
    public float GetFloat(string name) => _floats.TryGetValue(name, out float value) ? value : 0;
    public int GetInt(string name) => _ints.TryGetValue(name, out int value) ? value : 0;
    public Double4x4 GetMatrix(string name) => _matrices.TryGetValue(name, out Float4x4 value) ? (Double4x4)value : Double4x4.Identity;
    public Texture2D? GetTexture(string name) => _textures.TryGetValue(name, out Texture2D value) ? value : null;
    public GraphicsBuffer GetBuffer(string name) => _buffers.TryGetValue(name, out GraphicsBuffer value) ? value : null;
    public uint GetBufferBinding(string name) => _bufferBindings.TryGetValue(name, out uint value) ? value : 0;


    public void Clear()
    {
        _textures.Clear();
        _matrices.Clear();
        _matrixArr.Clear();
        _ints.Clear();
        _floats.Clear();
        _vectors2.Clear();
        _vectors3.Clear();
        _vectors4.Clear();
        _colors.Clear();
        _buffers.Clear();
        _bufferBindings.Clear();
    }

    public void ApplyOverride(PropertyState properties)
    {
        foreach (KeyValuePair<string, Color> item in properties._colors)
            _colors[item.Key] = item.Value;
        foreach (KeyValuePair<string, Float2> item in properties._vectors2)
            _vectors2[item.Key] = item.Value;
        foreach (KeyValuePair<string, Float3> item in properties._vectors3)
            _vectors3[item.Key] = item.Value;
        foreach (KeyValuePair<string, Float4> item in properties._vectors4)
            _vectors4[item.Key] = item.Value;
        foreach (KeyValuePair<string, float> item in properties._floats)
            _floats[item.Key] = item.Value;
        foreach (KeyValuePair<string, int> item in properties._ints)
            _ints[item.Key] = item.Value;
        foreach (KeyValuePair<string, Float4x4> item in properties._matrices)
            _matrices[item.Key] = item.Value;
        foreach (KeyValuePair<string, Float4x4[]> item in properties._matrixArr)
            _matrixArr[item.Key] = item.Value;
        foreach (KeyValuePair<string, Texture2D> item in properties._textures)
            _textures[item.Key] = item.Value;
        foreach (KeyValuePair<string, GraphicsBuffer> item in properties._buffers)
            _buffers[item.Key] = item.Value;
        foreach (KeyValuePair<string, uint> item in properties._bufferBindings)
            _bufferBindings[item.Key] = item.Value;
    }

    /// <summary>
    /// Applies material uniforms to the shader. Should be called once per material batch.
    /// Does not bind global uniforms - call this after globals are already bound.
    /// </summary>
    public static unsafe void ApplyMaterialUniforms(PropertyState materialProperties, GraphicsProgram shader, ref int texSlot)
    {
        GraphicsProgram.UniformCache cache = shader.uniformCache;

        // Apply material properties
        foreach (KeyValuePair<string, float> item in materialProperties._floats)
        {
            if (!cache.floats.TryGetValue(item.Key, out float cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformF(shader, item.Key, item.Value);
                cache.floats[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, int> item in materialProperties._ints)
        {
            if (!cache.ints.TryGetValue(item.Key, out int cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformI(shader, item.Key, item.Value);
                cache.ints[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float2> item in materialProperties._vectors2)
        {
            if (!cache.vectors2.TryGetValue(item.Key, out Float2 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV2(shader, item.Key, item.Value);
                cache.vectors2[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float3> item in materialProperties._vectors3)
        {
            if (!cache.vectors3.TryGetValue(item.Key, out Float3 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV3(shader, item.Key, item.Value);
                cache.vectors3[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float4> item in materialProperties._vectors4)
        {
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, item.Value);
                cache.vectors4[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Color> item in materialProperties._colors)
        {
            Float4 colorVec = new((float)item.Value.R, (float)item.Value.G, (float)item.Value.B, (float)item.Value.A);
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(colorVec))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, colorVec);
                cache.vectors4[item.Key] = colorVec;
            }
        }

        foreach (KeyValuePair<string, Float4x4> item in materialProperties._matrices)
        {
            if (!cache.matrices.TryGetValue(item.Key, out Float4x4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformMatrix(shader, item.Key, false, item.Value);
                cache.matrices[item.Key] = item.Value;
            }
        }

        // Matrix arrays - always set (comparison would be expensive)
        foreach (KeyValuePair<string, Float4x4[]> item in materialProperties._matrixArr)
            Graphics.Device.SetUniformMatrix(shader, item.Key, (uint)item.Value.Length, false, in item.Value[0].c0.X);

        // Bind uniform buffers - check if buffer changed
        foreach (KeyValuePair<string, GraphicsBuffer> item in materialProperties._buffers)
        {
            if (!cache.buffers.TryGetValue(item.Key, out GraphicsBuffer? cachedBuffer) || cachedBuffer != item.Value)
            {
                Graphics.Device.BindUniformBuffer(shader, item.Key, item.Value);
                cache.buffers[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Texture2D> item in materialProperties._textures)
        {
            Texture2D tex = item.Value;
            if (tex.IsValid())
            {
                // Always set textures - slot assignment must be consistent
                Graphics.Device.SetUniformTexture(shader, item.Key, texSlot, tex.Handle);
                texSlot++;
            }
        }
    }

    /// <summary>
    /// Applies instance uniforms to the shader. Should be called once per draw call.
    /// Does not bind global or material uniforms - call this after material uniforms are already bound.
    /// </summary>
    public static unsafe void ApplyInstanceUniforms(PropertyState instanceProperties, GraphicsProgram shader, ref int texSlot)
    {
        GraphicsProgram.UniformCache cache = shader.uniformCache;

        // Apply instance properties (can override material properties)
        foreach (KeyValuePair<string, float> item in instanceProperties._floats)
        {
            if (!cache.floats.TryGetValue(item.Key, out float cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformF(shader, item.Key, item.Value);
                cache.floats[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, int> item in instanceProperties._ints)
        {
            if (!cache.ints.TryGetValue(item.Key, out int cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformI(shader, item.Key, item.Value);
                cache.ints[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float2> item in instanceProperties._vectors2)
        {
            if (!cache.vectors2.TryGetValue(item.Key, out Float2 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV2(shader, item.Key, item.Value);
                cache.vectors2[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float3> item in instanceProperties._vectors3)
        {
            if (!cache.vectors3.TryGetValue(item.Key, out Float3 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV3(shader, item.Key, item.Value);
                cache.vectors3[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float4> item in instanceProperties._vectors4)
        {
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, item.Value);
                cache.vectors4[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Color> item in instanceProperties._colors)
        {
            Float4 colorVec = new((float)item.Value.R, (float)item.Value.G, (float)item.Value.B, (float)item.Value.A);
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(colorVec))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, colorVec);
                cache.vectors4[item.Key] = colorVec;
            }
        }

        foreach (KeyValuePair<string, Float4x4> item in instanceProperties._matrices)
        {
            if (!cache.matrices.TryGetValue(item.Key, out Float4x4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformMatrix(shader, item.Key, false, item.Value);
                cache.matrices[item.Key] = item.Value;
            }
        }

        // Matrix arrays - always set (comparison would be expensive)
        foreach (KeyValuePair<string, Float4x4[]> item in instanceProperties._matrixArr)
            Graphics.Device.SetUniformMatrix(shader, item.Key, (uint)item.Value.Length, false, in item.Value[0].c0.X);

        // Bind uniform buffers - check if buffer changed
        foreach (KeyValuePair<string, GraphicsBuffer> item in instanceProperties._buffers)
        {
            if (!cache.buffers.TryGetValue(item.Key, out GraphicsBuffer? cachedBuffer) || cachedBuffer != item.Value)
            {
                Graphics.Device.BindUniformBuffer(shader, item.Key, item.Value);
                cache.buffers[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Texture2D> item in instanceProperties._textures)
        {
            Texture2D tex = item.Value;
            if (tex.IsValid())
            {
                // Always set textures - slot assignment must be consistent
                Graphics.Device.SetUniformTexture(shader, item.Key, texSlot, tex.Handle);
                texSlot++;
            }
        }
    }

    public static unsafe void Apply(PropertyState mpb, GraphicsProgram shader)
    {
        GraphicsProgram.UniformCache cache = shader.uniformCache;
        int texSlot = 0;

        // Bind the global uniform buffer first
        GraphicsBuffer globalBuffer = GlobalUniforms.GetBuffer();
        if (globalBuffer != null)
        {
            Graphics.Device.BindUniformBuffer(shader, "GlobalUniforms", globalBuffer, 0);
        }

        // Apply global properties first (so instance properties can override them)
        ApplyGlobals(shader, cache, ref texSlot);

        // Then apply instance properties
        foreach (KeyValuePair<string, float> item in mpb._floats)
        {
            if (!cache.floats.TryGetValue(item.Key, out float cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformF(shader, item.Key, item.Value);
                cache.floats[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, int> item in mpb._ints)
        {
            if (!cache.ints.TryGetValue(item.Key, out int cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformI(shader, item.Key, item.Value);
                cache.ints[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float2> item in mpb._vectors2)
        {
            if (!cache.vectors2.TryGetValue(item.Key, out Float2 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV2(shader, item.Key, item.Value);
                cache.vectors2[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float3> item in mpb._vectors3)
        {
            if (!cache.vectors3.TryGetValue(item.Key, out Float3 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV3(shader, item.Key, item.Value);
                cache.vectors3[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Float4> item in mpb._vectors4)
        {
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, item.Value);
                cache.vectors4[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Color> item in mpb._colors)
        {
            Float4 colorVec = new((float)item.Value.R, (float)item.Value.G, (float)item.Value.B, (float)item.Value.A);
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, colorVec);
                cache.vectors4[item.Key] = colorVec;
            }
        }

        foreach (KeyValuePair<string, Float4x4> item in mpb._matrices)
        {
            if (!cache.matrices.TryGetValue(item.Key, out Float4x4 cachedValue) || !cachedValue.Equals(item.Value))
            {
                Graphics.Device.SetUniformMatrix(shader, item.Key, false, item.Value);
                cache.matrices[item.Key] = item.Value;
            }
        }

        // Matrix arrays - always set (comparison would be expensive)
        foreach (KeyValuePair<string, Float4x4[]> item in mpb._matrixArr)
            Graphics.Device.SetUniformMatrix(shader, item.Key, (uint)item.Value.Length, false, in item.Value[0].c0.X);

        // Bind uniform buffers - check if buffer changed
        foreach (KeyValuePair<string, GraphicsBuffer> item in mpb._buffers)
        {
            if (!cache.buffers.TryGetValue(item.Key, out GraphicsBuffer? cachedBuffer) || cachedBuffer != item.Value)
            {
                Graphics.Device.BindUniformBuffer(shader, item.Key, item.Value);
                cache.buffers[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Texture2D> item in mpb._textures)
        {
            Texture2D tex = item.Value;
            if (tex.IsValid())
            {
                // Always set textures - slot assignment must be consistent
                Graphics.Device.SetUniformTexture(shader, item.Key, texSlot, tex.Handle);
                texSlot++;
            }
        }
    }

    internal static void ApplyGlobals(GraphicsProgram shader, GraphicsProgram.UniformCache cache, ref int texSlot)
    {
        foreach (KeyValuePair<string, float> item in s_globalFloats)
        {
            if (!cache.floats.TryGetValue(item.Key, out float cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformF(shader, item.Key, item.Value);
                cache.floats[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, int> item in s_globalInts)
        {
            if (!cache.ints.TryGetValue(item.Key, out int cachedValue) || cachedValue != item.Value)
            {
                Graphics.Device.SetUniformI(shader, item.Key, item.Value);
                cache.ints[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Double2> item in s_globalVectors2)
        {
            Float2 value = (Float2)item.Value;
            if (!cache.vectors2.TryGetValue(item.Key, out Float2 cachedValue) || !cachedValue.Equals(value))
            {
                Graphics.Device.SetUniformV2(shader, item.Key, value);
                cache.vectors2[item.Key] = value;
            }
        }

        foreach (KeyValuePair<string, Double3> item in s_globalVectors3)
        {
            Float3 value = (Float3)item.Value;
            if (!cache.vectors3.TryGetValue(item.Key, out Float3 cachedValue) || !cachedValue.Equals(value))
            {
                Graphics.Device.SetUniformV3(shader, item.Key, value);
                cache.vectors3[item.Key] = value;
            }
        }

        foreach (KeyValuePair<string, Double4> item in s_globalVectors4)
        {
            Float4 value = (Float4)item.Value;
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(value))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, value);
                cache.vectors4[item.Key] = value;
            }
        }

        foreach (KeyValuePair<string, Color> item in s_globalColors)
        {
            Float4 colorVec = new((float)item.Value.R, (float)item.Value.G, (float)item.Value.B, (float)item.Value.A);
            if (!cache.vectors4.TryGetValue(item.Key, out Float4 cachedValue) || !cachedValue.Equals(colorVec))
            {
                Graphics.Device.SetUniformV4(shader, item.Key, colorVec);
                cache.vectors4[item.Key] = colorVec;
            }
        }

        foreach (KeyValuePair<string, Double4x4> item in s_globalMatrices)
        {
            Float4x4 value = (Float4x4)item.Value;
            if (!cache.matrices.TryGetValue(item.Key, out Float4x4 cachedValue) || !cachedValue.Equals(value))
            {
                Graphics.Device.SetUniformMatrix(shader, item.Key, false, value);
                cache.matrices[item.Key] = value;
            }
        }

        // Matrix arrays - always set (comparison would be expensive)
        foreach (KeyValuePair<string, System.Numerics.Matrix4x4[]> item in s_globalMatrixArr)
            Graphics.Device.SetUniformMatrix(shader, item.Key, (uint)item.Value.Length, false, in item.Value[0].M11);

        // Bind global uniform buffers - check if buffer changed
        foreach (KeyValuePair<string, GraphicsBuffer> item in s_globalBuffers)
        {
            if (!cache.buffers.TryGetValue(item.Key, out GraphicsBuffer? cachedBuffer) || cachedBuffer != item.Value)
            {
                Graphics.Device.BindUniformBuffer(shader, item.Key, item.Value);
                cache.buffers[item.Key] = item.Value;
            }
        }

        foreach (KeyValuePair<string, Texture2D> item in s_globalTextures)
        {
            Texture2D tex = item.Value;
            if (tex.IsValid())
            {
                // Always set textures - slot assignment must be consistent
                Graphics.Device.SetUniformTexture(shader, item.Key, texSlot, tex.Handle);
                texSlot++;
            }
        }
    }
}

public partial class PropertyState
{
    // Global static dictionaries
    private static Dictionary<string, Color> s_globalColors = [];
    private static Dictionary<string, Double2> s_globalVectors2 = [];
    private static Dictionary<string, Double3> s_globalVectors3 = [];
    private static Dictionary<string, Double4> s_globalVectors4 = [];
    private static Dictionary<string, float> s_globalFloats = [];
    private static Dictionary<string, int> s_globalInts = [];
    private static Dictionary<string, Double4x4> s_globalMatrices = [];
    private static Dictionary<string, System.Numerics.Matrix4x4[]> s_globalMatrixArr = [];
    private static Dictionary<string, Texture2D> s_globalTextures = [];
    private static Dictionary<string, GraphicsBuffer> s_globalBuffers = [];
    private static Dictionary<string, uint> s_globalBufferBindings = [];

    // Global setters
    public static void SetGlobalColor(string name, Color value) => s_globalColors[name] = value;
    public static void SetGlobalVector(string name, Double2 value) => s_globalVectors2[name] = value;
    public static void SetGlobalVector(string name, Double3 value) => s_globalVectors3[name] = value;
    public static void SetGlobalVector(string name, Double4 value) => s_globalVectors4[name] = value;
    public static void SetGlobalFloat(string name, double value) => s_globalFloats[name] = (float)value;
    public static void SetGlobalInt(string name, int value) => s_globalInts[name] = value;
    public static void SetGlobalMatrix(string name, Double4x4 value) => s_globalMatrices[name] = value;
    public static void SetGlobalMatrices(string name, Double4x4[] value) => s_globalMatrixArr[name] = [.. value.Select(x => (System.Numerics.Matrix4x4)(Float4x4)x)];
    public static void SetGlobalTexture(string name, Texture2D value) => s_globalTextures[name] = value;
    public static void SetGlobalBuffer(string name, GraphicsBuffer value, uint bindingPoint = 0)
    {
        s_globalBuffers[name] = value;
        s_globalBufferBindings[name] = bindingPoint;
    }

    // Global getters
    public static Color GetGlobalColor(string name) => s_globalColors.TryGetValue(name, out Color value) ? value : Color.White;
    public static Double2 GetGlobalVector2(string name) => s_globalVectors2.TryGetValue(name, out Double2 value) ? value : Double2.Zero;
    public static Double3 GetGlobalVector3(string name) => s_globalVectors3.TryGetValue(name, out Double3 value) ? value : Double3.Zero;
    public static Double4 GetGlobalVector4(string name) => s_globalVectors4.TryGetValue(name, out Double4 value) ? value : Double4.Zero;
    public static float GetGlobalFloat(string name) => s_globalFloats.TryGetValue(name, out float value) ? value : 0;
    public static int GetGlobalInt(string name) => s_globalInts.TryGetValue(name, out int value) ? value : 0;
    public static Double4x4 GetGlobalMatrix(string name) => s_globalMatrices.TryGetValue(name, out Double4x4 value) ? value : Double4x4.Identity;
    public static Texture2D? GetGlobalTexture(string name) => s_globalTextures.TryGetValue(name, out Texture2D value) ? value : null;
    public static GraphicsBuffer GetGlobalBuffer(string name) => s_globalBuffers.TryGetValue(name, out GraphicsBuffer value) ? value : null;
    public static uint GetGlobalBufferBinding(string name) => s_globalBufferBindings.TryGetValue(name, out uint value) ? value : 0;

    public static void ClearGlobals()
    {
        s_globalTextures.Clear();
        s_globalMatrices.Clear();
        s_globalInts.Clear();
        s_globalFloats.Clear();
        s_globalVectors2.Clear();
        s_globalVectors3.Clear();
        s_globalVectors4.Clear();
        s_globalColors.Clear();
        s_globalMatrixArr.Clear();
        s_globalBuffers.Clear();
        s_globalBufferBindings.Clear();
    }
}
