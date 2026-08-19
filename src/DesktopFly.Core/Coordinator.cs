using System.Numerics;
using DesktopFly.Core.Behavior;
using DesktopFly.Core.Models;
using DesktopFly.Core.Sim;

namespace DesktopFly.Core;

public class Coordinator
{
    public Vector2 Bounds { get; set; }
    public List<Fly> Flies { get; } = new();
    public LIFSim? Sim { get; }

    private readonly object _lock = new();
    private readonly List<Action<Coordinator>> _pending = new();
    private readonly SignalBuilder _signalBuilder = new();

    private double? _lastTime;
    private Vector2? _mouseScene;
    private Vector2? _prevMouse;
    private Vector2 _mouseVel = Vector2.Zero;
    private float _loomOverride = 0f;
    private double _msAccumulator = 0.0;

    private List<Ledge> _terrain = new();
    private float _typingLevel = 0f;
    private bool _sleepy = false;
    private float _tempo = 1f;
    private float _activity = 1f;
    private float _windowLoomL = 0f;
    private float _windowLoomR = 0f;
    private Vector2 _lastFlyPos = Vector2.Zero;

    private static float Clamp(float v, float lo, float hi) => Math.Min(hi, Math.Max(lo, v));

    public Coordinator(Vector2 bounds, LIFSim? sim)
    {
        Bounds = bounds;
        Sim = sim;
        Enqueue(c => c.AddFlyNow());
    }

    public void Enqueue(Action<Coordinator> action)
    {
        lock (_lock) { _pending.Add(action); }
    }

    private void AddFlyNow()
    {
        float hw = Bounds.X / 2f - 100f;
        float hh = Bounds.Y / 2f - 100f;
        var rand = new Random();
        float x = (float)(rand.NextDouble() * 2 - 1) * hw;
        float y = (float)(rand.NextDouble() * 2 - 1) * hh;
        var fly = new Fly(new Vector2(x, y));
        Flies.Add(fly);
    }

    public void AddFly() => Enqueue(c => c.AddFlyNow());

    public void RemoveFly()
    {
        Enqueue(c =>
        {
            if (c.Flies.Count > 1) // Fly #1 carries the connectome brain
            {
                var last = c.Flies[^1];
                c.Flies.RemoveAt(c.Flies.Count - 1);
                last.Node.RemoveFromParentNode();
            }
        });
    }

    public void ScareAll()
    {
        Enqueue(c =>
        {
            c._loomOverride = 0.8f;
            foreach (var fly in c.Flies)
            {
                if (fly.State != Fly.FlyState.Flying)
                    fly.StartFlight(c.Bounds, escape: true);
            }
        });
    }

    public void EscapeTest()
    {
        Enqueue(c =>
        {
            c._loomOverride = 1.0f;
            c.Sim?.Stimulate(c.Sim.GF, 0.5f, 40);
            foreach (var fly in c.Flies)
            {
                if (fly.State != Fly.FlyState.Flying)
                    fly.StartFlight(c.Bounds, escape: true);
            }
        });
    }

    public void SetMouse(Vector2? p)
    {
        lock (_lock) { _mouseScene = p; }
    }

    public void SetTerrain(List<Ledge> ledges) => Enqueue(c => c._terrain = ledges);

    public void Retarget(Vector2 size)
    {
        Enqueue(c =>
        {
            c.Bounds = size;
            c._terrain.Clear();
            foreach (var fly in c.Flies)
            {
                fly.CurrentLedge = null;
                fly.Pos = new Vector2(
                    Clamp(fly.Pos.X, -size.X / 2f + 40f, size.X / 2f - 40f),
                    Clamp(fly.Pos.Y, -size.Y / 2f + 40f, size.Y / 2f - 40f)
                );
            }
        });
    }

    public void SetAmbient(float typing, bool sleepy, float tempo, float activity)
    {
        Enqueue(c =>
        {
            c._typingLevel = typing;
            c._sleepy = sleepy;
            c._tempo = tempo;
            c._activity = activity;
        });
    }

    public Vector2 FlyPosition()
    {
        lock (_lock) { return _lastFlyPos; }
    }

    public void InjectWindowLoom(float strength, Vector2 p)
    {
        Enqueue(c =>
        {
            var first = c.Flies.FirstOrDefault();
            if (first == null) return;
            var rel = p - first.Pos;
            float dist = Math.Max(1f, rel.Length());
            var f = new Vector2(MathF.Cos(first.Heading), MathF.Sin(first.Heading));
            float crossZ = (f.X * rel.Y - f.Y * rel.X) / dist;
            c._windowLoomL = Math.Max(c._windowLoomL, strength * Clamp(0.5f + 0.5f * crossZ, 0.12f, 1f));
            c._windowLoomR = Math.Max(c._windowLoomR, strength * Clamp(0.5f - 0.5f * crossZ, 0.12f, 1f));
        });
    }

    public void InjectTap(Vector2 p)
    {
        Enqueue(c =>
        {
            if (c.Sim == null || c.Flies.Count == 0) return;
            float minD = float.MaxValue;
            foreach (var fly in c.Flies)
            {
                float d = Vector2.Distance(p, fly.Pos);
                if (d < minD) minD = d;
            }
            float strength = Clamp(1f - minD / 320f, 0f, 1f);
            if (strength > 0.05f)
            {
                c.Sim.Stimulate(c.Sim.Sens, 0.15f + strength * 0.35f, 130);
            }
        });
    }

    private (float L, float R, float Puff) ComputeLoom(Fly fly, Vector2? mouse, float dt)
    {
        if (!mouse.HasValue) return (0f, 0f, 0f);
        var m = mouse.Value;

        if (_prevMouse.HasValue && dt > 0f)
        {
            var v = (m - _prevMouse.Value) / dt;
            _mouseVel.X += (v.X - _mouseVel.X) * 0.4f;
            _mouseVel.Y += (v.Y - _mouseVel.Y) * 0.4f;
        }
        _prevMouse = m;

        var rel = m - fly.Pos;
        float dist = Math.Max(15f, rel.Length());
        float approach = -(rel.X * _mouseVel.X + rel.Y * _mouseVel.Y) / dist;

        // Visual Looming: active within 260 px, proportional to relative approach speed / distance
        float loom = 0f;
        if (approach > 50f && dist < 260f)
        {
            loom = Clamp(approach / dist * 4.5f, 0f, 1f) * Clamp(1f - dist / 260f, 0f, 1f);
        }

        // Close proximity threat: within 70 px
        if (dist < 70f)
        {
            loom += Clamp((70f - dist) / 70f, 0f, 1f) * 0.6f;
        }

        loom = Clamp(loom + _loomOverride, 0f, 1f);

        var f = new Vector2(MathF.Cos(fly.Heading), MathF.Sin(fly.Heading));
        var rd = rel / dist;
        float crossZ = f.X * rd.Y - f.Y * rd.X;
        float lw = Clamp(0.5f + 0.5f * crossZ, 0.12f, 1f);
        float rw = Clamp(0.5f - 0.5f * crossZ, 0.12f, 1f);

        // Air puff: fast cursor sweep within 180 px
        float speed = _mouseVel.Length();
        float puff = 0f;
        if (speed > 250f && dist < 180f)
        {
            puff = Clamp((speed - 250f) / 1200f, 0f, 1f) * Clamp(1f - dist / 180f, 0f, 1f);
        }

        return (loom * lw, loom * rw, puff);
    }

    public void UpdateAtTime(double t)
    {
        List<Action<Coordinator>> actions;
        Vector2? mouse;
        lock (_lock)
        {
            actions = new List<Action<Coordinator>>(_pending);
            _pending.Clear();
            mouse = _mouseScene;
        }

        foreach (var a in actions) a(this);

        if (!_lastTime.HasValue)
        {
            _lastTime = t;
            return;
        }

        float dt = (float)Math.Min(0.05, Math.Max(0.0, t - _lastTime.Value));
        _lastTime = t;

        BrainSignals? signals = null;
        if (Sim != null && Flies.Count > 0)
        {
            (float L, float R, float Puff) sensory = (0f, 0f, 0f);
            foreach (var fly in Flies)
            {
                var s = ComputeLoom(fly, mouse, dt);
                if (s.L + s.R + s.Puff > sensory.L + sensory.R + sensory.Puff)
                {
                    sensory = s;
                }
            }

            float decayF = MathF.Exp(-4f * dt);
            _windowLoomL *= decayF;
            _windowLoomR *= decayF;

            Sim.LoomL = Math.Max(sensory.L, _windowLoomL);
            Sim.LoomR = Math.Max(sensory.R, _windowLoomR);
            Sim.AirPuff = Math.Max(sensory.Puff, _typingLevel * 0.30f);

            var first = Flies[0];
            Sim.GaitDrive = first.WalkingIntensity;
            Sim.GaitPhase = first.GaitPhasePublic;

            Sim.ActivityScale = (1f - (1f - _activity) * 0.35f) * (_sleepy ? 0.75f : 1f);
            Sim.SensoryGate = _sleepy ? 0.55f : 1f;
            _loomOverride = Math.Max(0f, _loomOverride - dt * 1.2f);

            _msAccumulator += dt * 1000.0;
            int steps = Math.Min(50, (int)_msAccumulator);
            _msAccumulator -= steps;
            Sim.Step(steps);

            var sSignals = _signalBuilder.Make(Sim, dt);
            sSignals.Tempo = _tempo;
            sSignals.Sleep = _sleepy;
            signals = sSignals;
        }

        for (int i = 0; i < Flies.Count; i++)
        {
            var fly = Flies[i];
            fly.Terrain = _terrain;
            fly.Update(dt, Bounds, mouse, signals);
        }

        if (Flies.Count > 0)
        {
            lock (_lock) { _lastFlyPos = Flies[0].Pos; }
        }
    }
}
