using System.Numerics;

namespace DesktopFly.Core.Model3D;

public enum BlendMode
{
    Alpha,
    Add
}

public class Material
{
    public Vector4 Diffuse { get; set; } = Vector4.One;
    public Vector4 Specular { get; set; } = new Vector4(0.25f, 0.25f, 0.25f, 1f);
    public Vector4 Emission { get; set; } = Vector4.Zero;
    public float Shininess { get; set; } = 0.25f;
    public BlendMode BlendMode { get; set; } = BlendMode.Alpha;
    public bool IsDoubleSided { get; set; } = false;
    public byte[]? TextureRgba { get; set; }
    public int TextureWidth { get; set; }
    public int TextureHeight { get; set; }
}

public class MeshData
{
    public float[] Vertices { get; set; } = Array.Empty<float>();   // x, y, z
    public float[] Normals { get; set; } = Array.Empty<float>();    // nx, ny, nz
    public float[] TexCoords { get; set; } = Array.Empty<float>();  // u, v
    public float[] Colors { get; set; } = Array.Empty<float>();     // r, g, b, a
    public uint[] Indices { get; set; } = Array.Empty<uint>();
}

public class SceneNode
{
    public string Name { get; set; } = "";
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 EulerAngles { get; set; } = Vector3.Zero;
    public Vector3 Scale { get; set; } = Vector3.One;
    public float Opacity { get; set; } = 1.0f;
    public bool IsHidden { get; set; } = false;

    public MeshData? Geometry { get; set; }
    public Material? Material { get; set; }

    public SceneNode? Parent { get; private set; }
    public List<SceneNode> Children { get; } = new();

    public void AddChildNode(SceneNode child)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveFromParentNode()
    {
        Parent?.Children.Remove(this);
        Parent = null;
    }

    public Matrix4x4 LocalMatrix
    {
        get
        {
            var m = Matrix4x4.CreateScale(Scale);
            // SCNNode euler angles: X (pitch), Y (yaw), Z (roll)
            var rot = Matrix4x4.CreateRotationZ(EulerAngles.Z) *
                      Matrix4x4.CreateRotationY(EulerAngles.Y) *
                      Matrix4x4.CreateRotationX(EulerAngles.X);
            m *= rot;
            m *= Matrix4x4.CreateTranslation(Position);
            return m;
        }
    }

    public Matrix4x4 WorldMatrix => Parent == null ? LocalMatrix : LocalMatrix * Parent.WorldMatrix;
}
