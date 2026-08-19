using System.Numerics;
using DesktopFly.Core.Model3D;
using Silk.NET.OpenGL;

namespace DesktopFly.Rendering;

public class SceneRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _meshShader;
    private readonly Dictionary<MeshData, GlMesh> _meshCache = new();
    private readonly Dictionary<byte[], GlTexture> _textureCache = new();

    public Vector3 LightDir { get; set; } = Vector3.Normalize(new Vector3(-0.30f, 0.35f, -1.0f));
    public Vector3 LightColor { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);
    public Vector3 AmbientColor { get; set; } = new Vector3(0.55f, 0.55f, 0.55f);

    public SceneRenderer(GL gl)
    {
        _gl = gl;
        _meshShader = new ShaderProgram(_gl, ShaderSources.MeshVertexShader, ShaderSources.MeshFragmentShader);
    }

    public void Render(SceneNode rootNode, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector3 cameraPos)
    {
        _meshShader.Use();
        _meshShader.SetUniform("uView", viewMatrix);
        _meshShader.SetUniform("uProjection", projectionMatrix);
        _meshShader.SetUniform("uLightDir", LightDir);
        _meshShader.SetUniform("uLightColor", LightColor);
        _meshShader.SetUniform("uAmbientColor", AmbientColor);
        _meshShader.SetUniform("uCameraPos", cameraPos);

        RenderNode(rootNode, Matrix4x4.Identity);
    }

    private void RenderNode(SceneNode node, Matrix4x4 parentMatrix)
    {
        if (node.IsHidden) return;

        var worldMatrix = node.LocalMatrix * parentMatrix;

        if (node.Geometry != null)
        {
            if (!_meshCache.TryGetValue(node.Geometry, out var glMesh))
            {
                glMesh = new GlMesh(_gl, node.Geometry);
                _meshCache[node.Geometry] = glMesh;
            }

            var mat = node.Material ?? new Material();

            _meshShader.SetUniform("uModel", worldMatrix);
            _meshShader.SetUniform("uDiffuse", mat.Diffuse);
            _meshShader.SetUniform("uSpecular", mat.Specular);
            _meshShader.SetUniform("uEmission", mat.Emission);
            _meshShader.SetUniform("uShininess", mat.Shininess);
            _meshShader.SetUniform("uOpacity", node.Opacity);

            if (mat.TextureRgba != null)
            {
                if (!_textureCache.TryGetValue(mat.TextureRgba, out var glTexture))
                {
                    glTexture = new GlTexture(_gl, mat.TextureRgba, mat.TextureWidth, mat.TextureHeight);
                    _textureCache[mat.TextureRgba] = glTexture;
                }
                glTexture.Bind(TextureUnit.Texture0);
                _meshShader.SetUniform("uUseTexture", true);
                _meshShader.SetUniform("uTexture", 0);
            }
            else
            {
                _meshShader.SetUniform("uUseTexture", false);
            }

            if (mat.IsDoubleSided)
            {
                _gl.Disable(EnableCap.CullFace);
            }
            else
            {
                _gl.Enable(EnableCap.CullFace);
                _gl.CullFace(TriangleFace.Back);
            }

            if (mat.BlendMode == BlendMode.Add)
            {
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            }
            else
            {
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }

            glMesh.Draw();
        }

        foreach (var child in node.Children)
        {
            RenderNode(child, worldMatrix);
        }
    }

    public void Dispose()
    {
        _meshShader.Dispose();
        foreach (var m in _meshCache.Values) m.Dispose();
        foreach (var t in _textureCache.Values) t.Dispose();
        _meshCache.Clear();
        _textureCache.Clear();
    }
}
