// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Jitter2;
using Jitter2.LinearMath;

using Prowl.Vector;

namespace Prowl.Runtime;

public class JitterGizmosDrawer : IDebugDrawer
{
    static JitterGizmosDrawer m_Instance;
    public static JitterGizmosDrawer Instance => m_Instance ??= new();
    public Color color { get; set; } = new Color(0, 255, 0, 128);

    public void DrawPoint(in JVector p)
    {
        Float3 center = new(p.X, p.Y, p.Z);
        Debug.DrawSphere(center, 0.1f, color, 8);
    }

    public void DrawSegment(in JVector pA, in JVector pB)
    {
        Float3 a = new(pA.X, pA.Y, pA.Z);
        Float3 b = new(pB.X, pB.Y, pB.Z);
        Debug.DrawLine(a, b, color);
    }

    public void DrawTriangle(in JVector pA, in JVector pB, in JVector pC)
    {
        Float3 a = new(pA.X, pA.Y, pA.Z);
        Float3 b = new(pB.X, pB.Y, pB.Z);
        Float3 c = new(pC.X, pC.Y, pC.Z);
        //Debug.DrawTriangle(a, b, c, color);
        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, a, color);
    }
}
