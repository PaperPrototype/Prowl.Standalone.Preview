// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using Prowl.Vector;

namespace Prowl.Runtime.ParticleSystem.Modules;

/// <summary>
/// Controls particle velocity over their lifetime.
/// </summary>
[Serializable]
public class VelocityOverLifetimeModule : ParticleSystemModule
{
    public MinMaxCurve VelocityX = new(0.0f);
    public MinMaxCurve VelocityY = new(0.0f);
    public MinMaxCurve VelocityZ = new(0.0f);

    public override void OnParticleUpdate(ref Particle particle, float deltaTime)
    {
        if (!Enabled) return;

        float normalizedTime = particle.NormalizedLifetime;
        float vx = VelocityX.Evaluate(normalizedTime, null);
        float vy = VelocityY.Evaluate(normalizedTime, null);
        float vz = VelocityZ.Evaluate(normalizedTime, null);

        Float3 velocityChange = new Float3(vx, vy, vz);
        particle.Velocity += velocityChange * deltaTime;
    }
}
