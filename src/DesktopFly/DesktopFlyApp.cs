using System.Numerics;
using System.Windows.Forms;
using DesktopFly.Core;
using DesktopFly.Core.Behavior;
using DesktopFly.Core.Data;
using DesktopFly.Core.Models;
using DesktopFly.Core.Platform.Win32;
using DesktopFly.Core.Sim;
using DesktopFly.Rendering;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace DesktopFly;

public class DesktopFlyApp : IDisposable
{
    private const int TotalWidth = 1200;
    private const int TotalHeight = 800;
    private const int FlyWidth = 840;
    private const int BrainWidth = 360;

    private IWindow _window = null!;
    private GL _gl = null!;
    private IInputContext _input = null!;
    private SceneRenderer _sceneRenderer = null!;
    private BrainSceneRenderer? _brainRenderer = null!;
    private Coordinator _coordinator = null!;
    private LIFSim? _sim;
    private TrayIcon _trayIcon = null!;

    private readonly WindowSense _windowSense = new();
    private readonly InputSensors _inputSensors = new();

    private System.Threading.Timer? _ambientTimer;
    private System.Threading.Timer? _windowTimer;

    private float _typingLevel = 0f;
    private bool _paused = false;
    private string _stimToast = "";
    private float _stimToastTimer = 0f;

    public void Run()
    {
        var bounds = new Vector2(FlyWidth, TotalHeight);
        string dataInfo = "no data — run etl.py";
        var spikeBus = new SpikeBus();
        BrainPointsFile? brainPoints = null;

        var data = DataLoader.LoadBrainData();
        if (data.HasValue)
        {
            _sim = new LIFSim(data.Value.Circuit, spikeBus);
            brainPoints = data.Value.Points;
            dataInfo = $"FlyWire v783 · {data.Value.Points.Points.Length} somas · circuit {data.Value.Circuit.Neurons.Length}n/{data.Value.Circuit.Edges.Length}e";
        }

        _coordinator = new Coordinator(bounds, _sim);

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(TotalWidth, TotalHeight);
        options.Title = "DesktopFly 🪰 — FlyWire v783 Connectome Simulation";
        options.WindowBorder = WindowBorder.Fixed;
        options.TopMost = false;
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += () => OnLoad(brainPoints);
        _window.Render += OnRender;
        _window.Update += OnUpdate;

        // Start WinForms message pump for Tray Icon on dedicated STA thread
        var trayThread = new Thread(() =>
        {
            _trayIcon = new TrayIcon(dataInfo);
            _trayIcon.OnTogglePause += () =>
            {
                _paused = !_paused;
                _trayIcon.SetPaused(_paused);
            };
            _trayIcon.OnEscapeTest += () => _coordinator.EscapeTest();
            _trayIcon.OnAddFly += () => _coordinator.AddFly();
            _trayIcon.OnRemoveFly += () => _coordinator.RemoveFly();
            _trayIcon.OnScareFlies += () => _coordinator.ScareAll();
            _trayIcon.OnQuit += () => _window.Close();
            Application.Run();
        })
        {
            IsBackground = true
        };
        trayThread.SetApartmentState(ApartmentState.STA);
        trayThread.Start();

        _window.Run();
    }

    private bool _isBrainDragging = false;
    private Vector2 _brainDragStart = Vector2.Zero;
    private Vector2 _brainDragPrev = Vector2.Zero;
    private bool _brainDragMoved = false;

    private HudRenderer? _hudRenderer = null!;
    private readonly StimCardInfo _stimCard = new();

    private void OnLoad(BrainPointsFile? brainPoints)
    {
        _gl = _window.CreateOpenGL();
        _sceneRenderer = new SceneRenderer(_gl);

        if (_sim != null && brainPoints != null)
        {
            _brainRenderer = new BrainSceneRenderer(_gl, brainPoints, _sim);
            _hudRenderer = new HudRenderer(_gl, BrainWidth, TotalHeight);
        }

        _input = _window.CreateInput();
        foreach (var mouse in _input.Mice)
        {
            mouse.MouseMove += OnMouseMove;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.Scroll += OnMouseScroll;
        }

        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
        }

        _ambientTimer = new System.Threading.Timer(_ => PollAmbient(), null, 0, 100);
        _windowTimer = new System.Threading.Timer(_ => PollWindows(), null, 0, 700);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        float x = position.X;
        float y = position.Y;

        if (_isBrainDragging && _brainRenderer != null)
        {
            float dx = x - _brainDragPrev.X;
            float dy = y - _brainDragPrev.Y;
            _brainDragPrev = new Vector2(x, y);

            if (Vector2.Distance(new Vector2(x, y), _brainDragStart) > 4f)
            {
                _brainDragMoved = true;
            }

            _brainRenderer.RotationY += dx * 0.012f;
            _brainRenderer.RotationX = Math.Clamp(_brainRenderer.RotationX + dy * 0.012f, -1.4f, 1.4f);
            return;
        }

        if (x < FlyWidth && y >= 0 && y <= TotalHeight)
        {
            // Inside Fly Viewport: scene coordinates (center origin, +X right, +Y up)
            float sceneX = x - FlyWidth * 0.5f;
            float sceneY = TotalHeight * 0.5f - y;
            _coordinator.SetMouse(new Vector2(sceneX, sceneY));

            if (_brainRenderer != null) _brainRenderer.IsHovered = false;
        }
        else if (x >= FlyWidth && x <= TotalWidth && y >= 0 && y <= TotalHeight)
        {
            // Inside Brain Viewport
            if (_brainRenderer != null) _brainRenderer.IsHovered = true;
            _coordinator.SetMouse(null);
        }
        else
        {
            _coordinator.SetMouse(null);
            if (_brainRenderer != null) _brainRenderer.IsHovered = false;
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;

        float x = mouse.Position.X;
        float y = mouse.Position.Y;

        if (x < FlyWidth && y >= 0 && y <= TotalHeight)
        {
            // Left canvas click: substrate tap startle
            float sceneX = x - FlyWidth * 0.5f;
            float sceneY = TotalHeight * 0.5f - y;
            _coordinator.InjectTap(new Vector2(sceneX, sceneY));
        }
        else if (x >= FlyWidth && x <= TotalWidth && y >= 0 && y <= TotalHeight)
        {
            // Start dragging or click in Brain Viewport
            _isBrainDragging = true;
            _brainDragStart = new Vector2(x, y);
            _brainDragPrev = new Vector2(x, y);
            _brainDragMoved = false;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;

        if (_isBrainDragging)
        {
            float x = mouse.Position.X;
            float y = mouse.Position.Y;

            // If user clicked without dragging: trigger optogenetic raycast stimulation
            if (!_brainDragMoved && _brainRenderer != null && _sim != null)
            {
                float localX = x - FlyWidth;
                float localY = y;
                var res = BrainRaycaster.Pick(new Vector2(localX, localY), BrainWidth, TotalHeight, _brainRenderer.CurrentModelMatrix, _sim);
                if (res.HasValue)
                {
                    var (picked, anchor, regionName, description) = res.Value;
                    _sim.Stimulate(picked, 0.25f, 400);

                    for (int i = 0; i < Math.Min(16, picked.Length); i++)
                    {
                        _brainRenderer.Flash(picked[i], false);
                    }

                    _brainRenderer.AddHighlight(picked.Select(idx => _sim.Positions[idx]), 2.5f);
                    _brainRenderer.TriggerStimRing(anchor);

                    _stimCard.RegionName = regionName;
                    _stimCard.NeuronCount = picked.Length;
                    _stimCard.Description = description;
                    _stimCard.Timer = 4.0f;
                    _stimCard.MaxTimer = 4.0f;

                    _stimToast = regionName;
                    _stimToastTimer = 3.0f;
                    Console.WriteLine($"Stimulated {picked.Length} neurons: {regionName}");
                }
            }

            _isBrainDragging = false;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        float x = mouse.Position.X;
        if (x >= FlyWidth && _brainRenderer != null)
        {
            _brainRenderer.Zoom = Math.Clamp(_brainRenderer.Zoom + wheel.Y * 0.08f, 0.4f, 3.0f);
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        switch (key)
        {
            case Key.Space:
            case Key.E:
                _coordinator.EscapeTest();
                if (_sim != null && _brainRenderer != null)
                {
                    _brainRenderer.AddHighlight(_sim.GF.Select(i => _sim.Positions[i]), 2.5f);
                    _stimCard.RegionName = "⚡ Giant Fiber (DNp01) — Escape Flight!";
                    _stimCard.NeuronCount = _sim.GF.Count;
                    _stimCard.Description = BrainRaycaster.GetRegionDescription(_sim.GF, _sim);
                    _stimCard.Timer = 4.0f; _stimCard.MaxTimer = 4.0f;
                }
                _stimToast = "⚡ Escape Flight (Giant Fiber DNp01 triggered)";
                _stimToastTimer = 2.0f;
                break;
            case Key.W:
                if (_sim != null)
                {
                    _sim.Stimulate(_sim.Fwd, 0.25f, 1200);
                    _brainRenderer?.AddHighlight(_sim.Fwd.Select(i => _sim.Positions[i]), 2.5f);
                    _stimCard.RegionName = "⚡ Walking Command (DNp09)";
                    _stimCard.NeuronCount = _sim.Fwd.Count;
                    _stimCard.Description = BrainRaycaster.GetRegionDescription(_sim.Fwd, _sim);
                    _stimCard.Timer = 4.0f; _stimCard.MaxTimer = 4.0f;
                    _stimToast = "⚡ Walking Command (DNp09 stimulated)";
                    _stimToastTimer = 2.0f;
                }
                break;
            case Key.G:
                if (_sim != null)
                {
                    _sim.Stimulate(_sim.Groom, 0.25f, 600);
                    _brainRenderer?.AddHighlight(_sim.Groom.Select(i => _sim.Positions[i]), 2.5f);
                    _stimCard.RegionName = "⚡ Grooming Command (DNg11)";
                    _stimCard.NeuronCount = _sim.Groom.Count;
                    _stimCard.Description = BrainRaycaster.GetRegionDescription(_sim.Groom, _sim);
                    _stimCard.Timer = 4.0f; _stimCard.MaxTimer = 4.0f;
                    _stimToast = "⚡ Grooming Command (DNg11 stimulated)";
                    _stimToastTimer = 2.0f;
                }
                break;
            case Key.M:
            case Key.B:
                if (_sim != null)
                {
                    _sim.Stimulate(_sim.MDN, 0.30f, 600);
                    _brainRenderer?.AddHighlight(_sim.MDN.Select(i => _sim.Positions[i]), 2.5f);
                    _stimCard.RegionName = "⚡ Moonwalker Neurons (MDN)";
                    _stimCard.NeuronCount = _sim.MDN.Count;
                    _stimCard.Description = BrainRaycaster.GetRegionDescription(_sim.MDN, _sim);
                    _stimCard.Timer = 4.0f; _stimCard.MaxTimer = 4.0f;
                    _stimToast = "⚡ Moonwalker (MDN backward walk stimulated)";
                    _stimToastTimer = 2.0f;
                }
                break;
            case Key.P:
                _paused = !_paused;
                _trayIcon?.SetPaused(_paused);
                _stimToast = _paused ? "⏸ Simulation Paused" : "▶ Simulation Resumed";
                _stimToastTimer = 1.5f;
                break;
            case Key.A:
                _coordinator.AddFly();
                _stimToast = $"🪰 Added Fly ({_coordinator.Flies.Count} total)";
                _stimToastTimer = 1.5f;
                break;
            case Key.R:
                _coordinator.RemoveFly();
                _stimToast = $"🪰 Removed Fly ({_coordinator.Flies.Count} total)";
                _stimToastTimer = 1.5f;
                break;
        }
    }

    private void PollAmbient()
    {
        float idle = InputSensors.GetUserIdleSeconds();
        _typingLevel += ((idle < 0.6f ? 1.0f : 0.0f) - _typingLevel) * 0.15f;

        var now = DateTime.Now;
        double h = now.Hour + now.Minute / 60.0;
        bool sleepy = (idle > 600 && (h >= 22 || h < 6)) || idle > 1800;

        _coordinator.SetAmbient(
            _typingLevel,
            sleepy,
            ThermalSense.GetThermalTempo(),
            Circadian.Activity(h)
        );
    }

    private void PollWindows()
    {
        var snap = _windowSense.Poll(FlyWidth, TotalHeight, 0, 0);
        _coordinator.SetTerrain(snap.Ledges);
    }

    private void OnUpdate(double dt)
    {
        if (!_paused)
        {
            _coordinator.UpdateAtTime(_window.Time);
        }

        _brainRenderer?.Update((float)dt);

        if (_stimCard.Timer > 0f)
        {
            _stimCard.Timer = Math.Max(0f, _stimCard.Timer - (float)dt);
        }

        if (_stimToastTimer > 0f)
        {
            _stimToastTimer -= (float)dt;
            if (_stimToastTimer <= 0f)
            {
                _window.Title = "DesktopFly 🪰 — FlyWire v783 [Space=Escape, G=Groom, W=Walk, M=Moonwalk, P=Pause, Click=Stimulate]";
            }
            else
            {
                _window.Title = $"DesktopFly 🪰 — {_stimToast}";
            }
        }
    }

    private void OnRender(double dt)
    {
        _gl.Enable(EnableCap.ScissorTest);

        // ==========================================
        // 1. Left Viewport: 3D Fly (840 x 800)
        // ==========================================
        _gl.Viewport(0, 0, FlyWidth, TotalHeight);
        _gl.Scissor(0, 0, FlyWidth, TotalHeight);
        _gl.ClearColor(0.11f, 0.13f, 0.17f, 1.0f); // Modern dark workspace background
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var flyCameraPos = new Vector3(0f, 0f, 300f);
        var flyViewMatrix = Matrix4x4.CreateLookAt(flyCameraPos, Vector3.Zero, Vector3.UnitY);
        var flyProjMatrix = Matrix4x4.CreateOrthographic(FlyWidth, TotalHeight, 1f, 600f);

        foreach (var fly in _coordinator.Flies)
        {
            _sceneRenderer.Render(fly.Node, flyViewMatrix, flyProjMatrix, flyCameraPos);
        }

        // ==========================================
        // 2. Right Viewport: 3D Brain + HUD (360 x 800)
        // ==========================================
        if (_brainRenderer != null)
        {
            _gl.Viewport(FlyWidth, 0, BrainWidth, TotalHeight);
            _gl.Scissor(FlyWidth, 0, BrainWidth, TotalHeight);
            _gl.ClearColor(0.03f, 0.035f, 0.06f, 1.0f); // Deep space connectome background
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _brainRenderer.Render(BrainWidth, TotalHeight);
            _hudRenderer?.Render(_stimCard);
        }

        _gl.Disable(EnableCap.ScissorTest);
    }

    public void Dispose()
    {
        _ambientTimer?.Dispose();
        _windowTimer?.Dispose();
        _trayIcon?.Dispose();
        _hudRenderer?.Dispose();
        _brainRenderer?.Dispose();
        _sceneRenderer?.Dispose();
        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}
