// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime;

public enum ShadowQuality
{
    Hard = 0,
    Soft = 1
}

public abstract class Light : MonoBehaviour, IRenderableLight
{

    public Color Color = Color.White;
    public double Intensity = 8.0f;
    public double ShadowStrength = 1.0f;
    public double ShadowBias = 0.05f;
    public double ShadowNormalBias = 1f;
    public bool CastShadows = true;
    public ShadowQuality ShadowQuality = ShadowQuality.Hard;


    public override void Update()
    {
        GameObject.Scene.PushLight(this);
    }

    public virtual int GetLayer() => GameObject.LayerIndex;
    public virtual int GetLightID() => InstanceID;
    public abstract LightType GetLightType();
    public virtual Double3 GetLightPosition() => Transform.Position;
    public virtual Double3 GetLightDirection() => Transform.Forward;
    public virtual bool DoCastShadows() => CastShadows;
    public abstract void GetShadowMatrix(out Double4x4 view, out Double4x4 projection);
    public abstract void OnRenderLight(RenderTexture gBuffer, RenderTexture destination, DefaultRenderPipeline.CameraSnapshot css);
}
