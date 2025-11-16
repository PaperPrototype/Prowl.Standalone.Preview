using System.Runtime.CompilerServices;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Prowl.Runtime;

public partial class GameObject
{
    private static Material _standardMaterial;

    #region CUBES

    public static GameObject CreateCube(string name)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateCube(name, Double3.Zero, Double3.One, _standardMaterial);
    }

    public static GameObject CreateCube(Double3 position)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateCube(position.ToString(), position, Double3.One, _standardMaterial);
    }

    public static GameObject CreateCube(string name, Double3 position)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateCube(name, position, Double3.One, _standardMaterial);
    }

    public static GameObject CreateCube(string name, Double3 position, Double3 scale)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateCube(name, position, scale, _standardMaterial);
    }

    public static GameObject CreateCube(string name, Double3 position, Double3 scale, Material material)
    {
        // scaled mesh
        var mesh = Mesh.CreateCube(scale);

        // game object
        var go = new GameObject(name);
        go.Transform.Position = position;

        // visuals
        var ren = go.AddComponent<MeshRenderer>();
        ren.Mesh = mesh;
        ren.Material = material;

        // final game object
        return go;
    }

    public static GameObject CreatePhysicsCube(string name, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsCube(name, Double3.Zero, Double3.One, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsCube(Double3 position, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsCube(position.ToString(), position, Double3.One, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsCube(string name, Double3 position, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsCube(name, position, Double3.One, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsCube(string name, Double3 position, Double3 size, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsCube(name, position, size, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsCube(string name, Double3 position, Double3 size, Material material, bool isStatic = false)
    {
        // scaled mesh
        var mesh = Mesh.CreateCube(size);

        // game object
        var go = new GameObject(name);
        go.Transform.Position = position;

        // visuals
        var ren = go.AddComponent<MeshRenderer>();
        ren.Mesh = mesh;
        ren.Material = material;

        // rigidbody
        var rb = go.AddComponent<Rigidbody3D>();
        rb.IsStatic = isStatic;

        // scaled box collider
        var col = go.AddComponent<BoxCollider>();
        col.Size = size;

        // final game object
        return go;
    }
    #endregion

    #region SPHERES
   
    public static GameObject CreateSphere(string name)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateSphere(name, Double3.Zero, 1f, _standardMaterial);
    }

    public static GameObject CreateSphere(Double3 position)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateSphere(position.ToString(), position, 1f, _standardMaterial);
    }

    public static GameObject CreateSphere(string name, Double3 position)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateSphere(name, position, 1f, _standardMaterial);
    }

    public static GameObject CreateSphere(string name, Double3 position, float radius)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreateSphere(name, position, radius, _standardMaterial);
    }

    public static GameObject CreateSphere(string name, Double3 position, float radius, Material material)
    {
        // scaled mesh
        var mesh = Mesh.CreateSphere(radius, (int)(radius * 8f), (int)(radius * 8f));

        // game object
        var go = new GameObject(name);
        go.Transform.Position = position;

        // visuals
        var ren = go.AddComponent<MeshRenderer>();
        ren.Mesh = mesh;
        ren.Material = material;

        // final game object
        return go;
    }

    public static GameObject CreatePhysicsSphere(string name, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsSphere(name, Double3.Zero, 1f, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsSphere(Double3 position, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsSphere(position.ToString(), position, 1f, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsSphere(string name, Double3 position, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsSphere(name, position, 1f, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsSphere(string name, Double3 position, float radius, bool isStatic = false)
    {
        _standardMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        return CreatePhysicsSphere(name, position, radius, _standardMaterial, isStatic);
    }

    public static GameObject CreatePhysicsSphere(string name, Double3 position, float radius, Material material, bool isStatic = false)
    {
        // scaled mesh
        var mesh = Mesh.CreateSphere(radius, (int)(radius * 8f), (int)(radius * 8f));

        // game object
        var go = new GameObject(name);
        go.Transform.Position = position;

        // visuals
        var ren = go.AddComponent<MeshRenderer>();
        ren.Mesh = mesh;
        ren.Material = material;

        // rigidbody
        var rb = go.AddComponent<Rigidbody3D>();
        rb.IsStatic = isStatic;

        // scaled box collider
        var col = go.AddComponent<SphereCollider>();
        col.Radius = radius;

        // final game object
        return go;
    }
    #endregion
}