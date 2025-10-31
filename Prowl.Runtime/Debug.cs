// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime;

public enum LogSeverity
{
    Success = 1 << 0,
    Normal = 1 << 1,
    Warning = 1 << 2,
    Error = 1 << 3,
    Exception = 1 << 4
}


public delegate void OnLog(string message, DebugStackTrace? stackTrace, LogSeverity logSeverity);


public record DebugStackFrame(string FileName, int? Line = null, int? Column = null, MethodBase? MethodBase = null)
{
    public override string ToString()
    {
        string locSuffix = Line != null ? Column != null ? $"({Line},{Column})" : $"({Line})" : "";

        if (MethodBase != null)
            return $"In {MethodBase.DeclaringType.Name}.{MethodBase.Name} at {FileName}{locSuffix}";
        else
            return $"At {FileName}{locSuffix}";
    }

}


public record DebugStackTrace(params DebugStackFrame[] StackFrames)
{
    public static explicit operator DebugStackTrace(StackTrace stackTrace)
    {
        DebugStackFrame[] stackFrames = new DebugStackFrame[stackTrace.FrameCount];

        for (int i = 0; i < stackFrames.Length; i++)
        {
            StackFrame srcFrame = stackTrace.GetFrame(i);
            stackFrames[i] = new DebugStackFrame(srcFrame.GetFileName(), srcFrame.GetFileLineNumber(), srcFrame.GetFileColumnNumber(), srcFrame.GetMethod());
        }

        return new DebugStackTrace(stackFrames);
    }


    public override string ToString()
    {
        StringBuilder sb = new();

        for (int i = 0; i < StackFrames.Length; i++)
            sb.AppendLine($"\t{StackFrames[i]}");

        return sb.ToString();
    }
}


public static class Debug
{
    public static event OnLog? OnLog;

    public static void Log(object message)
        => Log(message.ToString(), LogSeverity.Normal);

    public static void Log(string message)
        => Log(message, LogSeverity.Normal);

    public static void LogWarning(object message)
        => Log(message.ToString(), LogSeverity.Warning);

    public static void LogWarning(string message)
        => Log(message, LogSeverity.Warning);

    public static void LogError(object message)
        => Log(message.ToString(), LogSeverity.Error);

    public static void LogError(string message)
        => Log(message, LogSeverity.Error);

    public static void LogSuccess(object message)
        => Log(message.ToString(), LogSeverity.Success);

    public static void LogSuccess(string message)
        => Log(message, LogSeverity.Success);

    public static void LogException(Exception exception)
    {
        ConsoleColor prevColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(exception.Message);

        if (exception.InnerException != null)
            Console.WriteLine(exception.InnerException.Message);

        DebugStackTrace trace = (DebugStackTrace)new StackTrace(exception.InnerException ?? exception, true);

        Console.WriteLine(trace.ToString());

        Console.ForegroundColor = prevColor;

        OnLog?.Invoke(exception.Message + "\n" + (exception.InnerException?.Message ?? ""), trace, LogSeverity.Exception);
    }

    // NOTE : StackTrace is pretty fast on modern .NET, so it's nice to keep it on by default, since it gives useful line numbers for debugging purposes.
    // For reference, getting a stack trace on a modern machine takes around 15 μs at a depth of 15.
    public static void Log(string message, LogSeverity logSeverity, DebugStackTrace? customTrace = null)
    {
        ConsoleColor prevColor = Console.ForegroundColor;

        Console.ForegroundColor = logSeverity switch
        {
            LogSeverity.Success => ConsoleColor.Green,
            LogSeverity.Warning => ConsoleColor.Yellow,
            LogSeverity.Error => ConsoleColor.Red,
            LogSeverity.Exception => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };

        Console.WriteLine(message);

        if (customTrace != null)
        {
            Console.WriteLine(customTrace.ToString());
            OnLog?.Invoke(message, customTrace, logSeverity);
        }
        else
        {
            StackTrace trace = new(2, true);
            OnLog?.Invoke(message, (DebugStackTrace)trace, logSeverity);
        }

        Console.ForegroundColor = prevColor;
    }

    public static void If(bool condition, string message = "")
    {
        if (condition)
            throw new Exception(message);
    }

    public static void IfNull(object value, string message = "")
    {
        if (value is null)
            throw new Exception(message);
    }

    public static void IfNullOrEmpty(string value, string message = "")
    {
        if (string.IsNullOrEmpty(value))
            throw new Exception(message);
    }

    internal static void ErrorGuard(Action value)
    {
        try
        {
            value();
        }
        catch (Exception e)
        {
            LogError(e.Message);
        }
    }

    public static void Assert(bool condition, string? message)
        => System.Diagnostics.Debug.Assert(condition, message);

    public static void Assert(bool condition)
        => System.Diagnostics.Debug.Assert(condition);

    #region Gizmos

    private static readonly GizmoBuilder s_gizmoBuilder = new();

    public static void ClearGizmos()
    {
        s_gizmoBuilder.Clear();
    }

    public static (Mesh? wire, Mesh? solid) GetGizmoDrawData()
    {
        return s_gizmoBuilder.UpdateMesh();
    }

    public static List<GizmoBuilder.IconDrawCall> GetGizmoIcons()
    {
        return s_gizmoBuilder.GetIcons();
    }

    public static void PushMatrix(Double4x4 matrix)
    {
        s_gizmoBuilder.PushMatrix(matrix);
    }

    public static void PopMatrix()
    {
        s_gizmoBuilder.PopMatrix();
    }

    public static void DrawLine(Double3 start, Double3 end, Color color) => s_gizmoBuilder.DrawLine(start, end, color);
    public static void DrawTriangle(Double3 a, Double3 b, Double3 c, Color color) => s_gizmoBuilder.DrawTriangle(a, b, c, color);
    public static void DrawWireCube(Double3 center, Double3 halfExtents, Color color) => s_gizmoBuilder.DrawWireCube(center, halfExtents, color);
    public static void DrawCube(Double3 center, Double3 halfExtents, Color color) => s_gizmoBuilder.DrawCube(center, halfExtents, color);
    public static void DrawWireCircle(Double3 center, Double3 normal, double radius, Color color, int segments = 16) => s_gizmoBuilder.DrawCircle(center, normal, radius, color, segments);
    public static void DrawWireSphere(Double3 center, double radius, Color color, int segments = 16) => s_gizmoBuilder.DrawWireSphere(center, radius, color, segments);
    public static void DrawSphere(Double3 center, double radius, Color color, int segments = 16) => s_gizmoBuilder.DrawSphere(center, radius, color, segments);
    public static void DrawWireCone(Double3 start, Double3 direction, double radius, Color color, int segments = 16) => s_gizmoBuilder.DrawWireCone(start, direction, radius, color, segments);
    public static void DrawWireCapsule(Double3 point1, Double3 point2, double radius, Color color, int segments = 16) => s_gizmoBuilder.DrawWireCapsule(point1, point2, radius, color, segments);
    public static void DrawWireCylinder(Double3 center, Quaternion rotation, double radius, double height, Color color, int segments = 16) => s_gizmoBuilder.DrawWireCylinder(center, rotation, radius, height, color, segments);
    public static void DrawArrow(Double3 start, Double3 direction, Color color) => s_gizmoBuilder.DrawArrow(start, direction, color);

    public static void DrawIcon(Texture2D icon, Double3 center, double scale, Color color) => s_gizmoBuilder.DrawIcon(icon, center, scale, color);

    #endregion

}

public class GizmoBuilder
{
    private struct MeshData
    {
        public List<Double3> Vertices = [];
        public List<Double2> Uvs = [];
        public List<Color32> Colors = [];
        public List<int> Indices = [];

        public MeshData()
        {
        }

        public readonly void Clear()
        {
            Vertices.Clear();
            Uvs.Clear();
            Colors.Clear();
            Indices.Clear();
        }
    }

    private MeshData _wireData = new();
    private MeshData _solidData = new();
    private Mesh? _wire;
    private Mesh? _solid;

    public struct IconDrawCall
    {
        public Texture2D Texture;
        public Double3 Center;
        public double Scale;
        public Color Color;
    }

    private List<IconDrawCall> _icons = [];

    private Stack<Double4x4> _matrix4X4s = new();


    public void Clear()
    {
        _wireData.Clear();
        _solidData.Clear();

        //_wire?.Clear();
        //_solid?.Clear();

        _icons.Clear();

        _matrix4X4s.Clear();
    }

    private void AddLine(Double3 a, Double3 b, Color color)
    {
        if (_matrix4X4s.Count > 0)
        {
            Double4x4 m = _matrix4X4s.Peek();
            a = Double4x4.TransformPoint(a, m);
            b = Double4x4.TransformPoint(b, m);
        }

        int index = _wireData.Vertices.Count;
        _wireData.Vertices.Add(a);
        _wireData.Vertices.Add(b);

        _wireData.Colors.Add(color);
        _wireData.Colors.Add(color);

        _wireData.Indices.Add(index);
        _wireData.Indices.Add(index + 1);
    }

    private void AddTriangle(Double3 a, Double3 b, Double3 c, Double2 a_uv, Double2 b_uv, Double2 c_uv, Color color)
    {
        if (_matrix4X4s.Count > 0)
        {
            Double4x4 m = _matrix4X4s.Peek();
            a = Double4x4.TransformPoint(a, m);
            b = Double4x4.TransformPoint(b, m);
            c = Double4x4.TransformPoint(c, m);
        }

        int index = _solidData.Vertices.Count;

        _solidData.Vertices.Add(a);
        _solidData.Vertices.Add(b);
        _solidData.Vertices.Add(c);

        _solidData.Uvs.Add(a_uv);
        _solidData.Uvs.Add(b_uv);
        _solidData.Uvs.Add(c_uv);

        _solidData.Colors.Add(color);
        _solidData.Colors.Add(color);
        _solidData.Colors.Add(color);

        _solidData.Indices.Add(index);
        _solidData.Indices.Add(index + 1);
        _solidData.Indices.Add(index + 2);
    }

    public void PushMatrix(Double4x4 matrix)
    {
        _matrix4X4s.Push(matrix);
    }

    public void PopMatrix()
    {
        _matrix4X4s.Pop();
    }

    public void DrawLine(Double3 start, Double3 end, Color color) => AddLine(start, end, color);

    public void DrawTriangle(Double3 a, Double3 b, Double3 c, Color color) => AddTriangle(a, b, c, Double2.Zero, Double2.Zero, Double2.Zero, color);

    public void DrawWireCube(Double3 center, Double3 halfExtents, Color color)
    {
        Double3[] vertices = [
            new Double3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z),
        ];

        AddLine(vertices[0], vertices[1], color);
        AddLine(vertices[1], vertices[2], color);
        AddLine(vertices[2], vertices[3], color);
        AddLine(vertices[3], vertices[0], color);

        AddLine(vertices[4], vertices[5], color);
        AddLine(vertices[5], vertices[6], color);
        AddLine(vertices[6], vertices[7], color);
        AddLine(vertices[7], vertices[4], color);

        AddLine(vertices[0], vertices[4], color);
        AddLine(vertices[1], vertices[5], color);
        AddLine(vertices[2], vertices[6], color);
        AddLine(vertices[3], vertices[7], color);
    }

    public void DrawCube(Double3 center, Double3 halfExtents, Color color)
    {
        Double3[] vertices = [
            new Double3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z - halfExtents.Z),
            new Double3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z),
            new Double3(center.X - halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z),
        ];

        Double2[] uvs = [
            new Double2(0, 0),
            new Double2(1, 0),
            new Double2(1, 1),
            new Double2(0, 1),
        ];

        AddTriangle(vertices[0], vertices[1], vertices[2], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[0], vertices[2], vertices[3], uvs[0], uvs[2], uvs[3], color);

        AddTriangle(vertices[4], vertices[6], vertices[5], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[4], vertices[7], vertices[6], uvs[0], uvs[2], uvs[3], color);

        AddTriangle(vertices[0], vertices[3], vertices[7], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[0], vertices[7], vertices[4], uvs[0], uvs[2], uvs[3], color);

        AddTriangle(vertices[1], vertices[5], vertices[6], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[1], vertices[6], vertices[2], uvs[0], uvs[2], uvs[3], color);

        AddTriangle(vertices[3], vertices[2], vertices[6], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[3], vertices[6], vertices[7], uvs[0], uvs[2], uvs[3], color);

        AddTriangle(vertices[0], vertices[4], vertices[5], uvs[0], uvs[1], uvs[2], color);
        AddTriangle(vertices[0], vertices[5], vertices[1], uvs[0], uvs[2], uvs[3], color);
    }

    public void DrawWireSphere(Double3 center, double radius, Color color, int segments = 16)
    {
        double step = MathF.PI * 2 / segments;

        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;

            Double3 a = new(Math.Cos(angle1) * radius + center.X,
                            Math.Sin(angle1) * radius + center.Y,
                            center.Z
                        );

            Double3 b = new(Math.Cos(angle2) * radius + center.X,
                            Math.Sin(angle2) * radius + center.Y,
                            center.Z
                        );

            AddLine(a, b, color);
        }

        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;

            Double3 a = new(Math.Cos(angle1) * radius + center.X,
                            center.Y,
                            Math.Sin(angle1) * radius + center.Z
                        );

            Double3 b = new(Math.Cos(angle2) * radius + center.X,
                            center.Y,
                            Math.Sin(angle2) * radius + center.Z
                        );

            AddLine(a, b, color);
        }

        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;

            Double3 a = new(center.X,
                            Math.Cos(angle1) * radius + center.Y,
                            Math.Sin(angle1) * radius + center.Z
                        );

            Double3 b = new(center.X,
                            Math.Cos(angle2) * radius + center.Y,
                            Math.Sin(angle2) * radius + center.Z
                        );

            AddLine(a, b, color);
        }
    }

    public void DrawCircle(Double3 center, Double3 normal, double radius, Color color, int segments)
    {
        Double3 u = Double3.Normalize(Double3.Cross(normal, Double3.UnitY));
        Double3 v = Double3.Normalize(Double3.Cross(u, normal));
        double step = MathF.PI * 2 / segments;
        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;
            Double3 a = center + radius * (Math.Cos(angle1) * u + Math.Sin(angle1) * v);
            Double3 b = center + radius * (Math.Cos(angle2) * u + Math.Sin(angle2) * v);
            AddLine(a, b, color);
        }
    }

    public void DrawSphere(Double3 center, double radius, Color color, int segments = 16)
    {
        int latitudeSegments = segments;
        int longitudeSegments = segments * 2;

        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            double theta1 = lat * MathF.PI / latitudeSegments;
            double theta2 = (lat + 1) * MathF.PI / latitudeSegments;

            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                double phi1 = lon * 2 * MathF.PI / longitudeSegments;
                double phi2 = (lon + 1) * 2 * MathF.PI / longitudeSegments;

                Double3 v1 = CalculatePointOnSphere(theta1, phi1, radius, center);
                Double3 v2 = CalculatePointOnSphere(theta1, phi2, radius, center);
                Double3 v3 = CalculatePointOnSphere(theta2, phi1, radius, center);
                Double3 v4 = CalculatePointOnSphere(theta2, phi2, radius, center);

                // First triangle
                AddTriangle(v1, v2, v3, Double2.Zero, Double2.Zero, Double2.Zero, color);

                // Second triangle
                AddTriangle(v2, v4, v3, Double2.Zero, Double2.Zero, Double2.Zero, color);
            }
        }
    }

    private Double3 CalculatePointOnSphere(double theta, double phi, double radius, Double3 center)
    {
        double x = Math.Sin(theta) * Math.Cos(phi);
        double y = Math.Cos(theta);
        double z = Math.Sin(theta) * Math.Sin(phi);

        return new Double3(
            x * radius + center.X,
            y * radius + center.Y,
            z * radius + center.Z
        );
    }

    public void DrawWireCone(Double3 start, Double3 direction, double radius, Color color, int segments = 16)
    {
        double step = MathF.PI * 2 / segments;
        Double3 tip = start + direction;

        // Normalize the direction vector
        Double3 dir = Double3.Normalize(direction);

        // Find perpendicular vectors
        Double3 u = GetPerpendicularVector(dir);
        Double3 v = Double3.Cross(dir, u);

        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;

            // Calculate circle points using the perpendicular vectors
            Double3 a = start + radius * (Math.Cos(angle1) * u + Math.Sin(angle1) * v);
            Double3 b = start + radius * (Math.Cos(angle2) * u + Math.Sin(angle2) * v);

            AddLine(a, b, color);
            if (i == 0 || i == segments / 4 || i == segments / 2 || i == segments * 3 / 4)
                AddLine(a, tip, color);
        }
    }

    public void DrawWireCapsule(Double3 point1, Double3 point2, double radius, Color color, int segments = 16)
    {
        // Calculate the axis of the capsule
        Double3 axis = point2 - point1;
        double height = Double3.Length(axis);

        if (height < 1e-6)
        {
            // Degenerate case: draw a sphere
            DrawWireSphere(point1, radius, color, segments);
            return;
        }

        Double3 dir = axis / height;

        // Find perpendicular vectors
        Double3 u = GetPerpendicularVector(dir);
        Double3 v = Double3.Cross(dir, u);

        double step = MathF.PI * 2 / segments;

        // Draw the cylindrical body (circles at both ends and connecting lines)
        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;

            // Circle at point1
            Double3 a1 = point1 + radius * (Math.Cos(angle1) * u + Math.Sin(angle1) * v);
            Double3 b1 = point1 + radius * (Math.Cos(angle2) * u + Math.Sin(angle2) * v);

            // Circle at point2
            Double3 a2 = point2 + radius * (Math.Cos(angle1) * u + Math.Sin(angle1) * v);
            Double3 b2 = point2 + radius * (Math.Cos(angle2) * u + Math.Sin(angle2) * v);

            AddLine(a1, b1, color);
            AddLine(a2, b2, color);

            // Connecting lines every quarter
            if (i % (segments / 4) == 0)
            {
                AddLine(a1, a2, color);
            }
        }

        // Draw hemisphere at point1 (bottom cap)
        for (int i = 0; i < segments / 2; i++)
        {
            double theta1 = MathF.PI / 2 + i * MathF.PI / segments;
            double theta2 = MathF.PI / 2 + (i + 1) * MathF.PI / segments;

            for (int j = 0; j < segments; j++)
            {
                double phi1 = j * 2 * MathF.PI / segments;
                double phi2 = (j + 1) * 2 * MathF.PI / segments;

                Double3 v1 = point1 + radius * (Math.Sin(theta1) * Math.Cos(phi1) * u + Math.Sin(theta1) * Math.Sin(phi1) * v + Math.Cos(theta1) * dir);
                Double3 v2 = point1 + radius * (Math.Sin(theta1) * Math.Cos(phi2) * u + Math.Sin(theta1) * Math.Sin(phi2) * v + Math.Cos(theta1) * dir);
                Double3 v3 = point1 + radius * (Math.Sin(theta2) * Math.Cos(phi1) * u + Math.Sin(theta2) * Math.Sin(phi1) * v + Math.Cos(theta2) * dir);

                if (j % (segments / 4) == 0)
                {
                    AddLine(v1, v3, color);
                }
                if (i == 0 || i == segments / 2 - 1)
                {
                    AddLine(v1, v2, color);
                }
            }
        }

        // Draw hemisphere at point2 (top cap)
        for (int i = 0; i < segments / 2; i++)
        {
            double theta1 = i * MathF.PI / segments;
            double theta2 = (i + 1) * MathF.PI / segments;

            for (int j = 0; j < segments; j++)
            {
                double phi1 = j * 2 * MathF.PI / segments;
                double phi2 = (j + 1) * 2 * MathF.PI / segments;

                Double3 v1 = point2 + radius * (Math.Sin(theta1) * Math.Cos(phi1) * u + Math.Sin(theta1) * Math.Sin(phi1) * v + Math.Cos(theta1) * dir);
                Double3 v2 = point2 + radius * (Math.Sin(theta1) * Math.Cos(phi2) * u + Math.Sin(theta1) * Math.Sin(phi2) * v + Math.Cos(theta1) * dir);
                Double3 v3 = point2 + radius * (Math.Sin(theta2) * Math.Cos(phi1) * u + Math.Sin(theta2) * Math.Sin(phi1) * v + Math.Cos(theta2) * dir);

                if (j % (segments / 4) == 0)
                {
                    AddLine(v1, v3, color);
                }
                if (i == 0 || i == segments / 2 - 1)
                {
                    AddLine(v1, v2, color);
                }
            }
        }
    }

    public void DrawWireCylinder(Double3 center, Quaternion rotation, double radius, double height, Color color, int segments)
    {
        Double3 up = rotation * Double3.UnitY;
        Double3 forward = rotation * Double3.UnitZ;
        Double3 right = rotation * Double3.UnitX;
        Double3 topCenter = center + (up * (height / 2));
        Double3 bottomCenter = center - (up * (height / 2));
        double step = MathF.PI * 2 / segments;
        // Draw top and bottom circles
        for (int i = 0; i < segments; i++)
        {
            double angle1 = i * step;
            double angle2 = (i + 1) * step;
            Double3 topA = topCenter + radius * (Math.Cos(angle1) * right + Math.Sin(angle1) * forward);
            Double3 topB = topCenter + radius * (Math.Cos(angle2) * right + Math.Sin(angle2) * forward);
            Double3 bottomA = bottomCenter + radius * (Math.Cos(angle1) * right + Math.Sin(angle1) * forward);
            Double3 bottomB = bottomCenter + radius * (Math.Cos(angle2) * right + Math.Sin(angle2) * forward);
            AddLine(topA, topB, color);
            AddLine(bottomA, bottomB, color);
            // Connecting lines every quarter
            if (i % (segments / 4) == 0)
            {
                AddLine(topA, bottomA, color);
            }
        }
    }

    private Double3 GetPerpendicularVector(Double3 v)
    {
        Double3 result;
        if (Math.Abs(v.X) > 0.1f)
            result = new Double3(v.Y, -v.X, 0);
        else if (Math.Abs(v.Y) > 0.1f)
            result = new Double3(0, v.Z, -v.Y);
        else
            result = new Double3(-v.Z, 0, v.X);
        return Double3.Normalize(result);
    }

    public void DrawArrow(Double3 start, Double3 direction, Color color)
    {
        Double3 axis = Double3.Normalize(direction);
        Double3 end = start + direction;
        AddLine(start, end, color);

        DrawWireCone(start + (direction * 0.9f), axis * 0.1f, 0.1f, color, 4);

    }

    public void DrawIcon(Texture2D icon, Double3 center, double scale, Color color) => _icons.Add(new IconDrawCall { Texture = icon, Center = center, Scale = scale, Color = color });

    public (Mesh? wire, Mesh? solid) UpdateMesh()
    {
        bool hasWire = _wireData.Vertices.Count > 0;
        if (hasWire)
        {
            _wire ??= new()
            {
                MeshTopology = GraphicsBackend.Primitives.Topology.Lines,
                IndexFormat = IndexFormat.UInt32,
            };

            _wire.Vertices = [.. _wireData.Vertices.Select(v => (Float3)v)];
            _wire.Colors = [.. _wireData.Colors];
            _wire.Indices = [.. _wireData.Indices.Select(i => (uint)i)];

            _wire.Vertices = [.. _wireData.Vertices.Select(v => (Float3)v)];
        }

        bool hasSolid = _solidData.Vertices.Count > 0;
        if (hasSolid)
        {
            _solid ??= new()
            {
                MeshTopology = GraphicsBackend.Primitives.Topology.Triangles,
                IndexFormat = IndexFormat.UInt32,
            };

            _solid.Vertices = [.. _solidData.Vertices.Select(v => (Float3)v)];

            _solid.Colors = [.. _solidData.Colors];
            _solid.UV = [.. _solidData.Uvs.Select(v => (Float2)v)];
            _solid.Indices = [.. _solidData.Indices.Select(i => (uint)i)];
        }

        return (
            hasWire ? _wire : null,
            hasSolid ? _solid : null
            );
    }

    public List<IconDrawCall> GetIcons()
    {
        return _icons;
    }
}
