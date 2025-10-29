// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using System;
using System.Collections.Generic;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Provides access to rendering resources and targets for image effects.
/// This context is passed to effects during rendering and gives them access to
/// all necessary buffers, textures, and rendering state.
/// </summary>
public sealed class RenderContext : IDisposable
{
    // Core rendering targets
    public RenderTexture GBuffer { get; set; }            // Contains all GBuffer attachments (Albedo, Normal, PBR, Custom) + Depth
    public RenderTexture LightAccumulation { get; set; }  // Light accumulation buffer (before albedo multiply)
    public RenderTexture SceneColor { get; set; }         // Final composed color (albedo * lighting)

    // Camera info
    public Camera Camera { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    // Current rendering stage
    public RenderStage CurrentStage { get; set; }

    // Temporary RT pool
    private readonly List<RenderTexture> _temporaryRTs = new();
    private readonly List<RenderTexture> _replacedRTs = new();

    /// <summary>
    /// Allocates a temporary render texture for use during this frame.
    /// Will be automatically disposed at the end of the frame.
    /// </summary>
    public RenderTexture GetTemporaryRT(int width, int height, TextureImageFormat format)
    {
        var rt = new RenderTexture(width, height, false, [format]);
        _temporaryRTs.Add(rt);
        return rt;
    }

    /// <summary>
    /// Replaces the scene color buffer with a new one (e.g., for HDR to LDR conversion).
    /// Only allowed during PostProcess stage.
    /// The old buffer is tracked for cleanup by the pipeline. Returns the new buffer.
    /// </summary>
    public RenderTexture ReplaceSceneColor(RenderTexture newBuffer)
    {
        if (CurrentStage != RenderStage.PostProcess)
            throw new InvalidOperationException("ReplaceSceneColor can only be called during PostProcess stage");

        if (SceneColor != null && !_replacedRTs.Contains(SceneColor))
        {
            _replacedRTs.Add(SceneColor);
        }
        SceneColor = newBuffer;
        return newBuffer;
    }

    /// <summary>
    /// Gets the list of replaced render targets that need cleanup by the pipeline.
    /// </summary>
    internal List<RenderTexture> GetReplacedRTs() => _replacedRTs;

    /// <summary>
    /// Allocates a temporary render texture matching the screen resolution.
    /// </summary>
    public RenderTexture GetTemporaryRT(TextureImageFormat format)
    {
        return GetTemporaryRT(Width, Height, format);
    }

    /// <summary>
    /// Allocates a temporary render texture with a resolution scale.
    /// </summary>
    public RenderTexture GetTemporaryRT(float scale, TextureImageFormat format)
    {
        int width = (int)(Width * scale);
        int height = (int)(Height * scale);
        return GetTemporaryRT(width, height, format);
    }

    /// <summary>
    /// Releases all temporary render textures allocated during this frame.
    /// Called automatically by the rendering pipeline.
    /// </summary>
    public void ReleaseTemporaryRTs()
    {
        foreach (var rt in _temporaryRTs)
        {
            rt?.Dispose();
        }
        _temporaryRTs.Clear();
    }

    public void Dispose()
    {
        ReleaseTemporaryRTs();
        // Note: Replaced RTs are NOT disposed here - the pipeline handles those
        _replacedRTs.Clear();
    }
}
