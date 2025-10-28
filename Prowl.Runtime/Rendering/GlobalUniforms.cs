// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Runtime.InteropServices;

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Vector;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Structure matching the layout of global uniforms in ShaderVariables.glsl
/// Uses std140 layout for uniform buffer compatibility
/// Contains only per-frame data that is constant across all draw calls
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct GlobalUniformsData
{
    // Suppress IDE0130 warning about naming rule violation, as these are meant to match the naming of the Shader code
#pragma warning disable IDE1006 // Naming Styles

    // Camera matrices (each mat4 = 64 bytes)
    public Float4x4 prowl_MatV;               // 64 bytes
    public Float4x4 prowl_MatIV;              // 64 bytes
    public Float4x4 prowl_MatP;               // 64 bytes
    public Float4x4 prowl_MatVP;              // 64 bytes
    public Float4x4 prowl_PrevViewProj;       // 64 bytes

    // Camera parameters
    public Float3 _WorldSpaceCameraPos;       // 12 bytes
    public float _padding0;                   // 4 bytes (padding)

    public Float4 _ProjectionParams;          // 16 bytes
    public Float4 _ScreenParams;              // 16 bytes
    public Float2 _CameraJitter;              // 8 bytes
    public Float2 _CameraPreviousJitter;      // 8 bytes

    // Time parameters
    public Float4 _Time;                      // 16 bytes
    public Float4 _SinTime;                   // 16 bytes
    public Float4 _CosTime;                   // 16 bytes
    public Float4 prowl_DeltaTime;            // 16 bytes

    // Fog parameters
    public Float4 prowl_FogColor;             // 16 bytes
    public Float4 prowl_FogParams;            // 16 bytes
    public Float3 prowl_FogStates;            // 12 bytes
    public float _padding1;                   // 4 bytes padding

    // Ambient light parameters
    public Float2 prowl_AmbientMode;          // 8 bytes
    public Float2 _padding2;                  // 8 bytes padding
    public Float4 prowl_AmbientColor;         // 16 bytes
    public Float4 prowl_AmbientSkyColor;      // 16 bytes
    public Float4 prowl_AmbientGroundColor;   // 16 bytes

    // Shadow parameters
    public Float2 prowl_ShadowAtlasSize;      // 8 bytes
    public Float2 _padding3;                  // 8 bytes padding

#pragma warning restore IDE1006 // Naming Styles
}

/// <summary>
/// Manages the global uniform buffer for efficient shader data upload
/// </summary>
public static class GlobalUniforms
{
    private static GraphicsBuffer? s_uniformBuffer;
    private static GlobalUniformsData s_data;
    private static bool s_isDirty = true;

    /// <summary>
    /// Initializes the global uniform buffer
    /// </summary>
    public static void Initialize()
    {
        if (s_uniformBuffer == null)
        {
            // Create a dynamic uniform buffer
            s_uniformBuffer = Graphics.Device.CreateBuffer<GlobalUniformsData>(
                BufferType.UniformBuffer,
                [s_data],
                true
            );
            s_isDirty = true;
        }
    }

    /// <summary>
    /// Updates the GPU buffer if data has changed
    /// </summary>
    public static void Upload()
    {
        Initialize();

        if (s_isDirty && s_uniformBuffer != null)
        {
            Graphics.Device.UpdateBuffer(s_uniformBuffer, 0, [s_data]);
            s_isDirty = false;
        }
    }

    /// <summary>
    /// Gets the uniform buffer for binding to shaders
    /// </summary>
    public static GraphicsBuffer GetBuffer()
    {
        Initialize();
        return s_uniformBuffer!;
    }

    /// <summary>
    /// Cleans up the global uniform buffer resources
    /// </summary>
    public static void Dispose()
    {
        s_uniformBuffer?.Dispose();
        s_uniformBuffer = null;
    }

    // Camera matrix setters (per-frame data)
    public static void SetMatrixV(Double4x4 value)
    {
        s_data.prowl_MatV = (Float4x4)value;
        s_isDirty = true;
    }

    public static void SetMatrixIV(Double4x4 value)
    {
        s_data.prowl_MatIV = (Float4x4)value;
        s_isDirty = true;
    }

    public static void SetMatrixP(Double4x4 value)
    {
        s_data.prowl_MatP = (Float4x4)value;
        s_isDirty = true;
    }

    public static void SetMatrixVP(Double4x4 value)
    {
        s_data.prowl_MatVP = (Float4x4)value;
        s_isDirty = true;
    }

    public static void SetPrevViewProj(Double4x4 value)
    {
        s_data.prowl_PrevViewProj = (Float4x4)value;
        s_isDirty = true;
    }

    // Camera parameters
    public static void SetWorldSpaceCameraPos(Double3 value)
    {
        s_data._WorldSpaceCameraPos = (Float3)value;
        s_isDirty = true;
    }

    public static void SetProjectionParams(Double4 value)
    {
        s_data._ProjectionParams = (Float4)value;
        s_isDirty = true;
    }

    public static void SetScreenParams(Double4 value)
    {
        s_data._ScreenParams = (Float4)value;
        s_isDirty = true;
    }

    public static void SetCameraJitter(Double2 value)
    {
        s_data._CameraJitter = (Float2)value;
        s_isDirty = true;
    }

    public static void SetCameraPreviousJitter(Double2 value)
    {
        s_data._CameraPreviousJitter = (Float2)value;
        s_isDirty = true;
    }

    // Time parameters
    public static void SetTime(Double4 value)
    {
        s_data._Time = (Float4)value;
        s_isDirty = true;
    }

    public static void SetSinTime(Double4 value)
    {
        s_data._SinTime = (Float4)value;
        s_isDirty = true;
    }

    public static void SetCosTime(Double4 value)
    {
        s_data._CosTime = (Float4)value;
        s_isDirty = true;
    }

    public static void SetDeltaTime(Double4 value)
    {
        s_data.prowl_DeltaTime = (Float4)value;
        s_isDirty = true;
    }

    // Fog parameters
    public static void SetFogColor(Double4 value)
    {
        s_data.prowl_FogColor = (Float4)value;
        s_isDirty = true;
    }

    public static void SetFogParams(Double4 value)
    {
        s_data.prowl_FogParams = (Float4)value;
        s_isDirty = true;
    }

    public static void SetFogStates(Float3 value)
    {
        s_data.prowl_FogStates = value;
        s_isDirty = true;
    }

    // Ambient light parameters
    public static void SetAmbientMode(Double2 value)
    {
        s_data.prowl_AmbientMode = (Float2)value;
        s_isDirty = true;
    }

    public static void SetAmbientColor(Double4 value)
    {
        s_data.prowl_AmbientColor = (Float4)value;
        s_isDirty = true;
    }

    public static void SetAmbientSkyColor(Double4 value)
    {
        s_data.prowl_AmbientSkyColor = (Float4)value;
        s_isDirty = true;
    }

    public static void SetAmbientGroundColor(Double4 value)
    {
        s_data.prowl_AmbientGroundColor = (Float4)value;
        s_isDirty = true;
    }

    // Shadow parameters
    public static void SetShadowAtlasSize(Double2 value)
    {
        s_data.prowl_ShadowAtlasSize = (Float2)value;
        s_isDirty = true;
    }

    /// <summary>
    /// Resets all data to defaults
    /// </summary>
    public static void Clear()
    {
        s_data = new GlobalUniformsData();
        s_isDirty = true;
    }
}
