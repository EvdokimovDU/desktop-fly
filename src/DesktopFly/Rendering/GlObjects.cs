using System.Numerics;
using DesktopFly.Core.Model3D;
using Silk.NET.OpenGL;

namespace DesktopFly.Rendering;

public class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public ShaderProgram(GL gl, string vertexSrc, string fragmentSrc)
    {
        _gl = gl;
        uint vs = CompileShader(ShaderType.VertexShader, vertexSrc);
        uint fs = CompileShader(ShaderType.FragmentShader, fragmentSrc);

        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vs);
        _gl.AttachShader(Handle, fs);
        _gl.LinkProgram(Handle);

        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string info = _gl.GetProgramInfoLog(Handle);
            throw new Exception($"Program linking failed: {info}");
        }

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
    }

    private uint CompileShader(ShaderType type, string src)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, src);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string info = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{type} compilation failed: {info}");
        }
        return shader;
    }

    public void Use() => _gl.UseProgram(Handle);

    public int GetUniformLocation(string name) => _gl.GetUniformLocation(Handle, name);

    public unsafe void SetUniform(string name, Matrix4x4 matrix)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl.UniformMatrix4(loc, 1, false, (float*)&matrix);
        }
    }

    public void SetUniform(string name, Vector4 v)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform4(loc, v.X, v.Y, v.Z, v.W);
    }

    public void SetUniform(string name, Vector3 v)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform3(loc, v.X, v.Y, v.Z);
    }

    public void SetUniform(string name, float f)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, f);
    }

    public void SetUniform(string name, bool b)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, b ? 1 : 0);
    }

    public void SetUniform(string name, int i)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, i);
    }

    public void Dispose() => _gl.DeleteProgram(Handle);
}

public class GlMesh : IDisposable
{
    private readonly GL _gl;
    public uint Vao { get; }
    public uint Vbo { get; }
    public uint Ebo { get; }
    public int IndexCount { get; }

    public unsafe GlMesh(GL gl, MeshData data)
    {
        _gl = gl;
        IndexCount = data.Indices.Length;

        Vao = _gl.GenVertexArray();
        _gl.BindVertexArray(Vao);

        // Interleave pos(3), normal(3), texcoord(2)
        int vertexCount = data.Vertices.Length / 3;
        var interleaved = new float[vertexCount * 8];
        for (int i = 0; i < vertexCount; i++)
        {
            interleaved[i * 8 + 0] = data.Vertices[i * 3 + 0];
            interleaved[i * 8 + 1] = data.Vertices[i * 3 + 1];
            interleaved[i * 8 + 2] = data.Vertices[i * 3 + 2];

            if (data.Normals.Length > i * 3 + 2)
            {
                interleaved[i * 8 + 3] = data.Normals[i * 3 + 0];
                interleaved[i * 8 + 4] = data.Normals[i * 3 + 1];
                interleaved[i * 8 + 5] = data.Normals[i * 3 + 2];
            }

            if (data.TexCoords.Length > i * 2 + 1)
            {
                interleaved[i * 8 + 6] = data.TexCoords[i * 2 + 0];
                interleaved[i * 8 + 7] = data.TexCoords[i * 2 + 1];
            }
        }

        Vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = interleaved)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(interleaved.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        if (data.Indices.Length > 0)
        {
            Ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
            fixed (uint* p = data.Indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(data.Indices.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);
            }
        }

        uint stride = 8 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindVertexArray(0);
    }

    public unsafe void Draw()
    {
        _gl.BindVertexArray(Vao);
        if (IndexCount > 0)
        {
            _gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(Vao);
        _gl.DeleteBuffer(Vbo);
        if (Ebo != 0) _gl.DeleteBuffer(Ebo);
    }
}

public class GlPointCloud : IDisposable
{
    private readonly GL _gl;
    public uint Vao { get; }
    public uint Vbo { get; }
    public int PointCount { get; }

    public unsafe GlPointCloud(GL gl, Vector3[] positions, Vector4[] colors)
    {
        _gl = gl;
        PointCount = Math.Min(positions.Length, colors.Length);

        Vao = _gl.GenVertexArray();
        _gl.BindVertexArray(Vao);

        // Interleave pos(3), color(4)
        var interleaved = new float[PointCount * 7];
        for (int i = 0; i < PointCount; i++)
        {
            interleaved[i * 7 + 0] = positions[i].X;
            interleaved[i * 7 + 1] = positions[i].Y;
            interleaved[i * 7 + 2] = positions[i].Z;

            interleaved[i * 7 + 3] = colors[i].X;
            interleaved[i * 7 + 4] = colors[i].Y;
            interleaved[i * 7 + 5] = colors[i].Z;
            interleaved[i * 7 + 6] = colors[i].W;
        }

        Vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = interleaved)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(interleaved.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        }

        uint stride = 7 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    public void Draw()
    {
        _gl.BindVertexArray(Vao);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)PointCount);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(Vao);
        _gl.DeleteBuffer(Vbo);
    }
}

public class GlTexture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public unsafe GlTexture(GL gl, byte[] rgba, int width, int height)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        fixed (byte* p = rgba)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
