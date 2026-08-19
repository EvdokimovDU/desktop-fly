using System.Numerics;
using DesktopFly.Core;
using DesktopFly.Core.Models;
using DesktopFly.Core.Sim;
using DesktopFly.Rendering;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace DesktopFly;

public class BrainWindowController : IDisposable
{
    private readonly IWindow _window;
    private GL? _gl;
    private BrainSceneRenderer? _renderer;
    private readonly BrainPointsFile _points;
    private readonly LIFSim _sim;

    public string? CurrentLabel { get; private set; }
    public float LabelTimer { get; private set; } = 0f;

    public bool IsVisible
    {
        get => _window.IsVisible;
        set => _window.IsVisible = value;
    }

    public BrainWindowController(BrainPointsFile points, LIFSim sim, Vector2D<int> screenPos)
    {
        _points = points;
        _sim = sim;

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(340, 280);
        options.Position = screenPos;
        options.Title = "Fly Brain — FlyWire v783 (click = stimulate)";
        options.WindowBorder = WindowBorder.Fixed;
        options.IsVisible = true;
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;
        _window.Closing += OnClosing;
    }

    public void Initialize()
    {
        _window.Initialize();
    }

    public void Run()
    {
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _renderer = new BrainSceneRenderer(_gl, _points, _sim);

        var input = _window.CreateInput();
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, btn) =>
            {
                if (btn == MouseButton.Left)
                {
                    HandleClick(new Vector2(mouse.Position.X, mouse.Position.Y));
                }
            };
        }
    }

    private void HandleClick(Vector2 mousePos)
    {
        if (_renderer == null) return;

        var res = BrainRaycaster.Pick(mousePos, _window.Size.X, _window.Size.Y, _renderer.CurrentModelMatrix, _sim);
        if (res.HasValue)
        {
            var (picked, anchor, regionName, description) = res.Value;
            _sim.Stimulate(picked, 0.25f, 400);

            for (int i = 0; i < Math.Min(16, picked.Length); i++)
            {
                _renderer.Flash(picked[i], false);
            }

            _renderer.TriggerStimRing(anchor);
            CurrentLabel = regionName;
            LabelTimer = 2.2f;
            Console.WriteLine($"Stimulated {picked.Length} neurons: {regionName}");
        }
    }

    private void OnUpdate(double dt)
    {
        _renderer?.Update((float)dt);

        if (LabelTimer > 0f)
        {
            LabelTimer -= (float)dt;
            if (LabelTimer <= 0f) CurrentLabel = null;
        }
    }

    private void OnRender(double dt)
    {
        if (_gl == null || _renderer == null) return;

        _gl.Viewport(0, 0, (uint)_window.Size.X, (uint)_window.Size.Y);
        _gl.ClearColor(0.03f, 0.035f, 0.06f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _renderer.Render(_window.Size.X, _window.Size.Y);
    }

    private void OnClosing()
    {
        _window.IsVisible = false; // Hide instead of closing main app
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _gl?.Dispose();
        _window.Dispose();
    }
}
