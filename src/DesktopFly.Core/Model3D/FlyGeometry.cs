using System.Numerics;

namespace DesktopFly.Core.Model3D;

public static class FlyGeometry
{
    public static MeshData CreateSphere(float radius, int rings = 16, int sectors = 24)
    {
        var vertices = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<uint>();

        float R = 1f / rings;
        float S = 1f / sectors;

        for (int r = 0; r <= rings; r++)
        {
            float v = r * R;
            float phi = v * MathF.PI;
            float y = MathF.Cos(phi);
            float sinPhi = MathF.Sin(phi);

            for (int s = 0; s <= sectors; s++)
            {
                float u = s * S;
                float theta = u * MathF.PI * 2;
                float x = MathF.Cos(theta) * sinPhi;
                float z = MathF.Sin(theta) * sinPhi;

                vertices.Add(x * radius);
                vertices.Add(y * radius);
                vertices.Add(z * radius);

                normals.Add(x);
                normals.Add(y);
                normals.Add(z);

                texCoords.Add(u);
                texCoords.Add(v);
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < sectors; s++)
            {
                uint first = (uint)(r * (sectors + 1) + s);
                uint second = (uint)((r + 1) * (sectors + 1) + s);

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        return new MeshData
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            TexCoords = texCoords.ToArray(),
            Indices = indices.ToArray()
        };
    }

    public static MeshData CreateCapsule(float capRadius, float height, int rings = 8, int sectors = 16)
    {
        // Capsule along Y axis centered at origin: cylinder from -height/2 to +height/2 with half-spheres at ends
        var vertices = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<uint>();

        float halfH = height * 0.5f;

        // Top hemisphere + cylinder + bottom hemisphere
        int totalRings = rings * 2 + 1;
        for (int r = 0; r <= totalRings; r++)
        {
            float phi;
            float yOffset;
            if (r <= rings)
            {
                phi = (r / (float)rings) * (MathF.PI * 0.5f);
                yOffset = halfH;
            }
            else
            {
                phi = (MathF.PI * 0.5f) + ((r - rings - 1) / (float)rings) * (MathF.PI * 0.5f);
                yOffset = -halfH;
            }

            float y = MathF.Cos(phi) * capRadius + yOffset;
            float sinPhi = MathF.Sin(phi);

            for (int s = 0; s <= sectors; s++)
            {
                float u = s / (float)sectors;
                float theta = u * MathF.PI * 2;
                float x = MathF.Cos(theta) * sinPhi;
                float z = MathF.Sin(theta) * sinPhi;

                vertices.Add(x * capRadius);
                vertices.Add(y);
                vertices.Add(z * capRadius);

                normals.Add(x);
                normals.Add(MathF.Cos(phi));
                normals.Add(z);

                texCoords.Add(u);
                texCoords.Add(r / (float)totalRings);
            }
        }

        for (int r = 0; r < totalRings; r++)
        {
            for (int s = 0; s < sectors; s++)
            {
                uint first = (uint)(r * (sectors + 1) + s);
                uint second = (uint)((r + 1) * (sectors + 1) + s);

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        return new MeshData
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            TexCoords = texCoords.ToArray(),
            Indices = indices.ToArray()
        };
    }

    public static MeshData CreateCone(float topRadius, float bottomRadius, float height, int sectors = 16)
    {
        var vertices = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<uint>();

        float halfH = height * 0.5f;

        // Side vertices
        for (int s = 0; s <= sectors; s++)
        {
            float u = s / (float)sectors;
            float theta = u * MathF.PI * 2;
            float cos = MathF.Cos(theta);
            float sin = MathF.Sin(theta);

            // Top
            vertices.Add(cos * topRadius);
            vertices.Add(halfH);
            vertices.Add(sin * topRadius);
            normals.Add(cos);
            normals.Add(0);
            normals.Add(sin);
            texCoords.Add(u);
            texCoords.Add(1);

            // Bottom
            vertices.Add(cos * bottomRadius);
            vertices.Add(-halfH);
            vertices.Add(sin * bottomRadius);
            normals.Add(cos);
            normals.Add(0);
            normals.Add(sin);
            texCoords.Add(u);
            texCoords.Add(0);
        }

        for (int s = 0; s < sectors; s++)
        {
            uint first = (uint)(s * 2);
            uint second = (uint)(s * 2 + 1);
            uint nextFirst = (uint)((s + 1) * 2);
            uint nextSecond = (uint)((s + 1) * 2 + 1);

            indices.Add(first);
            indices.Add(second);
            indices.Add(nextFirst);

            indices.Add(second);
            indices.Add(nextSecond);
            indices.Add(nextFirst);
        }

        return new MeshData
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            TexCoords = texCoords.ToArray(),
            Indices = indices.ToArray()
        };
    }

    public static MeshData CreateWingShape(int segments = 32)
    {
        // Extruded oval shape: x in [-2.6, 2.6], y in [-15.5, 1.0], depth 0.12
        var vertices = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<uint>();

        float depth = 0.12f;
        float halfD = depth * 0.5f;
        float rx = 2.6f;
        float ry = 8.25f; // height 16.5 -> ry = 8.25
        float centerY = -7.25f; // [-15.5, 1.0] center is -7.25

        // Front and back 2D polygon vertices
        var poly = new (float X, float Y)[segments];
        for (int i = 0; i < segments; i++)
        {
            float theta = i * MathF.PI * 2 / segments;
            poly[i] = (MathF.Cos(theta) * rx, centerY + MathF.Sin(theta) * ry);
        }

        // Top face (z = +halfD)
        int frontStart = vertices.Count / 3;
        // Center vertex
        vertices.Add(0); vertices.Add(centerY); vertices.Add(halfD);
        normals.Add(0); normals.Add(0); normals.Add(1);
        texCoords.Add(0.5f); texCoords.Add(0.5f);

        for (int i = 0; i < segments; i++)
        {
            vertices.Add(poly[i].X); vertices.Add(poly[i].Y); vertices.Add(halfD);
            normals.Add(0); normals.Add(0); normals.Add(1);
            texCoords.Add((poly[i].X + rx) / (2 * rx));
            texCoords.Add((poly[i].Y - centerY + ry) / (2 * ry));
        }

        for (int i = 0; i < segments; i++)
        {
            uint next = (uint)((i + 1) % segments);
            indices.Add((uint)frontStart);
            indices.Add((uint)(frontStart + 1 + i));
            indices.Add((uint)(frontStart + 1 + next));
        }

        // Bottom face (z = -halfD)
        int backStart = vertices.Count / 3;
        vertices.Add(0); vertices.Add(centerY); vertices.Add(-halfD);
        normals.Add(0); normals.Add(0); normals.Add(-1);
        texCoords.Add(0.5f); texCoords.Add(0.5f);

        for (int i = 0; i < segments; i++)
        {
            vertices.Add(poly[i].X); vertices.Add(poly[i].Y); vertices.Add(-halfD);
            normals.Add(0); normals.Add(0); normals.Add(-1);
            texCoords.Add((poly[i].X + rx) / (2 * rx));
            texCoords.Add((poly[i].Y - centerY + ry) / (2 * ry));
        }

        for (int i = 0; i < segments; i++)
        {
            uint next = (uint)((i + 1) % segments);
            indices.Add((uint)backStart);
            indices.Add((uint)(backStart + 1 + next));
            indices.Add((uint)(backStart + 1 + i));
        }

        // Sides
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            uint v0 = (uint)(frontStart + 1 + i);
            uint v1 = (uint)(frontStart + 1 + next);
            uint v2 = (uint)(backStart + 1 + i);
            uint v3 = (uint)(backStart + 1 + next);

            indices.Add(v0);
            indices.Add(v2);
            indices.Add(v1);

            indices.Add(v1);
            indices.Add(v2);
            indices.Add(v3);
        }

        return new MeshData
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            TexCoords = texCoords.ToArray(),
            Indices = indices.ToArray()
        };
    }
}
