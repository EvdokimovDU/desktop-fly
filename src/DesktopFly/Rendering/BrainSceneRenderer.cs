using System.Numerics;
using DesktopFly.Core.Model3D;
using DesktopFly.Core.Models;
using DesktopFly.Core.Sim;
using Silk.NET.OpenGL;

namespace DesktopFly.Rendering;

public class BrainSceneRenderer : IDisposable
{
    private static readonly Vector4[] ClassColors = new[]
    {
        new Vector4(0.16f, 0.22f, 0.34f, 1f), // optic
        new Vector4(0.45f, 0.33f, 0.16f, 1f), // central
        new Vector4(0.14f, 0.36f, 0.34f, 1f), // sensory
        new Vector4(0.10f, 0.48f, 0.62f, 1f), // visual_projection
        new Vector4(0.38f, 0.22f, 0.55f, 1f), // visual_centrifugal
        new Vector4(0.62f, 0.28f, 0.10f, 1f), // descending
        new Vector4(0.20f, 0.45f, 0.18f, 1f), // ascending
        new Vector4(0.55f, 0.14f, 0.14f, 1f), // motor
        new Vector4(0.50f, 0.25f, 0.40f, 1f), // endocrine
    };

    private readonly GL _gl;
    private readonly ShaderProgram _pointShader;
    private readonly ShaderProgram _meshShader;
    private readonly GlPointCloud _somaCloud;
    private readonly GlPointCloud _circuitCloud;

    private readonly LIFSim _sim;
    private readonly GlMesh _sphereMesh;

    public float RotationX { get; set; } = -0.15f;
    public float RotationY { get; set; } = 0.35f;
    public float Zoom { get; set; } = 1.0f;
    public bool IsHovered { get; set; } = false;

    // Flash particle structure
    public class FlashParticle
    {
        public Vector3 Position;
        public float Lifetime;
        public float MaxLifetime;
        public bool IsGF;
    }
    public List<FlashParticle> Flashes { get; } = new();

    // Stimulated cluster highlights
    public class HighlightCluster
    {
        public Vector3[] Positions = Array.Empty<Vector3>();
        public float Lifetime;
        public float MaxLifetime = 2.5f;
    }
    public List<HighlightCluster> Highlights { get; } = new();

    public void AddHighlight(IEnumerable<Vector3> positions, float duration = 2.5f)
    {
        Highlights.Add(new HighlightCluster
        {
            Positions = positions.ToArray(),
            Lifetime = duration,
            MaxLifetime = duration
        });
        if (Highlights.Count > 8) Highlights.RemoveAt(0);
    }

    // Stim ring animation
    public Vector3? StimRingPos;
    public float StimRingTime = 0f;

    public Matrix4x4 CurrentModelMatrix { get; private set; } = Matrix4x4.Identity;

    public BrainSceneRenderer(GL gl, BrainPointsFile points, LIFSim sim)
    {
        _gl = gl;
        _sim = sim;

        _pointShader = new ShaderProgram(_gl, ShaderSources.PointVertexShader, ShaderSources.PointFragmentShader);
        _meshShader = new ShaderProgram(_gl, ShaderSources.MeshVertexShader, ShaderSources.MeshFragmentShader);

        // 1. Full brain somas
        var somaPositions = new Vector3[points.Points.Length];
        var somaColors = new Vector4[points.Points.Length];
        for (int i = 0; i < points.Points.Length; i++)
        {
            var pt = points.Points[i];
            somaPositions[i] = new Vector3(pt[0], pt[1], pt[2]);
            int classIdx = Math.Clamp((int)pt[3], 0, ClassColors.Length - 1);
            somaColors[i] = ClassColors[classIdx];
        }
        _somaCloud = new GlPointCloud(_gl, somaPositions, somaColors);

        // 2. Circuit neurons
        var circuitPositions = new List<Vector3>();
        var circuitColors = new List<Vector4>();
        for (int i = 0; i < sim.N; i++)
        {
            circuitPositions.Add(sim.Positions[i]);
            var c = sim.Roles[i] switch
            {
                "lc4" or "lplc2" => new Vector4(0.2f, 0.8f, 1.0f, 1f),
                "gf" => new Vector4(1.0f, 0.9f, 0.1f, 1f),
                "dna01" or "dna02" => new Vector4(0.3f, 1.0f, 0.4f, 1f),
                "dnp09" => new Vector4(1.0f, 0.4f, 0.2f, 1f),
                "dng11" => new Vector4(1.0f, 0.3f, 0.8f, 1f),
                "escw" => new Vector4(0.9f, 0.6f, 0.2f, 1f),
                "mdn" => new Vector4(0.7f, 0.3f, 1.0f, 1f),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 0.8f)
            };
            circuitColors.Add(c);
        }
        _circuitCloud = new GlPointCloud(_gl, circuitPositions.ToArray(), circuitColors.ToArray());

        _sphereMesh = new GlMesh(_gl, FlyGeometry.CreateSphere(1.0f, 12, 16));
    }

    public void TriggerStimRing(Vector3 pos)
    {
        StimRingPos = pos;
        StimRingTime = 0f;
    }

    public void Flash(int neuron, bool isGF)
    {
        if (neuron >= 0 && neuron < _sim.N)
        {
            float duration = isGF ? 0.6f : 0.28f;
            Flashes.Add(new FlashParticle
            {
                Position = _sim.Positions[neuron],
                Lifetime = duration,
                MaxLifetime = duration,
                IsGF = isGF
            });
            if (Flashes.Count > 64) Flashes.RemoveAt(0);
        }
    }

    public void Update(float dt)
    {
        // Drain spike bus
        if (_sim.SpikeBus != null)
        {
            var events = _sim.SpikeBus.PopAll();
            foreach (var e in events)
            {
                Flash(e.Neuron, e.IsGF);
            }
        }

        // Update flashes
        for (int i = Flashes.Count - 1; i >= 0; i--)
        {
            Flashes[i].Lifetime -= dt;
            if (Flashes[i].Lifetime <= 0) Flashes.RemoveAt(i);
        }

        // Update cluster highlights
        for (int i = Highlights.Count - 1; i >= 0; i--)
        {
            Highlights[i].Lifetime -= dt;
            if (Highlights[i].Lifetime <= 0) Highlights.RemoveAt(i);
        }

        if (StimRingPos.HasValue)
        {
            StimRingTime += dt;
            if (StimRingTime >= 0.55f) StimRingPos = null;
        }
    }

    public void Render(float width, float height)
    {
        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // Additive blend for brain points
        _gl.Disable(EnableCap.DepthTest);

        var cameraPos = new Vector3(0f, 0.6f, 29f);
        var viewMatrix = Matrix4x4.CreateLookAt(cameraPos, new Vector3(0, 0.6f, 0), Vector3.UnitY);
        var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(46f * MathF.PI / 180f, width / height, 1f, 120f);

        var modelMatrix = Matrix4x4.CreateRotationX(RotationX) * Matrix4x4.CreateRotationY(RotationY) * Matrix4x4.CreateScale(Zoom);
        CurrentModelMatrix = modelMatrix;

        _pointShader.Use();
        _pointShader.SetUniform("uView", viewMatrix);
        _pointShader.SetUniform("uProjection", projMatrix);
        _pointShader.SetUniform("uModel", modelMatrix);

        // 1. Soma cloud
        _pointShader.SetUniform("uPointSize", 3.0f);
        _pointShader.SetUniform("uOpacity", 0.7f);
        _somaCloud.Draw();

        // 2. Circuit cloud
        _pointShader.SetUniform("uPointSize", 6.0f);
        _pointShader.SetUniform("uOpacity", 1.0f);
        _circuitCloud.Draw();

        // 3. GF Glowing spheres & Flash particles & Stim Highlights
        _meshShader.Use();
        _meshShader.SetUniform("uView", viewMatrix);
        _meshShader.SetUniform("uProjection", projMatrix);
        _meshShader.SetUniform("uLightDir", -Vector3.UnitZ);
        _meshShader.SetUniform("uLightColor", Vector3.Zero);
        _meshShader.SetUniform("uAmbientColor", Vector3.Zero);
        _meshShader.SetUniform("uCameraPos", cameraPos);
        _meshShader.SetUniform("uUseTexture", false);

        // Giant Fibers glowing spheres
        foreach (int gfIdx in _sim.GF)
        {
            var p = _sim.Positions[gfIdx];
            var sphereMat = Matrix4x4.CreateScale(0.28f) * Matrix4x4.CreateTranslation(p) * modelMatrix;
            _meshShader.SetUniform("uModel", sphereMat);
            _meshShader.SetUniform("uEmission", new Vector4(1.0f, 0.85f, 0.25f, 1f));
            _meshShader.SetUniform("uOpacity", 0.35f);
            _sphereMesh.Draw();
        }

        // Live flashes
        foreach (var f in Flashes)
        {
            float alpha = f.Lifetime / f.MaxLifetime;
            float scale = f.IsGF ? 0.45f : 0.16f;
            var sphereMat = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(f.Position) * modelMatrix;
            _meshShader.SetUniform("uModel", sphereMat);
            _meshShader.SetUniform("uEmission", f.IsGF ? new Vector4(1.0f, 0.9f, 0.2f, 1f) : new Vector4(0.75f, 0.95f, 1.0f, 1f));
            _meshShader.SetUniform("uOpacity", alpha * (f.IsGF ? 1.0f : 0.8f));
            _sphereMesh.Draw();
        }

        // Stimulated neuron highlights
        foreach (var h in Highlights)
        {
            float alpha = Math.Clamp(h.Lifetime / h.MaxLifetime, 0f, 1f);
            float pulse = 0.22f + 0.08f * MathF.Sin(h.Lifetime * 14f);
            foreach (var pos in h.Positions)
            {
                var mat = Matrix4x4.CreateScale(pulse) * Matrix4x4.CreateTranslation(pos) * modelMatrix;
                _meshShader.SetUniform("uModel", mat);
                _meshShader.SetUniform("uEmission", new Vector4(0.1f, 0.95f, 1.0f, 1f));
                _meshShader.SetUniform("uOpacity", alpha * 0.95f);
                _sphereMesh.Draw();
            }
        }

        // Stim ring
        if (StimRingPos.HasValue)
        {
            float progress = StimRingTime / 0.55f;
            float scale = 0.5f + progress * 0.9f;
            float alpha = (1f - progress) * 0.4f;
            var ringMat = Matrix4x4.CreateScale(scale * 2.2f) * Matrix4x4.CreateTranslation(StimRingPos.Value) * modelMatrix;
            _meshShader.SetUniform("uModel", ringMat);
            _meshShader.SetUniform("uEmission", new Vector4(1.0f, 0.9f, 0.5f, 1f));
            _meshShader.SetUniform("uOpacity", alpha);
            _sphereMesh.Draw();
        }
    }

    public void Dispose()
    {
        _pointShader.Dispose();
        _meshShader.Dispose();
        _somaCloud.Dispose();
        _circuitCloud.Dispose();
        _sphereMesh.Dispose();
    }
}
