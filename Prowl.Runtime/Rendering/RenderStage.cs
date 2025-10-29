// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Defines the stages in the rendering pipeline where image effects can be injected.
/// </summary>
public enum RenderStage
{
    /// <summary>
    /// Runs before the GBuffer is written (useful for screen-space effects on depth/normals).
    /// </summary>
    BeforeGBuffer,

    /// <summary>
    /// Runs after GBuffer is complete but before lighting (useful for modifying surface properties).
    /// </summary>
    AfterGBuffer,

    /// <summary>
    /// Runs during lighting passes, after light accumulation but before composition.
    /// Effects can read/write to the light accumulation buffer.
    /// Perfect for global illumination effects like SSPT, SSAO, etc.
    /// </summary>
    DuringLighting,

    /// <summary>
    /// Runs after lighting composition (albedo * lighting) but before transparent objects.
    /// This is the traditional "opaque post-processing" stage.
    /// Perfect for SSR, effects that need final opaque color.
    /// </summary>
    AfterLighting,

    /// <summary>
    /// Runs after all rendering (including transparents) as final post-processing.
    /// Perfect for tonemapping, color grading, bloom, DOF, etc.
    /// </summary>
    PostProcess
}
