// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Echo;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime;

public abstract class ImageEffect
{
    /// <summary>
    /// Defines at which stage of the rendering pipeline this effect should run.
    /// Default is PostProcess for backward compatibility.
    /// </summary>
    public virtual RenderStage Stage => RenderStage.PostProcess;

    /// <summary>
    /// For backward compatibility: effects that run during opaque rendering (AfterLighting stage).
    /// New effects should use Stage property instead.
    /// </summary>
    [Obsolete("Use Stage property instead. Set Stage = RenderStage.AfterLighting")]
    public virtual bool IsOpaqueEffect { get; } = false;

    /// <summary>
    /// Whether this effect transforms HDR to LDR. Used for tonemapping effects.
    /// </summary>
    public virtual bool TransformsToLDR { get; } = false;

    /// <summary>
    /// NEW API: Called during rendering with full access to render targets.
    /// Override this for effects that need access to GBuffers, light accumulation, etc.
    /// </summary>
    /// <param name="context">Provides access to all render targets and rendering state</param>
    public virtual void OnRenderEffect(RenderContext context)
    {
    }

    /// <summary>
    /// Called after all rendering is complete for this camera.
    /// </summary>
    public virtual void OnPostRender(Camera camera) { }

    /// <summary>
    /// Called before culling for this camera.
    /// </summary>
    public virtual void OnPreCull(Camera camera) { }

    /// <summary>
    /// Called before rendering starts for this camera.
    /// </summary>
    public virtual void OnPreRender(Camera camera) { }
}

public enum CameraClearFlags
{
    Nothing,
    SolidColor,
    Depth,
    Skybox,
}

[Flags]
public enum DepthTextureMode
{
    None = 0,
    /// <summary>
    /// When enabled rendering will draw a Pre-Depth pass before the main rendering pass.
    /// This can improve overdrawing and sorting issues, but can be slower on some hardware and some cases.
    /// This also enables the _CameraDepthTexture shader property.
    /// </summary>
    Depth = 1, // _CameraDepthTexture
    //Normal = 2, // _CameraNormalsTexture
    MotionVectors = 4, // _CameraMotionVectorsTexture
}

public class Camera : MonoBehaviour
{
    public List<ImageEffect> Effects = [];

    public CameraClearFlags ClearFlags = CameraClearFlags.Skybox;
    public Color ClearColor = new(0f, 0f, 0f, 1f);
    public LayerMask CullingMask = LayerMask.Everything;

    public enum ProjectionType { Perspective, Orthographic }
    public ProjectionType ProjectionMode = ProjectionType.Perspective;

    public double FieldOfView = 60f;
    public double OrthographicSize = 0.5f;
    public double NearClipPlane = 0.01f;
    public double FarClipPlane = 1000f;
    //public Rect Viewrect = new(0, 0, 1, 1); // Not Implemented
    public int Depth = -1;

    public RenderPipeline? Pipeline;
    public RenderTexture? Target;
    public bool HDR = false;
    public double RenderScale = 1.0f;

    public bool IsOrthographic => ProjectionMode == ProjectionType.Orthographic;

    [SerializeIgnore]
    public DepthTextureMode DepthTextureMode = DepthTextureMode.None;

    private double _aspect;
    private bool _customAspect;
    private Double4x4 _projectionMatrix;
    private bool _customProjectionMatrix;

    private Double4x4 _previousViewMatrix;
    private Double4x4 _previousProjectionMatrix;
    private Double4x4 _previousViewProjectionMatrix;
    private bool _firstFrame = true;

    public uint PixelWidth { get; private set; }
    public uint PixelHeight { get; private set; }

    public double Aspect
    {
        get => _aspect;
        set
        {
            _aspect = value;
            _customAspect = true;
        }
    }

    public Double4x4 ProjectionMatrix
    {
        get => _projectionMatrix;
        set
        {
            _projectionMatrix = value;
            _customProjectionMatrix = true;
        }
    }

    public Double4x4 ViewMatrix { get; private set; }

    public Double4x4 PreviousViewMatrix => _previousViewMatrix;
    public Double4x4 PreviousProjectionMatrix => _previousProjectionMatrix;
    public Double4x4 PreviousViewProjectionMatrix => _previousViewProjectionMatrix;

    public Camera() : base() { }

    public override void OnEnable()
    {
        _firstFrame = true;
    }

    public void Render(in RenderingData? data = null)
    {
        RenderPipeline pipeline = Pipeline ?? DefaultRenderPipeline.Default;
        pipeline.Render(this, data ?? new());
    }

    public RenderTexture? UpdateRenderData()
    {
        if (!_firstFrame)
        {
            _previousViewMatrix = ViewMatrix;
            _previousProjectionMatrix = _projectionMatrix;
            _previousViewProjectionMatrix = _projectionMatrix * ViewMatrix;
        }
        _firstFrame = false;

        // Since Scene Updating is guranteed to execute before rendering, we can setup camera data for this frame here
        RenderTexture? camTarget = null;

        if (Target.IsValid())
            camTarget = Target;

        int width = camTarget?.Width ?? Window.InternalWindow.FramebufferSize.X;
        int height = camTarget?.Height ?? Window.InternalWindow.FramebufferSize.Y;

        double renderScale = Math.Clamp(RenderScale, 0.1f, 2.0f);
        PixelWidth = (uint)Math.Max(1, (int)(width * renderScale));
        PixelHeight = (uint)Math.Max(1, (int)(height * renderScale));

        if (!_customAspect)
            _aspect = PixelWidth / (double)PixelHeight;

        if (!_customProjectionMatrix)
        {
            _projectionMatrix = GetProjectionMatrix(_aspect);
        }

        ViewMatrix = Double4x4.CreateLookTo(Transform.Position, Transform.Forward, Transform.Up);

        return camTarget;
    }

    public void ResetAspect()
    {
        _aspect = PixelWidth / (double)PixelHeight;
        _customAspect = false;
    }

    public void ResetProjectionMatrix()
    {
        _projectionMatrix = GetProjectionMatrix(_aspect);
        _customProjectionMatrix = false;
    }

    public void ResetMotionHistory()
    {
        _firstFrame = true;
    }

    public Ray ScreenPointToRay(Double2 screenPoint, Double2 screenSize)
    {
        // Normalize screen coordinates to [-1, 1]
        Double2 ndc = new(
            (screenPoint.X / screenSize.X) * 2.0f - 1.0f,
            1.0f - (screenPoint.Y / screenSize.Y) * 2.0f
        );

        // Create the near and far points in NDC
        Double4 nearPointNDC = new(ndc.X, ndc.Y, 0.0f, 1.0f);
        Double4 farPointNDC = new(ndc.X, ndc.Y, 1.0f, 1.0f);

        // Calculate the inverse view-projection matrix
        double aspect = screenSize.X / screenSize.Y;
        Double4x4 viewProjectionMatrix = GetProjectionMatrix(aspect) * GetViewMatrix();
        Double4x4 inverseViewProjectionMatrix = viewProjectionMatrix.Invert();

        // Unproject the near and far points to world space
        Double4 nearPointWorld = Double4x4.TransformPoint(nearPointNDC, inverseViewProjectionMatrix);
        Double4 farPointWorld = Double4x4.TransformPoint(farPointNDC, inverseViewProjectionMatrix);

        // Perform perspective divide
        nearPointWorld /= nearPointWorld.W;
        farPointWorld /= farPointWorld.W;

        // Create the ray
        Double3 rayOrigin = new(nearPointWorld.X, nearPointWorld.Y, nearPointWorld.Z);
        Double3 rayDirection = Double3.Normalize(new Double3(farPointWorld.X, farPointWorld.Y, farPointWorld.Z) - rayOrigin);

        return new Ray(rayOrigin, rayDirection);
    }

    public Double4x4 GetViewMatrix(bool applyPosition = true)
    {
        Double3 position = applyPosition ? Transform.Position : Double3.Zero;

        return Double4x4.CreateLookTo(position, Transform.Forward, Transform.Up);
    }

    private Double4x4 GetProjectionMatrix(double aspect)
    {
        Double4x4 proj;

        if (ProjectionMode == ProjectionType.Orthographic)
            proj = Double4x4.CreateOrtho(OrthographicSize, OrthographicSize, NearClipPlane, FarClipPlane);
        else
            proj = Double4x4.CreatePerspectiveFov(Maths.ToRadians(FieldOfView), aspect, NearClipPlane, FarClipPlane);

        return proj;
    }
}
